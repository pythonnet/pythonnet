using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Python.Runtime.Platform;
using Python.Runtime.Slots;

namespace Python.Runtime
{

    /// <summary>
    /// The TypeManager class is responsible for building binary-compatible
    /// Python type objects that are implemented in managed code.
    /// </summary>
    internal class TypeManager
    {
        private static BindingFlags tbFlags;
        private static Dictionary<Type, IntPtr> cache;

        static TypeManager()
        {
            tbFlags = BindingFlags.Public | BindingFlags.Static;
            cache = new Dictionary<Type, IntPtr>(128);
        }

        public static void Reset()
        {
            cache = new Dictionary<Type, IntPtr>(128);
        }

        /// <summary>
        /// Given a managed Type derived from ExtensionType, get the handle to
        /// a Python type object that delegates its implementation to the Type
        /// object. These Python type instances are used to implement internal
        /// descriptor and utility types like ModuleObject, PropertyObject, etc.
        /// </summary>
        internal static IntPtr GetTypeHandle(Type type)
        {
            // Note that these types are cached with a refcount of 1, so they
            // effectively exist until the CPython runtime is finalized.
            IntPtr handle;
            cache.TryGetValue(type, out handle);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }
            handle = CreateType(type);
            cache[type] = handle;
            return handle;
        }


