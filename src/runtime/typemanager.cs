using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const BindingFlags tbFlags = BindingFlags.Public | BindingFlags.Static;
        private static readonly Dictionary<Type, IntPtr> cache = new Dictionary<Type, IntPtr>();
        private static readonly Dictionary<IntPtr, SlotsHolder> _slotsHolders = new Dictionary<IntPtr, SlotsHolder>();

        static TypeManager()
        {

        }

        public static void Reset()
        {
            cache.Clear();
        }

        internal static void RemoveTypes()
        {
            foreach (var tpHandle in cache.Values)
            {
                SlotsHolder holder;
                if (_slotsHolders.TryGetValue(tpHandle, out holder))
                {
                    // If refcount > 1, it needs to reset the managed slot,
                    // otherwise it can dealloc without any trick.
                    if (Runtime.Refcount(tpHandle) > 1)
                    {
                        holder.ResetSlots();
                    }
                }
                Runtime.XDecref(tpHandle);
            }
            cache.Clear();
            _slotsHolders.Clear();
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
        /// Return value: Borrowed reference.
        internal static IntPtr CreateType(Type impl)
        {
            IntPtr type = AllocateTypeObject(impl.Name);
            int ob_size = ObjectOffset.Size(type);

            // Set tp_basicsize to the size of our managed instance objects.
            Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);

            var offset = (IntPtr)ObjectOffset.DictOffset(type);
            Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, offset);

            SlotsHolder slotsHolder = CreateSlotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            int flags = TypeFlags.Default | TypeFlags.Managed |
                        TypeFlags.HeapType | TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }
            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(dict, "__module__", mod);
            Runtime.XDecref(mod);

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
            int tp_dictoffset = ObjectOffset.DictOffset(Runtime.PyTypeType);

            // XXX Hack, use a different base class for System.Exception
            // Python 2.5+ allows new style class exceptions but they *must*
            // subclass BaseException (or better Exception).
            if (typeof(Exception).IsAssignableFrom(clrType))
            {
                ob_size = ObjectOffset.Size(Exceptions.Exception);
                tp_dictoffset = ObjectOffset.DictOffset(Exceptions.Exception);
            }

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

            // we want to do this after the slot stuff above in case the class itself implements a slot method
            SlotsHolder slotsHolder = CreateSlotsHolder(type);
            InitializeSlots(type, impl.GetType(), slotsHolder);

            // add a __len__ slot for inheritors of ICollection and ICollection<>
            if (typeof(ICollection).IsAssignableFrom(clrType) || clrType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                var method = typeof(mp_length_slot).GetMethod(nameof(mp_length_slot.mp_length));
                var thunk = Interop.GetThunk(method);
                InitializeSlot(type, thunk, "__len__", slotsHolder);
            }

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

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            string mn = clrType.Namespace ?? "";
            IntPtr mod = Runtime.PyString_FromString(mn);
            Runtime.PyDict_SetItemString(dict, "__module__", mod);
            Runtime.XDecref(mod);

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

        internal static void FreeMethodDef(IntPtr mdef)
        {
            unsafe
            {
                var def = (PyMethodDef*)mdef;
                if (def->ml_name != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(def->ml_name);
                    def->ml_name = IntPtr.Zero;
                }
                if (def->ml_doc != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(def->ml_doc);
                    def->ml_doc = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Adds a deallocator for a type's method. At deallocation, the deallocator will remove the
        /// method from the type's Dict and deallocate the PyMethodDef object.
        /// </summary>
        /// <param name="t">The type to add the deallocator to.</param>
        /// <param name="mdef">The pointer to the PyMethodDef structure.</param>
        /// <param name="name">The name of the slot.</param>
        /// <param name="slotsHolder">The SlotsHolder holding the deallocator/.</param>
        internal static void AddDeallocator(IntPtr t, IntPtr mdef, string name,  SlotsHolder slotsHolder)
        {
            slotsHolder.AddDealloctor(() =>
            {
                //IntPtr t = type;
                IntPtr tp_dict = Marshal.ReadIntPtr(t, TypeOffset.tp_dict);
                if (Runtime.PyDict_DelItemString(tp_dict, name) != 0)
                {
                    Runtime.PyErr_Print();
                }   
                FreeMethodDef(mdef);
            });
        }

        internal static IntPtr CreateMetaType(Type impl, out SlotsHolder slotsHolder)
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
            slotsHolder = new SlotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            int flags = TypeFlags.Default;
            flags |= TypeFlags.Managed;
            flags |= TypeFlags.HeapType;
            flags |= TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            // We need space for 3 PyMethodDef structs, each of them
            // 4 int-ptrs in size.
            IntPtr mdef = Runtime.PyMem_Malloc(3 * 4 * IntPtr.Size);
            Debug.Assert(4 * IntPtr.Size == Marshal.SizeOf(typeof(PyMethodDef)));
            IntPtr mdefStart = mdef;
            ThunkInfo thunk = Interop.GetThunk(typeof(MetaType).GetMethod("__instancecheck__"), "BinaryFunc");
            slotsHolder.KeepAlive(thunk.Target);
            // Add deallocator before writing the method def, as after WriteMethodDef, mdef
            // will not have the same value.
            AddDeallocator(type, mdef, "__instancecheck__", slotsHolder);
            mdef = WriteMethodDef(
                mdef,
                "__instancecheck__",
                thunk.Address
            );

            thunk = Interop.GetThunk(typeof(MetaType).GetMethod("__subclasscheck__"), "BinaryFunc");
            slotsHolder.KeepAlive(thunk.Target);
            AddDeallocator(type, mdef, "__subclasscheck__", slotsHolder);

            mdef = WriteMethodDef(
                mdef,
                "__subclasscheck__",
                thunk.Address
            );

            // Pad the last field with zeroes to terminate the array
            mdef = WriteMethodDefSentinel(mdef);

            Marshal.WriteIntPtr(type, TypeOffset.tp_methods, mdefStart);
            slotsHolder.Set(TypeOffset.tp_methods, (t, offset) =>
            {
                var p = Marshal.ReadIntPtr(t, offset);
                Runtime.PyMem_Free(p);
                Marshal.WriteIntPtr(t, offset, IntPtr.Zero);
            });

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

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

            SlotsHolder slotsHolder = CreateSlotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            IntPtr tp_dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(tp_dict, "__module__", mod);

            return type;
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
#if PYTHON3
            IntPtr temp = Runtime.PyUnicode_FromString(name);
            IntPtr raw = Runtime.PyUnicode_AsUTF8(temp);
#elif PYTHON2
            IntPtr temp = Runtime.PyString_FromString(name);
            IntPtr raw = Runtime.PyString_AsString(temp);
#endif
            Marshal.WriteIntPtr(type, TypeOffset.tp_name, raw);
            Marshal.WriteIntPtr(type, TypeOffset.name, temp);

#if PYTHON3
            Marshal.WriteIntPtr(type, TypeOffset.qualname, temp);
#endif

            long ptr = type.ToInt64(); // 64-bit safe

            temp = new IntPtr(ptr + TypeOffset.nb_add);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_number, temp);

            temp = new IntPtr(ptr + TypeOffset.sq_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_sequence, temp);

            temp = new IntPtr(ptr + TypeOffset.mp_length);
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_mapping, temp);

#if PYTHON3
            temp = new IntPtr(ptr + TypeOffset.bf_getbuffer);
#elif PYTHON2
            temp = new IntPtr(ptr + TypeOffset.bf_getreadbuffer);
#endif
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
                            throw new NotImplementedException($"mmap is not supported on {Runtime.OperatingSystemName}");
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
                    throw new NotImplementedException($"No support for {Runtime.OperatingSystemName}");
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
        /// Given a newly allocated Python type object and a managed Type that
        /// provides the implementation for the type, connect the type slots of
        /// the Python object to the managed methods of the implementing Type.
        /// </summary>
        internal static void InitializeSlots(IntPtr type, Type impl, SlotsHolder slotsHolder = null)
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

            IntPtr ret0 = IntPtr.Zero;
            IntPtr ret1 = IntPtr.Zero;
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
            else
            {
                ret1 = Interop.GetThunk(((Func<IntPtr, int>)Return1).Method).Address;
                ret0 = Interop.GetThunk(((Func<IntPtr, int>)Return0).Method).Address;
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
        /// <param name="name">Name of the slot to initialize</param>
        static void InitializeSlot(IntPtr type, IntPtr slot, string name)
        {
            var offset = GetSlotOffset(name);
            if (Marshal.ReadIntPtr(type, offset) != IntPtr.Zero)
            {
                return;
            }
            Marshal.WriteIntPtr(type, offset, slot);
        }

        static void InitializeSlot(IntPtr type, ThunkInfo thunk, string name, SlotsHolder slotsHolder = null, bool canOverride = true)
        {
            Type typeOffset = typeof(TypeOffset);
            FieldInfo fi = typeOffset.GetField(name);
            var offset = (int)fi.GetValue(typeOffset);

            if (!canOverride && Marshal.ReadIntPtr(type, offset) != IntPtr.Zero)
            {
                return;
            }
            Marshal.WriteIntPtr(type, offset, thunk.Address);
            if (slotsHolder != null)
            {
                slotsHolder.Add(offset, thunk);
            }
        }

        static int GetSlotOffset(string name)
        {
            Type typeOffset = typeof(TypeOffset);
            FieldInfo fi = typeOffset.GetField(name);
            var offset = (int)fi.GetValue(typeOffset);
            return offset;
        }

        static bool IsSlotSet(IntPtr type, string name)
        {
            int offset = GetSlotOffset(name);
            return Marshal.ReadIntPtr(type, offset) != IntPtr.Zero;
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

        private static SlotsHolder CreateSlotsHolder(IntPtr type)
        {
            var holder = new SlotsHolder(type);
            _slotsHolders.Add(type, holder);
            return holder;
        }
    }

    class SlotsHolder
    {
        /// <summary>
        /// Delegate called to customize a (Python) Type slot reset
        /// </summary>
        /// <param name="type">The type that will have a slot reset</param>
        /// <param name="offset">The offset of the slot</param>
        public delegate void ResetSlotAction(IntPtr type, int offset);

        private readonly IntPtr type;
        private Dictionary<int, ThunkInfo> slots = new Dictionary<int, ThunkInfo>();
        private List<Delegate> keepalive = new List<Delegate>();
        private Dictionary<int, ResetSlotAction> customResetors = new Dictionary<int, ResetSlotAction>();
        private List<Action> deallocators = new List<Action>();
        private bool alreadyReset = false;

        /// <summary>
        /// Create slots holder for holding the delegate of slots and be able  to reset them.
        /// </summary>
        /// <param name="type">Steals a reference to target type</param>
        public SlotsHolder(IntPtr type)
        {
            this.type = type;
        }

        public void Add(int offset, ThunkInfo thunk)
        {
            slots.Add(offset, thunk);
        }

        public void Set(int offset, ResetSlotAction resetor)
        {
            customResetors[offset] = resetor;
        }

        public void AddDealloctor(Action deallocate)
        {
            deallocators.Add(deallocate);
        }

        /// <summary>
        /// Add a delegate to keep it from being garbage collected.
        /// </summary>
        /// <param name="d">The delegate to add</param>
        public void KeepAlive(Delegate d)
        {
            keepalive.Add(d);
        }

        public void ResetSlots()
        {
            if (alreadyReset)
            {
                return;
            }
            alreadyReset = true;
            foreach (var offset in slots.Keys)
            {
                IntPtr ptr = GetDefaultSlot(offset);
                //DebugUtil.Print($"Set slot<{TypeOffsetHelper.GetSlotNameByOffset(offset)}> to 0x{ptr.ToString("X")} at {typeName}<0x{_type}>");
                Marshal.WriteIntPtr(type, offset, ptr);
            }

            foreach (var action in deallocators)
            {
                action();
            }

            foreach (var pair in customResetors)
            {
                int offset = pair.Key;
                var resetor = pair.Value;
                resetor?.Invoke(type, offset);
            }

            customResetors.Clear();
            slots.Clear();
            keepalive.Clear();
            deallocators.Clear();

            // Custom reset
            IntPtr tp_base = Marshal.ReadIntPtr(type, TypeOffset.tp_base);
            Runtime.XDecref(tp_base);
            Marshal.WriteIntPtr(type, TypeOffset.tp_base, IntPtr.Zero);

            IntPtr tp_bases = Marshal.ReadIntPtr(type, TypeOffset.tp_bases);
            Runtime.XDecref(tp_bases);
            tp_bases = Runtime.PyTuple_New(0);
            Marshal.WriteIntPtr(type, TypeOffset.tp_bases, tp_bases);
        }

        /// <summary>
        /// Returns the default C function pointer for the slot to reset.
        /// </summary>
        /// <param name="offset">The offset of the slot.</param>
        /// <returns>The default C function pointer of the slot.</returns>
        private static IntPtr GetDefaultSlot(int offset)
        {
            if (offset == TypeOffset.tp_clear
                || offset == TypeOffset.tp_traverse)
            {
                return TypeManager.NativeCodePage + TypeManager.NativeCode.Active.Return0;
            }
            else if (offset == TypeOffset.tp_dealloc)
            {
                // tp_free of PyTypeType is point to PyObject_GC_Del.
                return Marshal.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_free);
            }
            else if (offset == TypeOffset.tp_free)
            {
                // PyObject_GC_Del
                return Marshal.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_free);
            }
            else if (offset == TypeOffset.tp_call)
            {
                return IntPtr.Zero;
            }
            else if (offset == TypeOffset.tp_new)
            {
                // PyType_GenericNew
                return Marshal.ReadIntPtr(Runtime.PySuper_Type, TypeOffset.tp_new);
            }
            else if (offset == TypeOffset.tp_getattro)
            {
                // PyObject_GenericGetAttr
                return Marshal.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_getattro);
            }
            else if (offset == TypeOffset.tp_setattro)
            {
                // PyObject_GenericSetAttr
                return Marshal.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_setattro);
            }

            return Marshal.ReadIntPtr(Runtime.PyTypeType, offset);
        }
    }


    static class SlotHelper
    {
        public static IntPtr CreateObjectType()
        {
            IntPtr globals = Runtime.PyDict_New();
            if (Runtime.PyDict_SetItemString(globals, "__builtins__", Runtime.PyEval_GetBuiltins()) != 0)
            {
                Runtime.XDecref(globals);
                throw new PythonException();
            }
            const string code = "class A(object): pass";
            IntPtr res = Runtime.PyRun_String(code, (IntPtr)RunFlagType.File, globals, globals);
            if (res == IntPtr.Zero)
            {
                try
                {
                    throw new PythonException();
                }
                finally
                {
                    Runtime.XDecref(globals);
                }
            }
            Runtime.XDecref(res);
            IntPtr A = Runtime.PyDict_GetItemString(globals, "A");
            Debug.Assert(A != IntPtr.Zero);
            Runtime.XIncref(A);
            Runtime.XDecref(globals);
            return A;
        }
    }
}
