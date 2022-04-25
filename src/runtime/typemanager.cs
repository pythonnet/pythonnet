using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Python.Runtime.Slots;
using static Python.Runtime.PythonException;

namespace Python.Runtime
{

    /// <summary>
    /// The TypeManager class is responsible for building binary-compatible
    /// Python type objects that are implemented in managed code.
    /// </summary>
    internal class TypeManager
    {
        internal static IntPtr subtype_traverse;
        internal static IntPtr subtype_clear;

        private const BindingFlags tbFlags = BindingFlags.Public | BindingFlags.Static;
        private static Dictionary<Type, IntPtr> cache = new Dictionary<Type, IntPtr>();

        private static readonly Dictionary<IntPtr, SlotsHolder> _slotsHolders = new Dictionary<IntPtr, SlotsHolder>();
        private static Dictionary<MaybeType, Type> _slotsImpls = new Dictionary<MaybeType, Type>();

        // Slots which must be set
        private static readonly string[] _requiredSlots = new string[]
        {
            "tp_traverse",
            "tp_clear",
        };

        internal static void Initialize()
        {
            Debug.Assert(cache.Count == 0, "Cache should be empty",
                "Some errors may occurred on last shutdown");
            IntPtr type = SlotHelper.CreateObjectType();
            subtype_traverse = Marshal.ReadIntPtr(type, TypeOffset.tp_traverse);
            subtype_clear = Marshal.ReadIntPtr(type, TypeOffset.tp_clear);
            Runtime.XDecref(type);
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
            _slotsImpls.Clear();
            _slotsHolders.Clear();
        }

        internal static void SaveRuntimeData(RuntimeDataStorage storage)
        {
            foreach (var tpHandle in cache.Values)
            {
                Runtime.XIncref(tpHandle);
            }
            storage.AddValue("cache", cache);
            storage.AddValue("slots", _slotsImpls);
        }

        internal static void RestoreRuntimeData(RuntimeDataStorage storage)
        {
            Debug.Assert(cache == null || cache.Count == 0);
            storage.GetValue("slots", out _slotsImpls);
            storage.GetValue<Dictionary<MaybeType, IntPtr>>("cache", out var _cache);
            foreach (var entry in _cache)
            {
                if (!entry.Key.Valid)
                {
                    Runtime.XDecref(entry.Value);
                    continue;
                }
                Type type = entry.Key.Value;;
                IntPtr handle = entry.Value;
                cache[type] = handle;
                SlotsHolder holder = CreateSolotsHolder(handle);
                InitializeSlots(handle, _slotsImpls[type], holder);
                // FIXME: mp_length_slot.CanAssgin(clrType)
            }
        }

        /// <summary>
        /// Return value: Borrowed reference.
        /// Given a managed Type derived from ExtensionType, get the handle to
        /// a Python type object that delegates its implementation to the Type
        /// object. These Python type instances are used to implement internal
        /// descriptor and utility types like ModuleObject, PropertyObject, etc.
        /// </summary>
        [Obsolete]
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
            _slotsImpls.Add(type, type);
            return handle;
        }
        /// <summary>
        /// Given a managed Type derived from ExtensionType, get the handle to
        /// a Python type object that delegates its implementation to the Type
        /// object. These Python type instances are used to implement internal
        /// descriptor and utility types like ModuleObject, PropertyObject, etc.
        /// </summary>
        internal static BorrowedReference GetTypeReference(Type type)
            => new BorrowedReference(GetTypeHandle(type));


        /// <summary>
        /// Return value: Borrowed reference.
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
            _slotsImpls.Add(type, obj.GetType());
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
            IntPtr type = AllocateTypeObject(impl.Name, metatype: Runtime.PyTypeType);
            int ob_size = ObjectOffset.Size(type);