        /// <summary>
        /// Get the handle of a Python type that reflects the given CLR type.
        /// The given ManagedType instance is a managed object that implements
        /// the appropriate semantics in Python for the reflected managed type.
        /// </summary>
        internal static IntPtr GetTypeHandle(ManagedType obj, Type type)
        {
            IntPtr handle;
            cache.TryGetValue(type, out handle);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }
            handle = CreateType(obj, type);
            cache[type] = handle;
            return handle;
        }


        /// <summary>
        /// The following CreateType implementations do the necessary work to
        /// create Python types to represent managed extension types, reflected
        /// types, subclasses of reflected types and the managed metatype. The
        /// dance is slightly different for each kind of type due to different
        /// behavior needed and the desire to have the existing Python runtime
        /// do as much of the allocation and initialization work as possible.
        /// </summary>
        internal static IntPtr CreateType(Type impl)
        {
            var slotArray = CreateSlotArray(impl);
            int flags = TypeFlags.Default | TypeFlags.Managed |
                        TypeFlags.HeapType | TypeFlags.HaveGC;

            IntPtr type = CreateTypeObject(impl.Name, ObjectOffset.Size(), flags, slotArray);

            if (ObjectOffset.Size() != ObjectOffset.Size(type))
            {
                //should we reset the size and call PyType_Ready again??
                //how do we deal with the fact that size is based on whether
                //the type is an exception type.  Should CreateSlotArray
                //return a tuple with both the slot array and a flag on
                //whether the type array describes an exception or not?
            }

            var offset = (IntPtr)ObjectOffset.TypeDictOffset(type);
            Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, offset);

            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(dict, "__module__", mod);

            InitMethods(type, impl);

            return type;
        }


        internal static IntPtr CreateType(ManagedType impl, Type clrType)
        {
            // Cleanup the type name to get rid of funny nested type names.
            string name = "CLR." + clrType.FullName;
            int i = name.LastIndexOf('+');
            if (i > -1)
            {
                name = name.Substring(i + 1);
            }
            i = name.LastIndexOf('.');
            if (i > -1)
            {
                name = name.Substring(i + 1);
            }

            IntPtr base_ = IntPtr.Zero;
            int ob_size = ObjectOffset.Size(Runtime.PyTypeType);

            // XXX Hack, use a different base class for System.Exception
            // Python 2.5+ allows new style class exceptions but they *must*
            // subclass BaseException (or better Exception).
            if (typeof(Exception).IsAssignableFrom(clrType))
            {
                ob_size = ObjectOffset.Size(Exceptions.Exception);
            }

            int tp_dictoffset = ob_size + ManagedDataOffsets.ob_dict;

            if (clrType == typeof(Exception))
            {
                base_ = Exceptions.Exception;
            }
            else if (clrType.BaseType != null)
            {
                ClassBase bc = ClassManager.GetClass(clrType.BaseType);
                base_ = bc.pyHandle;
            }

            IntPtr type = AllocateTypeObject(name);

            Marshal.WriteIntPtr(type, TypeOffset.ob_type, Runtime.PyCLRMetaType);
            Runtime.XIncref(Runtime.PyCLRMetaType);

            Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);
            Marshal.WriteIntPtr(type, TypeOffset.tp_itemsize, IntPtr.Zero);
            Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, (IntPtr)tp_dictoffset);

            // add a __len__ slot for inheritors of ICollection and ICollection<>
            if (typeof(ICollection).IsAssignableFrom(clrType) || clrType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                InitializeSlot(type, TypeOffset.mp_length, typeof(mp_length_slot).GetMethod(nameof(mp_length_slot.mp_length)));
            }

            // we want to do this after the slot stuff above in case the class itself implements a slot method
            InitializeSlots(type, impl.GetType());

            if (base_ != IntPtr.Zero)
            {
                Marshal.WriteIntPtr(type, TypeOffset.tp_base, base_);
                Runtime.XIncref(base_);
            }

            int flags = TypeFlags.Default;
            flags |= TypeFlags.Managed;
            flags |= TypeFlags.HeapType;
            flags |= TypeFlags.BaseType;
            flags |= TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            // Leverage followup initialization from the Python runtime. Note
            // that the type of the new type must PyType_Type at the time we
            // call this, else PyType_Ready will skip some slot initialization.

            Runtime.PyType_Ready(type);

            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            string mn = clrType.Namespace ?? "";
            IntPtr mod = Runtime.PyString_FromString(mn);
            Runtime.PyDict_SetItemString(dict, "__module__", mod);

            // Hide the gchandle of the implementation in a magic type slot.
            GCHandle gc = GCHandle.Alloc(impl);
            Marshal.WriteIntPtr(type, TypeOffset.magic(), (IntPtr)gc);

            // Set the handle attributes on the implementing instance.
            impl.tpHandle = Runtime.PyCLRMetaType;
            impl.gcHandle = gc;
            impl.pyHandle = type;

            //DebugUtil.DumpType(type);

            return type;
        }

        static PY_TYPE_SLOT InitializeSlot(TypeSlots slotNumber, MethodInfo method)
        {
            var thunk = Interop.GetThunk(method);
            return new PY_TYPE_SLOT { slot = slotNumber, func = thunk.Address};
        }

        static PY_TYPE_SLOT InitializeSlot(TypeSlots slotNumber, IntPtr thunk)
        {
            return new PY_TYPE_SLOT { slot = slotNumber, func = thunk };
        }

        static void InitializeSlot(IntPtr type, int slotOffset, MethodInfo method)
        {
            var thunk = Interop.GetThunk(method);
            Marshal.WriteIntPtr(type, slotOffset, thunk.Address);
        }

        internal static IntPtr CreateSubType(IntPtr py_name, IntPtr py_base_type, IntPtr py_dict)
        {
            // Utility to create a subtype of a managed type with the ability for the
            // a python subtype able to override the managed implementation
            string name = Runtime.GetManagedString(py_name);

            // the derived class can have class attributes __assembly__ and __module__ which
            // control the name of the assembly and module the new type is created in.
            object assembly = null;
            object namespaceStr = null;

            var disposeList = new List<PyObject>();
            try
            {
                var assemblyKey = new PyObject(Converter.ToPython("__assembly__", typeof(string)));
                disposeList.Add(assemblyKey);
                if (0 != Runtime.PyMapping_HasKey(py_dict, assemblyKey.Handle))
                {
                    var pyAssembly = new PyObject(Runtime.PyDict_GetItem(py_dict, assemblyKey.Handle));
                    Runtime.XIncref(pyAssembly.Handle);
                    disposeList.Add(pyAssembly);
                    if (!Converter.ToManagedValue(pyAssembly.Handle, typeof(string), out assembly, false))
                    {
                        throw new InvalidCastException("Couldn't convert __assembly__ value to string");
                    }
                }

                var namespaceKey = new PyObject(Converter.ToPythonImplicit("__namespace__"));
                disposeList.Add(namespaceKey);
                if (0 != Runtime.PyMapping_HasKey(py_dict, namespaceKey.Handle))
                {
                    var pyNamespace = new PyObject(Runtime.PyDict_GetItem(py_dict, namespaceKey.Handle));
                    Runtime.XIncref(pyNamespace.Handle);
                    disposeList.Add(pyNamespace);
                    if (!Converter.ToManagedValue(pyNamespace.Handle, typeof(string), out namespaceStr, false))
                    {
                        throw new InvalidCastException("Couldn't convert __namespace__ value to string");
                    }
                }
            }
            finally
            {
                foreach (PyObject o in disposeList)
                {
                    o.Dispose();
                }
            }

            // create the new managed type subclassing the base managed type
            var baseClass = ManagedType.GetManagedObject(py_base_type) as ClassBase;
            if (null == baseClass)
            {
                return Exceptions.RaiseTypeError("invalid base class, expected CLR class type");
            }

            try
            {
                Type subType = ClassDerivedObject.CreateDerivedType(name,
                    baseClass.type,
                    py_dict,
                    (string)namespaceStr,
                    (string)assembly);

                // create the new ManagedType and python type
                ClassBase subClass = ClassManager.GetClass(subType);
                IntPtr py_type = GetTypeHandle(subClass, subType);

                // by default the class dict will have all the C# methods in it, but as this is a
                // derived class we want the python overrides in there instead if they exist.
                IntPtr cls_dict = Marshal.ReadIntPtr(py_type, TypeOffset.tp_dict);
                Runtime.PyDict_Update(cls_dict, py_dict);

                // Update the __classcell__ if it exists
                var cell = new BorrowedReference(Runtime.PyDict_GetItemString(cls_dict, "__classcell__"));
                if (!cell.IsNull)
                {
                    Runtime.PyCell_Set(cell, py_type);
                    Runtime.PyDict_DelItemString(cls_dict, "__classcell__");
                }

                return py_type;
            }
            catch (Exception e)
            {
                return Exceptions.RaiseTypeError(e.Message);
            }
        }

        internal static IntPtr WriteMethodDef(IntPtr mdef, IntPtr name, IntPtr func, int flags, IntPtr doc)
        {
            Marshal.WriteIntPtr(mdef, name);
            Marshal.WriteIntPtr(mdef, 1 * IntPtr.Size, func);
            Marshal.WriteInt32(mdef, 2 * IntPtr.Size, flags);
            Marshal.WriteIntPtr(mdef, 3 * IntPtr.Size, doc);
            return mdef + 4 * IntPtr.Size;
        }

        internal static IntPtr WriteMethodDef(IntPtr mdef, string name, IntPtr func, int flags = 0x0001,
            string doc = null)
        {
            IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);
            IntPtr docPtr = doc != null ? Marshal.StringToHGlobalAnsi(doc) : IntPtr.Zero;

            return WriteMethodDef(mdef, namePtr, func, flags, docPtr);
        }

        internal static IntPtr WriteMethodDefSentinel(IntPtr mdef)
        {
            return WriteMethodDef(mdef, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        }

        internal static IntPtr CreateMetaType(Type impl)
        {
            // The managed metatype is functionally little different than the
            // standard Python metatype (PyType_Type). It overrides certain of
            // the standard type slots, and has to subclass PyType_Type for
            // certain functions in the C runtime to work correctly with it.

            IntPtr type = AllocateTypeObject("CLR Metatype");
            IntPtr py_type = Runtime.PyTypeType;

            Marshal.WriteIntPtr(type, TypeOffset.tp_base, py_type);
            Runtime.XIncref(py_type);

            // Slots will inherit from TypeType, it's not neccesary for setting them.
            // Inheried slots:
            // tp_basicsize, tp_itemsize,
            // tp_dictoffset, tp_weaklistoffset,
            // tp_traverse, tp_clear, tp_is_gc, etc.

            // Override type slots with those of the managed implementation.

            InitializeSlots(type, impl);

            int flags = TypeFlags.Default;
            flags |= TypeFlags.Managed;
            flags |= TypeFlags.HeapType;
            flags |= TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            // We need space for 3 PyMethodDef structs, each of them
            // 4 int-ptrs in size.
            IntPtr mdef = Runtime.PyMem_Malloc(3 * 4 * IntPtr.Size);
            IntPtr mdefStart = mdef;
            ThunkInfo thunkInfo = Interop.GetThunk(typeof(MetaType).GetMethod("__instancecheck__"), "BinaryFunc");
            mdef = WriteMethodDef(
                mdef,
                "__instancecheck__",
                thunkInfo.Address
            );

            thunkInfo = Interop.GetThunk(typeof(MetaType).GetMethod("__subclasscheck__"), "BinaryFunc");
            mdef = WriteMethodDef(
                mdef,
                "__subclasscheck__",
                thunkInfo.Address
            );

            // FIXME: mdef is not used
            mdef = WriteMethodDefSentinel(mdef);

            Marshal.WriteIntPtr(type, TypeOffset.tp_methods, mdefStart);

            Runtime.PyType_Ready(type);

            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(dict, "__module__", mod);

            //DebugUtil.DumpType(type);

            return type;
        }


        internal static IntPtr BasicSubType(string name, IntPtr base_, Type impl)
        {
            // Utility to create a subtype of a std Python type, but with
            // a managed type able to override implementation

            IntPtr type = AllocateTypeObject(name);
            //Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)obSize);
            //Marshal.WriteIntPtr(type, TypeOffset.tp_itemsize, IntPtr.Zero);

            //IntPtr offset = (IntPtr)ObjectOffset.ob_dict;
            //Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, offset);

            //IntPtr dc = Runtime.PyDict_Copy(dict);
            //Marshal.WriteIntPtr(type, TypeOffset.tp_dict, dc);

            Marshal.WriteIntPtr(type, TypeOffset.tp_base, base_);
            Runtime.XIncref(base_);

            int flags = TypeFlags.Default;
            flags |= TypeFlags.Managed;
            flags |= TypeFlags.HeapType;
            flags |= TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            CopySlot(base_, type, TypeOffset.tp_traverse);
            CopySlot(base_, type, TypeOffset.tp_clear);
            CopySlot(base_, type, TypeOffset.tp_is_gc);

            InitializeSlots(type, impl);

            Runtime.PyType_Ready(type);

            IntPtr tp_dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(tp_dict, "__module__", mod);

            return type;
        }

        internal enum TypeSlots : int
        {
            bf_getbuffer = 1,
            bf_releasebuffer = 2,
            mp_ass_subscript = 3,
            mp_length = 4,
            mp_subscript = 5,
            nb_absolute = 6,
            nb_add = 7,
            nb_and = 8,
            nb_bool = 9,
            nb_divmod = 10,
            nb_float = 11,
            nb_floor_divide = 12,
            nb_index = 13,
            nb_inplace_add = 14,
            nb_inplace_and = 15,
            nb_inplace_floor_divide = 16,
            nb_inplace_lshift = 17,
            nb_inplace_multiply = 18,
            nb_inplace_or = 19,
            nb_inplace_power = 20,
            nb_inplace_remainder = 21,
            nb_inplace_rshift = 22,
            nb_inplace_subtract = 23,
            nb_inplace_true_divide = 24,
            nb_inplace_xor = 25,
            nb_int = 26,
            nb_invert = 27,
            nb_lshift = 28,
            nb_multiply = 29,
            nb_negative = 30,
            nb_or = 31,
            nb_positive = 32,
            nb_power = 33,
            nb_remainder = 34,
            nb_rshift = 35,
            nb_subtract = 36,
            nb_true_divide = 37,
            nb_xor = 38,
            sq_ass_item = 39,
            sq_concat = 40,
            sq_contains = 41,
            sq_inplace_concat = 42,
            sq_inplace_repeat = 43,
            sq_item = 44,
            sq_length = 45,
            sq_repeat = 46,
            tp_alloc = 47,
            tp_base = 48,
            tp_bases = 49,
            tp_call = 50,
            tp_clear = 51,
            tp_dealloc = 52,
            tp_del = 53,
            tp_descr_get = 54,
            tp_descr_set = 55,
            tp_doc = 56,
            tp_getattr = 57,
            tp_getattro = 58,
            tp_hash = 59,
            tp_init = 60,
            tp_is_gc = 61,
            tp_iter = 62,
            tp_iternext = 63,
            tp_methods = 64,
            tp_new = 65,
            tp_repr = 66,
            tp_richcompare = 67,
            tp_setattr = 68,
            tp_setattro = 69,
            tp_str = 70,
            tp_traverse = 71,
            tp_members = 72,
            tp_getset = 73,
            tp_free = 74,
            nb_matrix_multiply = 75,
            nb_inplace_matrix_multiply = 76,
            am_await = 77,
            am_aiter = 78,
            am_anext = 79,
            tp_finalize = 80,
        }

        private static TypeSlots getSlotNumber(string methodName)
        {
            return (TypeSlots)Enum.Parse(typeof(TypeSlots), methodName);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PY_TYPE_SLOT
        {
            internal TypeSlots slot; //slot id, from typeslots.h
            internal IntPtr func; //function pointer of the function implementing the slot
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal class PyTypeSpecOffset
        {
            static PyTypeSpecOffset()
            {
                Type type = typeof(PyTypeSpecOffset);
                FieldInfo[] fi = type.GetFields();
                int size = IntPtr.Size;
                for (int i = 0; i < fi.Length; i++)
                {
                    fi[i].SetValue(null, i * size + TypeOffset.ob_size);
                }
            }

            public static IntPtr AllocPyTypeSpec(string typename, int obSize, int obFlags, IntPtr slotsPtr)
            {
                byte[] ascii = System.Text.Encoding.ASCII.GetBytes(typename);

                //This approach is the same as the one in interop.cs for AllocModuleDef
                //allocate the size of the struct (which is given by the value of the last
                //static member and enough space to hold to typename as a char buffer.  The
                //amount of space needed is the length of the string and the null terminator
                //char* name member will simply point to the position of the buffer.
                int size = name_value + ascii.Length + 1;
                IntPtr specPtr = Marshal.AllocHGlobal(size);

                Marshal.Copy(ascii, 0, specPtr + name_value, ascii.Length);
                Marshal.WriteIntPtr(specPtr, name, specPtr + name_value);
                Marshal.WriteByte(specPtr, name_value + ascii.Length, 0);

                Marshal.WriteInt32(specPtr, basicsize, obSize);
                Marshal.WriteInt32(specPtr, itemsize, 0);
                Marshal.WriteInt32(specPtr, flags, obFlags);
                //Util.WriteCLong(specPtr, basicsize, obFlags);
                //Util.WriteCLong(specPtr, itemsize, 0);
                //Util.WriteCLong(specPtr, flags, obFlags);

                Marshal.WriteIntPtr(specPtr, slots, slotsPtr);
                return specPtr;
            }

            public static int name = 0;
            public static int basicsize = name + IntPtr.Size;
            public static int itemsize = basicsize + 4;
            public static int flags = itemsize + 4;
            public static int slots = flags + 4;

            public static int name_value = slots + IntPtr.Size;
        }


        internal static IntPtr CreateTypeObject(string name, int obSize, int obFlags, PY_TYPE_SLOT[] type_slots)
        {
            //type_slots *must* be terminated by a {0,0} entry.  TODO - Should I check/throw?

            //convert type slot array into intptr
            int structSize = Marshal.SizeOf(typeof(PY_TYPE_SLOT));
            GCHandle pinnedArray = GCHandle.Alloc(type_slots, GCHandleType.Pinned);

            //Well, this will leak.  Maybe pinnedArray should be added as a member to managedtype.
            IntPtr slotsPtr = pinnedArray.AddrOfPinnedObject();
            //pinnedArray.Free(); //at some point

            //create a type from the spec and return it.
            IntPtr specPtr = PyTypeSpecOffset.AllocPyTypeSpec(name, obSize, obFlags, slotsPtr);
            IntPtr typePtr = Runtime.PyType_FromSpec(specPtr);
            return typePtr;

            //TODO - taken from AllocateTypeObject.  I have no idea what this is meant to do.
            /* 
            // Cheat a little: we'll set tp_name to the internal char * of
            // the Python version of the type name - otherwise we'd have to
            // allocate the tp_name and would have no way to free it.
            IntPtr temp = Runtime.PyUnicode_FromString(name);
            IntPtr raw = Runtime.PyUnicode_AsUTF8(temp);
            Marshal.WriteIntPtr(type, TypeOffset.tp_name, raw);
            Marshal.WriteIntPtr(type, TypeOffset.name, temp);

            Marshal.WriteIntPtr(type, TypeOffset.qualname, temp);

            long ptr = type.ToInt64(); // 64-bit safe

            temp = new IntPtr(ptr + TypeOffset.nb_add);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_number, temp);

            temp = new IntPtr(ptr + TypeOffset.sq_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_sequence, temp);

            temp = new IntPtr(ptr + TypeOffset.mp_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_mapping, temp);

            temp = new IntPtr(ptr + TypeOffset.bf_getbuffer);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_buffer, temp);
            return type;
             */
        }

        /// <summary>
        /// Utility method to allocate a type object &amp; do basic initialization.
        /// </summary>
        internal static IntPtr AllocateTypeObject(string name)
        {
            IntPtr type = Runtime.PyType_GenericAlloc(Runtime.PyTypeType, 0);

            // Cheat a little: we'll set tp_name to the internal char * of
            // the Python version of the type name - otherwise we'd have to
            // allocate the tp_name and would have no way to free it.
            IntPtr temp = Runtime.PyUnicode_FromString(name);
            IntPtr raw = Runtime.PyUnicode_AsUTF8(temp);
            Marshal.WriteIntPtr(type, TypeOffset.tp_name, raw);
            Marshal.WriteIntPtr(type, TypeOffset.name, temp);

            Marshal.WriteIntPtr(type, TypeOffset.qualname, temp);

            long ptr = type.ToInt64(); // 64-bit safe

            temp = new IntPtr(ptr + TypeOffset.nb_add);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_number, temp);

            temp = new IntPtr(ptr + TypeOffset.sq_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_sequence, temp);

            temp = new IntPtr(ptr + TypeOffset.mp_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_mapping, temp);

            temp = new IntPtr(ptr + TypeOffset.bf_getbuffer);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_buffer, temp);
            return type;
        }


        #region Native Code Page
        /// <summary>
        /// Initialized by InitializeNativeCodePage.
        ///
        /// This points to a page of memory allocated using mmap or VirtualAlloc
        /// (depending on the system), and marked read and execute (not write).
        /// Very much on purpose, the page is *not* released on a shutdown and
        /// is instead leaked. See the TestDomainReload test case.
        ///
        /// The contents of the page are two native functions: one that returns 0,
        /// one that returns 1.
        ///
        /// If python didn't keep its gc list through a Py_Finalize we could remove
        /// this entire section.
        /// </summary>
        internal static IntPtr NativeCodePage = IntPtr.Zero;

        /// <summary>
        /// Structure to describe native code.
        ///
        /// Use NativeCode.Active to get the native code for the current platform.
        ///
        /// Generate the code by creating the following C code:
        /// <code>
        /// int Return0() { return 0; }
        /// int Return1() { return 1; }
        /// </code>
        /// Then compiling on the target platform, e.g. with gcc or clang:
        /// <code>cc -c -fomit-frame-pointer -O2 foo.c</code>
        /// And then analyzing the resulting functions with a hex editor, e.g.:
        /// <code>objdump -disassemble foo.o</code>
        /// </summary>
        internal class NativeCode
        {
            /// <summary>
            /// The code, as a string of bytes.
            /// </summary>
            public byte[] Code { get; private set; }

            /// <summary>
            /// Where does the "return 0" function start?
            /// </summary>
            public int Return0 { get; private set; }

            /// <summary>
            /// Where does the "return 1" function start?
            /// </summary>
            public int Return1 { get; private set; }

            public static NativeCode Active
            {
                get
                {
                    switch (Runtime.Machine)
                    {
                        case MachineType.i386:
                            return I386;
                        case MachineType.x86_64:
                            return X86_64;
                        default:
                            return null;
                    }
                }
            }

            /// <summary>
            /// Code for x86_64. See the class comment for how it was generated.
            /// </summary>
            public static readonly NativeCode X86_64 = new NativeCode()
            {
                Return0 = 0x10,
                Return1 = 0,
                Code = new byte[]
                {
                    // First Return1:
                    0xb8, 0x01, 0x00, 0x00, 0x00, // movl $1, %eax
                    0xc3, // ret

                    // Now some padding so that Return0 can be 16-byte-aligned.
                    // I put Return1 first so there's not as much padding to type in.
                    0x66, 0x2e, 0x0f, 0x1f, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00, // nop

                    // Now Return0.
                    0x31, 0xc0, // xorl %eax, %eax
                    0xc3, // ret
                }
            };

            /// <summary>
            /// Code for X86.
            ///
            /// It's bitwise identical to X86_64, so we just point to it.
            /// <see cref="NativeCode.X86_64"/>
            /// </summary>
            public static readonly NativeCode I386 = X86_64;
        }

        /// <summary>
        /// Platform-dependent mmap and mprotect.
        /// </summary>
        internal interface IMemoryMapper
        {
            /// <summary>
            /// Map at least numBytes of memory. Mark the page read-write (but not exec).
            /// </summary>
            IntPtr MapWriteable(int numBytes);

            /// <summary>
            /// Sets the mapped memory to be read-exec (but not write).
            /// </summary>
            void SetReadExec(IntPtr mappedMemory, int numBytes);
        }

        class WindowsMemoryMapper : IMemoryMapper
        {
            const UInt32 MEM_COMMIT = 0x1000;
            const UInt32 MEM_RESERVE = 0x2000;
            const UInt32 PAGE_READWRITE = 0x04;
            const UInt32 PAGE_EXECUTE_READ = 0x20;

            [DllImport("kernel32.dll")]
            static extern IntPtr VirtualAlloc(IntPtr lpAddress, IntPtr dwSize, UInt32 flAllocationType, UInt32 flProtect);

            [DllImport("kernel32.dll")]
            static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, UInt32 flNewProtect, out UInt32 lpflOldProtect);

            public IntPtr MapWriteable(int numBytes)
            {
                return VirtualAlloc(IntPtr.Zero, new IntPtr(numBytes),
                                    MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
            }

            public void SetReadExec(IntPtr mappedMemory, int numBytes)
            {
                UInt32 _;
                VirtualProtect(mappedMemory, new IntPtr(numBytes), PAGE_EXECUTE_READ, out _);
            }
        }

        class UnixMemoryMapper : IMemoryMapper
        {
            const int PROT_READ = 0x1;
            const int PROT_WRITE = 0x2;
            const int PROT_EXEC = 0x4;

            const int MAP_PRIVATE = 0x2;
            int MAP_ANONYMOUS
            {
                get
                {
                    switch (Runtime.OperatingSystem)
                    {
                        case OperatingSystemType.Darwin:
                            return 0x1000;
                        case OperatingSystemType.Linux:
                            return 0x20;
                        default:
                            throw new NotImplementedException(
                                $"mmap is not supported on {Runtime.OperatingSystem}"
                            );
                    }
                }
            }

            [DllImport("libc")]
            static extern IntPtr mmap(IntPtr addr, IntPtr len, int prot, int flags, int fd, IntPtr offset);

            [DllImport("libc")]
            static extern int mprotect(IntPtr addr, IntPtr len, int prot);

            public IntPtr MapWriteable(int numBytes)
            {
                // MAP_PRIVATE must be set on linux, even though MAP_ANON implies it.
                // It doesn't hurt on darwin, so just do it.
                return mmap(IntPtr.Zero, new IntPtr(numBytes), PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, IntPtr.Zero);
            }

            public void SetReadExec(IntPtr mappedMemory, int numBytes)
            {
                mprotect(mappedMemory, new IntPtr(numBytes), PROT_READ | PROT_EXEC);
            }
        }

        internal static IMemoryMapper CreateMemoryMapper()
        {
            switch (Runtime.OperatingSystem)
            {
                case OperatingSystemType.Darwin:
                case OperatingSystemType.Linux:
                    return new UnixMemoryMapper();
                case OperatingSystemType.Windows:
                    return new WindowsMemoryMapper();
                default:
                    throw new NotImplementedException(
                        $"No support for {Runtime.OperatingSystem}"
                    );
            }
        }

        /// <summary>
        /// Initializes the native code page.
        ///
        /// Safe to call if we already initialized (this function is idempotent).
        /// <see cref="NativeCodePage"/>
        /// </summary>
        internal static void InitializeNativeCodePage()
        {
            // Do nothing if we already initialized.
            if (NativeCodePage != IntPtr.Zero)
            {
                return;
            }

            // Allocate the page, write the native code into it, then set it
            // to be executable.
            IMemoryMapper mapper = CreateMemoryMapper();
            int codeLength = NativeCode.Active.Code.Length;
            NativeCodePage = mapper.MapWriteable(codeLength);
            Marshal.Copy(NativeCode.Active.Code, 0, NativeCodePage, codeLength);
            mapper.SetReadExec(NativeCodePage, codeLength);
        }
        #endregion

        /// <summary>
        /// Given a managed Type that provides the implementation for the type,
        /// create a PY_TYPE_SLOT array to be used for PyType_FromSpec.
        /// </summary>
        internal static PY_TYPE_SLOT[] CreateSlotArray(Type impl)
        {
            // We work from the most-derived class up; make sure to get
            // the most-derived slot and not to override it with a base
            // class's slot.
            var seen = new HashSet<string>();
            var typeslots = new List<PY_TYPE_SLOT>();

            while (impl != null)
            {
                MethodInfo[] methods = impl.GetMethods(tbFlags);
                foreach (MethodInfo method in methods)
                {
                    string name = method.Name;
                    if (!(name.StartsWith("tp_") ||
                          name.StartsWith("nb_") ||
                          name.StartsWith("sq_") ||
                          name.StartsWith("mp_") ||
                          name.StartsWith("bf_")
                    ))
                    {
                        continue;
                    }

                    if (seen.Contains(name))
                    {
                        continue;
                    }

                    typeslots.Add(InitializeSlot(getSlotNumber(name), method));
                    seen.Add(name);
                }

                impl = impl.BaseType;
            }

            var native = NativeCode.Active;

            // The garbage collection related slots always have to return 1 or 0
            // since .NET objects don't take part in Python's gc:
            //   tp_traverse (returns 0)
            //   tp_clear    (returns 0)
            //   tp_is_gc    (returns 1)
            // These have to be defined, though, so by default we fill these with
            // static C# functions from this class.

            var ret0 = Interop.GetThunk(((Func<IntPtr, int>)Return0).Method).Address;
            var ret1 = Interop.GetThunk(((Func<IntPtr, int>)Return1).Method).Address;

            if (native != null)
            {
                // If we want to support domain reload, the C# implementation
                // cannot be used as the assembly may get released before
                // CPython calls these functions. Instead, for amd64 and x86 we
                // load them into a separate code page that is leaked
                // intentionally.
                InitializeNativeCodePage();
                ret1 = NativeCodePage + native.Return1;
                ret0 = NativeCodePage + native.Return0;
            }

            typeslots.Add(InitializeSlot(getSlotNumber("tp_traverse"), ret0));
            typeslots.Add(InitializeSlot(getSlotNumber("tp_clear"), ret0));
            typeslots.Add(InitializeSlot(getSlotNumber("tp_is_gc"), ret1));

            typeslots.Add(new PY_TYPE_SLOT { slot = 0, func = IntPtr.Zero });
            return typeslots.ToArray();
        }

        /// <summary>
        /// Given a newly allocated Python type object and a managed Type that
        /// provides the implementation for the type, connect the type slots of
        /// the Python object to the managed methods of the implementing Type.
        /// </summary>
        internal static void InitializeSlots(IntPtr type, Type impl)
        {
            // We work from the most-derived class up; make sure to get
            // the most-derived slot and not to override it with a base
            // class's slot.
            var seen = new HashSet<string>();

            while (impl != null)
            {
                MethodInfo[] methods = impl.GetMethods(tbFlags);
                foreach (MethodInfo method in methods)
                {
                    string name = method.Name;
                    if (!(name.StartsWith("tp_") ||
                          name.StartsWith("nb_") ||
                          name.StartsWith("sq_") ||
                          name.StartsWith("mp_") ||
                          name.StartsWith("bf_")
                    ))
                    {
                        continue;
                    }

                    if (seen.Contains(name))
                    {
                        continue;
                    }

                    var thunkInfo = Interop.GetThunk(method);
                    InitializeSlot(type, thunkInfo.Address, name);

                    seen.Add(name);
                }

                impl = impl.BaseType;
            }

            var native = NativeCode.Active;

            // The garbage collection related slots always have to return 1 or 0
            // since .NET objects don't take part in Python's gc:
            //   tp_traverse (returns 0)
            //   tp_clear    (returns 0)
            //   tp_is_gc    (returns 1)
            // These have to be defined, though, so by default we fill these with
            // static C# functions from this class.

            var ret0 = Interop.GetThunk(((Func<IntPtr, int>)Return0).Method).Address;
            var ret1 = Interop.GetThunk(((Func<IntPtr, int>)Return1).Method).Address;

            if (native != null)
            {
                // If we want to support domain reload, the C# implementation
                // cannot be used as the assembly may get released before
                // CPython calls these functions. Instead, for amd64 and x86 we
                // load them into a separate code page that is leaked
                // intentionally.
                InitializeNativeCodePage();
                ret1 = NativeCodePage + native.Return1;
                ret0 = NativeCodePage + native.Return0;
            }

            InitializeSlot(type, ret0, "tp_traverse");
            InitializeSlot(type, ret0, "tp_clear");
            InitializeSlot(type, ret1, "tp_is_gc");
        }

        static int Return1(IntPtr _) => 1;

        static int Return0(IntPtr _) => 0;

        /// <summary>
        /// Helper for InitializeSlots.
        ///
        /// Initializes one slot to point to a function pointer.
        /// The function pointer might be a thunk for C#, or it may be
        /// an address in the NativeCodePage.
        /// </summary>
        /// <param name="type">Type being initialized.</param>
        /// <param name="slot">Function pointer.</param>
        /// <param name="name">Name of the method.</param>
        static void InitializeSlot(IntPtr type, IntPtr slot, string name)
        {
            Type typeOffset = typeof(TypeOffset);
            FieldInfo fi = typeOffset.GetField(name);
            var offset = (int)fi.GetValue(typeOffset);

            Marshal.WriteIntPtr(type, offset, slot);
        }

        /// <summary>
        /// Given a newly allocated Python type object and a managed Type that
        /// implements it, initialize any methods defined by the Type that need
        /// to appear in the Python type __dict__ (based on custom attribute).
        /// </summary>
        private static void InitMethods(IntPtr pytype, Type type)
        {
            IntPtr dict = Marshal.ReadIntPtr(pytype, TypeOffset.tp_dict);
            Type marker = typeof(PythonMethodAttribute);

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var addedMethods = new HashSet<string>();

            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(flags);
                foreach (MethodInfo method in methods)
                {
                    if (!addedMethods.Contains(method.Name))
                    {
                        object[] attrs = method.GetCustomAttributes(marker, false);
                        if (attrs.Length > 0)
                        {
                            string method_name = method.Name;
                            var mi = new MethodInfo[1];
                            mi[0] = method;
                            MethodObject m = new TypeMethod(type, method_name, mi);
                            Runtime.PyDict_SetItemString(dict, method_name, m.pyHandle);
                            addedMethods.Add(method_name);
                        }
                    }
                }
                type = type.BaseType;
            }
        }


        /// <summary>
        /// Utility method to copy slots from a given type to another type.
        /// </summary>
        internal static void CopySlot(IntPtr from, IntPtr to, int offset)
        {
            IntPtr fp = Marshal.ReadIntPtr(from, offset);
            Marshal.WriteIntPtr(to, offset, fp);
        }
    }
}