            // Set tp_basicsize to the size of our managed instance objects.
            Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);

            var offset = (IntPtr)ObjectOffset.TypeDictOffset(type);
            Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, offset);

            SlotsHolder slotsHolder = CreateSolotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            int flags = TypeFlags.Default | TypeFlags.Managed |
                        TypeFlags.HeapType | TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            var dict = new BorrowedReference(Marshal.ReadIntPtr(type, TypeOffset.tp_dict));
            var mod = NewReference.DangerousFromPointer(Runtime.PyString_FromString("CLR"));
            Runtime.PyDict_SetItem(dict, PyIdentifier.__module__, mod);
            mod.Dispose();

            InitMethods(type, impl);

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(type);
            return type;
        }


        internal static IntPtr CreateType(ManagedType impl, Type clrType)
        {
            // Cleanup the type name to get rid of funny nested type names.
            string name = $"clr.{clrType.FullName}";
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

            IntPtr type = AllocateTypeObject(name, Runtime.PyCLRMetaType);

            Marshal.WriteIntPtr(type, TypeOffset.ob_type, Runtime.PyCLRMetaType);
            Runtime.XIncref(Runtime.PyCLRMetaType);

            Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);
            Marshal.WriteIntPtr(type, TypeOffset.tp_itemsize, IntPtr.Zero);
            Marshal.WriteIntPtr(type, TypeOffset.tp_dictoffset, (IntPtr)tp_dictoffset);

            // we want to do this after the slot stuff above in case the class itself implements a slot method
            SlotsHolder slotsHolder = CreateSolotsHolder(type);
            InitializeSlots(type, impl.GetType(), slotsHolder);

            if (Marshal.ReadIntPtr(type, TypeOffset.mp_length) == IntPtr.Zero
                && mp_length_slot.CanAssign(clrType))
            {
                InitializeSlot(type, TypeOffset.mp_length, mp_length_slot.Method, slotsHolder);
            }

            // we want to do this after the slot stuff above in case the class itself implements a slot method
            InitializeSlots(type, impl.GetType());

            if (!clrType.GetInterfaces().Any(ifc => ifc == typeof(IEnumerable) || ifc == typeof(IEnumerator)))
            {
                // The tp_iter slot should only be set for enumerable types.
                Marshal.WriteIntPtr(type, TypeOffset.tp_iter, IntPtr.Zero);
            }


            // Only set mp_subscript and mp_ass_subscript for types with indexers
            if (impl is ClassBase cb)
            {
                if (!(impl is ArrayObject))
                {
                    if (cb.indexer == null || !cb.indexer.CanGet)
                    {
                        Marshal.WriteIntPtr(type, TypeOffset.mp_subscript, IntPtr.Zero);
                    }
                    if (cb.indexer == null || !cb.indexer.CanSet)
                    {
                        Marshal.WriteIntPtr(type, TypeOffset.mp_ass_subscript, IntPtr.Zero);
                    }
                }
            }
            else
            {
                Marshal.WriteIntPtr(type, TypeOffset.mp_subscript, IntPtr.Zero);
                Marshal.WriteIntPtr(type, TypeOffset.mp_ass_subscript, IntPtr.Zero);
            }

            if (base_ != IntPtr.Zero)
            {
                Marshal.WriteIntPtr(type, TypeOffset.tp_base, base_);
                Runtime.XIncref(base_);
            }

            const int flags = TypeFlags.Default
                            | TypeFlags.Managed
                            | TypeFlags.HeapType
                            | TypeFlags.BaseType
                            | TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            OperatorMethod.FixupSlots(type, clrType);
            // Leverage followup initialization from the Python runtime. Note
            // that the type of the new type must PyType_Type at the time we
            // call this, else PyType_Ready will skip some slot initialization.

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            var dict = new BorrowedReference(Marshal.ReadIntPtr(type, TypeOffset.tp_dict));
            string mn = clrType.Namespace ?? "";
            var mod = NewReference.DangerousFromPointer(Runtime.PyString_FromString(mn));
            Runtime.PyDict_SetItem(dict, PyIdentifier.__module__, mod);
            mod.Dispose();

            // Hide the gchandle of the implementation in a magic type slot.
            GCHandle gc = impl.AllocGCHandle();
            Marshal.WriteIntPtr(type, TypeOffset.magic(), (IntPtr)gc);

            // Set the handle attributes on the implementing instance.
            impl.tpHandle = type;
            impl.pyHandle = type;

            //DebugUtil.DumpType(type);

            return type;
        }

        internal static IntPtr CreateSubType(IntPtr py_name, IntPtr py_base_type, IntPtr py_dict)
        {
            var dictRef = new BorrowedReference(py_dict);
            // Utility to create a subtype of a managed type with the ability for the
            // a python subtype able to override the managed implementation
            string name = Runtime.GetManagedString(py_name);

            // the derived class can have class attributes __assembly__ and __module__ which
            // control the name of the assembly and module the new type is created in.
            object assembly = null;
            object namespaceStr = null;

            using (var assemblyKey = new PyString("__assembly__"))
            {
                var assemblyPtr = Runtime.PyDict_GetItemWithError(dictRef, assemblyKey.Reference);
                if (assemblyPtr.IsNull)
                {
                    if (Exceptions.ErrorOccurred()) return IntPtr.Zero;
                }
                else if (!Converter.ToManagedValue(assemblyPtr, typeof(string), out assembly, true))
                {
                    return Exceptions.RaiseTypeError("Couldn't convert __assembly__ value to string");
                }

                using (var namespaceKey = new PyString("__namespace__"))
                {
                    var pyNamespace = Runtime.PyDict_GetItemWithError(dictRef, namespaceKey.Reference);
                    if (pyNamespace.IsNull)
                    {
                        if (Exceptions.ErrorOccurred()) return IntPtr.Zero;
                    }
                    else if (!Converter.ToManagedValue(pyNamespace, typeof(string), out namespaceStr, true))
                    {
                        return Exceptions.RaiseTypeError("Couldn't convert __namespace__ value to string");
                    }
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
                    baseClass.type.Value,
                    py_dict,
                    (string)namespaceStr,
                    (string)assembly);

                // create the new ManagedType and python type
                ClassBase subClass = ClassManager.GetClass(subType);
                IntPtr py_type = GetTypeHandle(subClass, subType);

                // by default the class dict will have all the C# methods in it, but as this is a
                // derived class we want the python overrides in there instead if they exist.
                var cls_dict = new BorrowedReference(Marshal.ReadIntPtr(py_type, TypeOffset.tp_dict));
                ThrowIfIsNotZero(Runtime.PyDict_Update(cls_dict, new BorrowedReference(py_dict)));
                Runtime.XIncref(py_type);
                // Update the __classcell__ if it exists
                BorrowedReference cell = Runtime.PyDict_GetItemString(cls_dict, "__classcell__");
                if (!cell.IsNull)
                {
                    ThrowIfIsNotZero(Runtime.PyCell_Set(cell, py_type));
                    ThrowIfIsNotZero(Runtime.PyDict_DelItemString(cls_dict, "__classcell__"));
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

        internal static IntPtr CreateMetaType(Type impl, out SlotsHolder slotsHolder)
        {
            // The managed metatype is functionally little different than the
            // standard Python metatype (PyType_Type). It overrides certain of
            // the standard type slots, and has to subclass PyType_Type for
            // certain functions in the C runtime to work correctly with it.

            IntPtr type = AllocateTypeObject("CLR Metatype", metatype: Runtime.PyTypeType);

            IntPtr py_type = Runtime.PyTypeType;
            Marshal.WriteIntPtr(type, TypeOffset.tp_base, py_type);
            Runtime.XIncref(py_type);

            int size = TypeOffset.magic() + IntPtr.Size;
            Marshal.WriteIntPtr(type, TypeOffset.tp_basicsize, new IntPtr(size));

            const int flags = TypeFlags.Default
                            | TypeFlags.Managed
                            | TypeFlags.HeapType
                            | TypeFlags.HaveGC;
            Util.WriteCLong(type, TypeOffset.tp_flags, flags);

            // Slots will inherit from TypeType, it's not neccesary for setting them.
            // Inheried slots:
            // tp_basicsize, tp_itemsize,
            // tp_dictoffset, tp_weaklistoffset,
            // tp_traverse, tp_clear, tp_is_gc, etc.
            slotsHolder = SetupMetaSlots(impl, type);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            IntPtr dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItemString(dict, "__module__", mod);

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(type);
            //DebugUtil.DumpType(type);

            return type;
        }

        internal static SlotsHolder SetupMetaSlots(Type impl, IntPtr type)
        {
            // Override type slots with those of the managed implementation.
            SlotsHolder slotsHolder = new SlotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            // We need space for 3 PyMethodDef structs.
            int mdefSize = (MetaType.CustomMethods.Length + 1) * Marshal.SizeOf(typeof(PyMethodDef));
            IntPtr mdef = Runtime.PyMem_Malloc(mdefSize);
            IntPtr mdefStart = mdef;
            foreach (var methodName in MetaType.CustomMethods)
            {
                mdef = AddCustomMetaMethod(methodName, type, mdef, slotsHolder);
            }
            mdef = WriteMethodDefSentinel(mdef);
            Debug.Assert((long)(mdefStart + mdefSize) <= (long)mdef);

            Marshal.WriteIntPtr(type, TypeOffset.tp_methods, mdefStart);

            // XXX: Hard code with mode check.
            if (Runtime.ShutdownMode != ShutdownMode.Reload)
            {
                slotsHolder.Set(TypeOffset.tp_methods, (t, offset) =>
                {
                    var p = Marshal.ReadIntPtr(t, offset);
                    Runtime.PyMem_Free(p);
                    Marshal.WriteIntPtr(t, offset, IntPtr.Zero);
                });
            }
            return slotsHolder;
        }

        private static IntPtr AddCustomMetaMethod(string name, IntPtr type, IntPtr mdef, SlotsHolder slotsHolder)
        {
            MethodInfo mi = typeof(MetaType).GetMethod(name);
            ThunkInfo thunkInfo = Interop.GetThunk(mi, "BinaryFunc");
            slotsHolder.KeeapAlive(thunkInfo);

            // XXX: Hard code with mode check.
            if (Runtime.ShutdownMode != ShutdownMode.Reload)
            {
                IntPtr mdefAddr = mdef;
                slotsHolder.AddDealloctor(() =>
                {
                    var tp_dict = new BorrowedReference(Marshal.ReadIntPtr(type, TypeOffset.tp_dict));
                    if (Runtime.PyDict_DelItemString(tp_dict, name) != 0)
                    {
                        Runtime.PyErr_Print();
                        Debug.Fail($"Cannot remove {name} from metatype");
                    }
                    FreeMethodDef(mdefAddr);
                });
            }
            mdef = WriteMethodDef(mdef, name, thunkInfo.Address);
            return mdef;
        }

        internal static IntPtr BasicSubType(string name, IntPtr base_, Type impl)
        {
            // Utility to create a subtype of a std Python type, but with
            // a managed type able to override implementation

            IntPtr type = AllocateTypeObject(name, metatype: Runtime.PyTypeType);
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

            SlotsHolder slotsHolder = CreateSolotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw new PythonException();
            }

            IntPtr tp_dict = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            IntPtr mod = Runtime.PyString_FromString("CLR");
            Runtime.PyDict_SetItem(tp_dict, PyIdentifier.__module__, mod);

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(type);

            return type;
        }


        /// <summary>
        /// Utility method to allocate a type object &amp; do basic initialization.
        /// </summary>
        internal static IntPtr AllocateTypeObject(string name, IntPtr metatype)
        {
            IntPtr type = Runtime.PyType_GenericAlloc(metatype, 0);
            // Clr type would not use __slots__,
            // and the PyMemberDef after PyHeapTypeObject will have other uses(e.g. type handle),
            // thus set the ob_size to 0 for avoiding slots iterations.
            Marshal.WriteIntPtr(type, TypeOffset.ob_size, IntPtr.Zero);

            // Cheat a little: we'll set tp_name to the internal char * of
            // the Python version of the type name - otherwise we'd have to
            // allocate the tp_name and would have no way to free it.
            IntPtr temp = Runtime.PyUnicode_FromString(name);
            IntPtr raw = Runtime.PyUnicode_AsUTF8(temp);
            Marshal.WriteIntPtr(type, TypeOffset.tp_name, raw);
            Marshal.WriteIntPtr(type, TypeOffset.name, temp);

            Runtime.XIncref(temp);
            Marshal.WriteIntPtr(type, TypeOffset.qualname, temp);
            temp = type + TypeOffset.nb_add;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_number, temp);

            temp = type + TypeOffset.sq_length;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_sequence, temp);

            temp = type + TypeOffset.mp_length;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_mapping, temp);

            temp = type + TypeOffset.bf_getbuffer;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_buffer, temp);
            return type;
        }

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
                    if (!name.StartsWith("tp_") && !TypeOffset.IsSupportedSlotName(name))
                    {
                        Debug.Assert(!name.Contains("_") || name.StartsWith("_") || method.IsSpecialName);
                        continue;
                    }

                    if (seen.Contains(name))
                    {
                        continue;
                    }

                    InitializeSlot(type, Interop.GetThunk(method), name, slotsHolder);

                    seen.Add(name);
                }

                impl = impl.BaseType;
            }

            foreach (string slot in _requiredSlots)
            {
                if (seen.Contains(slot))
                {
                    continue;
                }
                var offset = ManagedDataOffsets.GetSlotOffset(slot);
                Marshal.WriteIntPtr(type, offset, SlotsHolder.GetDefaultSlot(offset));
            }
        }

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
        /// <param name="canOverride">Can override the slot when it existed</param>
        static void InitializeSlot(IntPtr type, IntPtr slot, string name, bool canOverride = true)
        {
            var offset = ManagedDataOffsets.GetSlotOffset(name);
            if (!canOverride && Marshal.ReadIntPtr(type, offset) != IntPtr.Zero)
            {
                return;
            }
            Marshal.WriteIntPtr(type, offset, slot);
        }

        static void InitializeSlot(IntPtr type, ThunkInfo thunk, string name, SlotsHolder slotsHolder = null, bool canOverride = true)
        {
            int offset = ManagedDataOffsets.GetSlotOffset(name);

            if (!canOverride && Marshal.ReadIntPtr(type, offset) != IntPtr.Zero)
            {
                return;
            }
            Marshal.WriteIntPtr(type, offset, thunk.Address);
            if (slotsHolder != null)
            {
                slotsHolder.Set(offset, thunk);
            }
        }

        static void InitializeSlot(IntPtr type, int slotOffset, MethodInfo method, SlotsHolder slotsHolder = null)
        {
            var thunk = Interop.GetThunk(method);
            Marshal.WriteIntPtr(type, slotOffset, thunk.Address);
            if (slotsHolder != null)
            {
                slotsHolder.Set(slotOffset, thunk);
            }
        }

        static bool IsSlotSet(IntPtr type, string name)
        {
            int offset = ManagedDataOffsets.GetSlotOffset(name);
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
                            m.DecrRefCount();
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

        private static SlotsHolder CreateSolotsHolder(IntPtr type)
        {
            var holder = new SlotsHolder(type);
            _slotsHolders.Add(type, holder);
            return holder;
        }
    }


    class SlotsHolder
    {
        public delegate void Resetor(IntPtr type, int offset);

        private readonly IntPtr _type;
        private Dictionary<int, ThunkInfo> _slots = new Dictionary<int, ThunkInfo>();
        private List<ThunkInfo> _keepalive = new List<ThunkInfo>();
        private Dictionary<int, Resetor> _customResetors = new Dictionary<int, Resetor>();
        private List<Action> _deallocators = new List<Action>();
        private bool _alreadyReset = false;

        /// <summary>
        /// Create slots holder for holding the delegate of slots and be able  to reset them.
        /// </summary>
        /// <param name="type">Steals a reference to target type</param>
        public SlotsHolder(IntPtr type)
        {
            _type = type;
        }

        public void Set(int offset, ThunkInfo thunk)
        {
            _slots[offset] = thunk;
        }

        public void Set(int offset, Resetor resetor)
        {
            _customResetors[offset] = resetor;
        }

        public void AddDealloctor(Action deallocate)
        {
            _deallocators.Add(deallocate);
        }

        public void KeeapAlive(ThunkInfo thunk)
        {
            _keepalive.Add(thunk);
        }

        public void ResetSlots()
        {
            if (_alreadyReset)
            {
                return;
            }
            _alreadyReset = true;
#if DEBUG
            IntPtr tp_name = Marshal.ReadIntPtr(_type, TypeOffset.tp_name);
            string typeName = Marshal.PtrToStringAnsi(tp_name);
#endif
            foreach (var offset in _slots.Keys)
            {
                IntPtr ptr = GetDefaultSlot(offset);
#if DEBUG
                //DebugUtil.Print($"Set slot<{TypeOffsetHelper.GetSlotNameByOffset(offset)}> to 0x{ptr.ToString("X")} at {typeName}<0x{_type}>");
#endif
                Marshal.WriteIntPtr(_type, offset, ptr);
            }

            foreach (var action in _deallocators)
            {
                action();
            }

            foreach (var pair in _customResetors)
            {
                int offset = pair.Key;
                var resetor = pair.Value;
                resetor?.Invoke(_type, offset);
            }

            _customResetors.Clear();
            _slots.Clear();
            _keepalive.Clear();
            _deallocators.Clear();

            // Custom reset
            IntPtr handlePtr = Marshal.ReadIntPtr(_type, TypeOffset.magic());
            if (handlePtr != IntPtr.Zero)
            {
                GCHandle handle = GCHandle.FromIntPtr(handlePtr);
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
                Marshal.WriteIntPtr(_type, TypeOffset.magic(), IntPtr.Zero);
            }
        }

        public static IntPtr GetDefaultSlot(int offset)
        {
            if (offset == TypeOffset.tp_clear)
            {
                return TypeManager.subtype_clear;
            }
            else if (offset == TypeOffset.tp_traverse)
            {
                return TypeManager.subtype_traverse;
            }
            else if (offset == TypeOffset.tp_dealloc)
            {
                // tp_free of PyTypeType is point to PyObejct_GC_Del.
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
            using var globals = NewReference.DangerousFromPointer(Runtime.PyDict_New());
            if (Runtime.PyDict_SetItemString(globals, "__builtins__", Runtime.PyEval_GetBuiltins()) != 0)
            {
                globals.Dispose();
                throw new PythonException();
            }
            const string code = "class A(object): pass";
            using var resRef = Runtime.PyRun_String(code, RunFlagType.File, globals, globals);
            if (resRef.IsNull())
            {
                globals.Dispose();
                throw new PythonException();
            }
            resRef.Dispose();
            BorrowedReference A = Runtime.PyDict_GetItemString(globals, "A");
            Debug.Assert(!A.IsNull);
            return new NewReference(A).DangerousMoveToPointer();
        }
    }
}
