using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Python.Runtime.Native;
using Python.Runtime.StateSerialization;


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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        /// <remarks>initialized in <see cref="Initialize"/> rather than in constructor</remarks>
        internal static IPythonBaseTypeProvider pythonBaseTypeProvider;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


        private const BindingFlags tbFlags = BindingFlags.Public | BindingFlags.Static;
        private static readonly Dictionary<MaybeType, PyType> cache = new();

        static readonly Dictionary<PyType, SlotsHolder> _slotsHolders = new(PythonReferenceComparer.Instance);

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
            using (var plainType = SlotHelper.CreateObjectType())
            {
                subtype_traverse = Util.ReadIntPtr(plainType.Borrow(), TypeOffset.tp_traverse);
                subtype_clear = Util.ReadIntPtr(plainType.Borrow(), TypeOffset.tp_clear);
            }
            pythonBaseTypeProvider = PythonEngine.InteropConfiguration.pythonBaseTypeProviders;
        }

        internal static void RemoveTypes()
        {
            if (Runtime.HostedInPython)
            {
                foreach (var holder in _slotsHolders)
                {
                    // If refcount > 1, it needs to reset the managed slot,
                    // otherwise it can dealloc without any trick.
                    if (holder.Key.Refcount > 1)
                    {
                        holder.Value.ResetSlots();
                    }
                }
            }

            foreach (var type in cache.Values)
            {
                type.Dispose();
            }
            cache.Clear();
            _slotsHolders.Clear();
        }

        internal static TypeManagerState SaveRuntimeData()
            => new()
            {
                Cache = cache,
            };

        internal static void RestoreRuntimeData(TypeManagerState storage)
        {
            Debug.Assert(cache == null || cache.Count == 0);
            var typeCache = storage.Cache;
            foreach (var entry in typeCache)
            {
                var type = entry.Key.Value;
                cache![type] = entry.Value;
                SlotsHolder holder = CreateSlotsHolder(entry.Value);
                InitializeSlots(entry.Value, type, holder);
                Runtime.PyType_Modified(entry.Value);
            }
        }

        internal static PyType GetType(Type type)
        {
            // Note that these types are cached with a refcount of 1, so they
            // effectively exist until the CPython runtime is finalized.
            if (!cache.TryGetValue(type, out var pyType))
            {
                pyType = CreateType(type);
                cache[type] = pyType;
            }
            return pyType;
        }
        /// <summary>
        /// Given a managed Type derived from ExtensionType, get the handle to
        /// a Python type object that delegates its implementation to the Type
        /// object. These Python type instances are used to implement internal
        /// descriptor and utility types like ModuleObject, PropertyObject, etc.
        /// </summary>
        internal static BorrowedReference GetTypeReference(Type type) => GetType(type).Reference;

        /// <summary>
        /// The following CreateType implementations do the necessary work to
        /// create Python types to represent managed extension types, reflected
        /// types, subclasses of reflected types and the managed metatype. The
        /// dance is slightly different for each kind of type due to different
        /// behavior needed and the desire to have the existing Python runtime
        /// do as much of the allocation and initialization work as possible.
        /// </summary>
        internal static unsafe PyType CreateType(Type impl)
        {
            // TODO: use PyType(TypeSpec) constructor
            PyType type = AllocateTypeObject(impl.Name, metatype: Runtime.PyCLRMetaType);

            BorrowedReference base_ = impl == typeof(CLRModule)
                ? Runtime.PyModuleType
                : Runtime.PyBaseObjectType;

            type.BaseReference = base_;

            int newFieldOffset = InheritOrAllocateStandardFields(type, base_);

            int tp_clr_inst_offset = newFieldOffset;
            newFieldOffset += IntPtr.Size;

            int ob_size = newFieldOffset;
            // Set tp_basicsize to the size of our managed instance objects.
            Util.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);
            Util.WriteInt32(type, ManagedType.Offsets.tp_clr_inst_offset, tp_clr_inst_offset);
            Util.WriteIntPtr(type, TypeOffset.tp_new, (IntPtr)Runtime.Delegates.PyType_GenericNew);

            SlotsHolder slotsHolder = CreateSlotsHolder(type);
            InitializeSlots(type, impl, slotsHolder);

            type.Flags = TypeFlags.Default | TypeFlags.HasClrInstance |
                           TypeFlags.HeapType | TypeFlags.HaveGC;

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }


            using (var dict = Runtime.PyObject_GenericGetDict(type.Reference))
            using (var mod = Runtime.PyString_FromString("CLR"))
            {
                Runtime.PyDict_SetItem(dict.Borrow(), PyIdentifier.__module__, mod.Borrow());
            }

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(type.Reference);
            return type;
        }


        internal static void InitializeClassCore(Type clrType, PyType pyType, ClassBase impl)
        {
            if (pyType.BaseReference != null)
            {
                return;
            }

            // Hide the gchandle of the implementation in a magic type slot.
            GCHandle gc = GCHandle.Alloc(impl);
            ManagedType.InitGCHandle(pyType, Runtime.CLRMetaType, gc);

            using var baseTuple = GetBaseTypeTuple(clrType);

            InitializeBases(pyType, baseTuple);
            // core fields must be initialized in partially constructed classes,
            // otherwise it would be impossible to manipulate GCHandle and check type size
            InitializeCoreFields(pyType);
        }

        internal static string GetPythonTypeName(Type clrType)
        {
            var result = new System.Text.StringBuilder();
            GetPythonTypeName(clrType, target: result);
            return result.ToString();
        }

        static void GetPythonTypeName(Type clrType, System.Text.StringBuilder target)
        {
            if (clrType.IsGenericType)
            {
                string fullName = clrType.GetGenericTypeDefinition().FullName;
                int argCountIndex = fullName.LastIndexOf('`');
                if (argCountIndex >= 0)
                {
                    string nonGenericFullName = fullName.Substring(0, argCountIndex);
                    string nonGenericName = CleanupFullName(nonGenericFullName);
                    target.Append(nonGenericName);

                    var arguments = clrType.GetGenericArguments();
                    target.Append('[');
                    for (int argIndex = 0; argIndex < arguments.Length; argIndex++)
                    {
                        if (argIndex != 0)
                        {
                            target.Append(',');
                        }

                        GetPythonTypeName(arguments[argIndex], target);
                    }

                    target.Append(']');

                    int nestedStart = fullName.IndexOf('+');
                    while (nestedStart >= 0)
                    {
                        target.Append('.');
                        int nextNested = fullName.IndexOf('+', nestedStart + 1);
                        if (nextNested < 0)
                        {
                            target.Append(fullName.Substring(nestedStart + 1));
                        }
                        else
                        {
                            target.Append(fullName.Substring(nestedStart + 1, length: nextNested - nestedStart - 1));
                        }
                        nestedStart = nextNested;
                    }

                    return;
                }
            }

            string name = CleanupFullName(clrType.FullName);
            target.Append(name);
        }

        static string CleanupFullName(string fullTypeName)
        {
            // Cleanup the type name to get rid of funny nested type names.
            string name = "clr." + fullTypeName;
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

            return name;
        }

        static BorrowedReference InitializeBases(PyType pyType, PyTuple baseTuple)
        {
            Debug.Assert(baseTuple.Length() > 0);
            var primaryBase = baseTuple[0].Reference;
            pyType.BaseReference = primaryBase;

            if (baseTuple.Length() > 1)
            {
                Util.WriteIntPtr(pyType, TypeOffset.tp_bases, baseTuple.NewReferenceOrNull().DangerousMoveToPointer());
            }
            return primaryBase;
        }

        static void InitializeCoreFields(PyType type)
        {
            int newFieldOffset = InheritOrAllocateStandardFields(type);

            if (ManagedType.IsManagedType(type.BaseReference))
            {
                int baseClrInstOffset = Util.ReadInt32(type.BaseReference, ManagedType.Offsets.tp_clr_inst_offset);
                Util.WriteInt32(type, ManagedType.Offsets.tp_clr_inst_offset, baseClrInstOffset);
            }
            else
            {
                Util.WriteInt32(type, ManagedType.Offsets.tp_clr_inst_offset, newFieldOffset);
                newFieldOffset += IntPtr.Size;
            }

            int ob_size = newFieldOffset;

            Util.WriteIntPtr(type, TypeOffset.tp_basicsize, (IntPtr)ob_size);
            Util.WriteIntPtr(type, TypeOffset.tp_itemsize, IntPtr.Zero);
        }

        internal static void InitializeClass(PyType type, ClassBase impl, Type clrType)
        {
            // we want to do this after the slot stuff above in case the class itself implements a slot method
            SlotsHolder slotsHolder = CreateSlotsHolder(type);
            InitializeSlots(type, impl.GetType(), slotsHolder);

            impl.InitializeSlots(type, slotsHolder);

            OperatorMethod.FixupSlots(type, clrType);
            // Leverage followup initialization from the Python runtime. Note
            // that the type of the new type must PyType_Type at the time we
            // call this, else PyType_Ready will skip some slot initialization.

            if (!type.IsReady && Runtime.PyType_Ready(type) != 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }

            var dict = Util.ReadRef(type, TypeOffset.tp_dict);
            string mn = clrType.Namespace ?? "";
            using (var mod = Runtime.PyString_FromString(mn))
                Runtime.PyDict_SetItem(dict, PyIdentifier.__module__, mod.Borrow());

            Runtime.PyType_Modified(type.Reference);

            //DebugUtil.DumpType(type);
        }

        static int InheritOrAllocateStandardFields(BorrowedReference type)
        {
            var @base = Util.ReadRef(type, TypeOffset.tp_base);
            return InheritOrAllocateStandardFields(type, @base);
        }
        static int InheritOrAllocateStandardFields(BorrowedReference typeRef, BorrowedReference @base)
        {
            IntPtr baseAddress = @base.DangerousGetAddress();
            IntPtr type = typeRef.DangerousGetAddress();
            int baseSize = Util.ReadInt32(@base, TypeOffset.tp_basicsize);
            int newFieldOffset = baseSize;

            void InheritOrAllocate(int typeField)
            {
                int value = Marshal.ReadInt32(baseAddress, typeField);
                if (value == 0)
                {
                    Marshal.WriteIntPtr(type, typeField, new IntPtr(newFieldOffset));
                    newFieldOffset += IntPtr.Size;
                }
                else
                {
                    Marshal.WriteIntPtr(type, typeField, new IntPtr(value));
                }
            }

            InheritOrAllocate(TypeOffset.tp_dictoffset);
            InheritOrAllocate(TypeOffset.tp_weaklistoffset);

            return newFieldOffset;
        }

        static PyTuple GetBaseTypeTuple(Type clrType)
        {
            var bases = pythonBaseTypeProvider
                .GetBaseTypes(clrType, new PyType[0])
                ?.ToArray();
            if (bases is null || bases.Length == 0)
            {
                throw new InvalidOperationException("At least one base type must be specified");
            }
            var nonBases = bases.Where(@base => !@base.Flags.HasFlag(TypeFlags.BaseType)).ToList();
            if (nonBases.Count > 0)
            {
                throw new InvalidProgramException("The specified Python type(s) can not be inherited from: "
                                                  + string.Join(", ", nonBases));
            }

            return new PyTuple(bases);
        }

        internal static NewReference CreateSubType(BorrowedReference py_name, BorrowedReference py_base_type, BorrowedReference dictRef)
        {
            // Utility to create a subtype of a managed type with the ability for the
            // a python subtype able to override the managed implementation
            string? name = Runtime.GetManagedString(py_name);
            if (name is null)
            {
                Exceptions.SetError(Exceptions.ValueError, "Class name must not be None");
                return default;
            }

            // the derived class can have class attributes __assembly__ and __module__ which
            // control the name of the assembly and module the new type is created in.
            object? assembly = null;
            object? namespaceStr = null;

            using (var assemblyKey = new PyString("__assembly__"))
            {
                var assemblyPtr = Runtime.PyDict_GetItemWithError(dictRef, assemblyKey.Reference);
                if (assemblyPtr.IsNull)
                {
                    if (Exceptions.ErrorOccurred()) return default;
                }
                else if (!Converter.ToManagedValue(assemblyPtr, typeof(string), out assembly, true))
                {
                    return Exceptions.RaiseTypeError("Couldn't convert __assembly__ value to string");
                }

                using var namespaceKey = new PyString("__namespace__");
                var pyNamespace = Runtime.PyDict_GetItemWithError(dictRef, namespaceKey.Reference);
                if (pyNamespace.IsNull)
                {
                    if (Exceptions.ErrorOccurred()) return default;
                }
                else if (!Converter.ToManagedValue(pyNamespace, typeof(string), out namespaceStr, true))
                {
                    return Exceptions.RaiseTypeError("Couldn't convert __namespace__ value to string");
                }
            }

            // create the new managed type subclassing the base managed type
            if (ManagedType.GetManagedObject(py_base_type) is ClassBase baseClass)
            {
                return ReflectedClrType.CreateSubclass(baseClass, name,
                                                       ns: (string?)namespaceStr,
                                                       assembly: (string?)assembly,
                                                       dict: dictRef);
            }
            else
            {
                return Exceptions.RaiseTypeError("invalid base class, expected CLR class type");
            }
        }

        internal static IntPtr WriteMethodDef(IntPtr mdef, IntPtr name, IntPtr func, PyMethodFlags flags, IntPtr doc)
        {
            Marshal.WriteIntPtr(mdef, name);
            Marshal.WriteIntPtr(mdef, 1 * IntPtr.Size, func);
            Marshal.WriteInt32(mdef, 2 * IntPtr.Size, (int)flags);
            Marshal.WriteIntPtr(mdef, 3 * IntPtr.Size, doc);
            return mdef + 4 * IntPtr.Size;
        }

        internal static IntPtr WriteMethodDef(IntPtr mdef, string name, IntPtr func, PyMethodFlags flags = PyMethodFlags.VarArgs,
            string? doc = null)
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

        internal static PyType CreateMetatypeWithGCHandleOffset()
        {
            var py_type = new PyType(Runtime.PyTypeType, prevalidated: true);
            int size = Util.ReadInt32(Runtime.PyTypeType, TypeOffset.tp_basicsize)
                       + IntPtr.Size // tp_clr_inst_offset
            ;

            var slots = new[] {
                new TypeSpec.Slot(TypeSlotID.tp_traverse, subtype_traverse),
                new TypeSpec.Slot(TypeSlotID.tp_clear, subtype_clear)
            };
            var result = new PyType(
                new TypeSpec(
                    "clr._internal.GCOffsetBase",
                    basicSize: size,
                    slots: slots,
                    TypeFlags.Default | TypeFlags.HeapType | TypeFlags.HaveGC
                ),
                bases: new PyTuple(new[] { py_type })
            );

            return result;
        }

        internal static PyType CreateMetaType(Type impl, out SlotsHolder slotsHolder)
        {
            // The managed metatype is functionally little different than the
            // standard Python metatype (PyType_Type). It overrides certain of
            // the standard type slots, and has to subclass PyType_Type for
            // certain functions in the C runtime to work correctly with it.

            PyType gcOffsetBase = CreateMetatypeWithGCHandleOffset();

            PyType type = AllocateTypeObject("CLRMetatype", metatype: gcOffsetBase);

            Util.WriteRef(type, TypeOffset.tp_base, new NewReference(gcOffsetBase).Steal());

            nint size = Util.ReadInt32(gcOffsetBase, TypeOffset.tp_basicsize)
                       + IntPtr.Size // tp_clr_inst
            ;
            Util.WriteIntPtr(type, TypeOffset.tp_basicsize, size);
            Util.WriteInt32(type, ManagedType.Offsets.tp_clr_inst_offset, ManagedType.Offsets.tp_clr_inst);

            const TypeFlags flags = TypeFlags.Default
                            | TypeFlags.HeapType
                            | TypeFlags.HaveGC
                            | TypeFlags.HasClrInstance;
            Util.WriteCLong(type, TypeOffset.tp_flags, (int)flags);

            // Slots will inherit from TypeType, it's not neccesary for setting them.
            // Inheried slots:
            // tp_basicsize, tp_itemsize,
            // tp_dictoffset, tp_weaklistoffset,
            // tp_traverse, tp_clear, tp_is_gc, etc.
            slotsHolder = SetupMetaSlots(impl, type);

            if (Runtime.PyType_Ready(type) != 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }

            BorrowedReference dict = Util.ReadRef(type, TypeOffset.tp_dict);
            using (var mod = Runtime.PyString_FromString("clr._internal"))
                Runtime.PyDict_SetItemString(dict, "__module__", mod.Borrow());

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(type);
            //DebugUtil.DumpType(type);

            return type;
        }

        internal static SlotsHolder SetupMetaSlots(Type impl, PyType type)
        {
            // Override type slots with those of the managed implementation.
            var slotsHolder = new SlotsHolder(type);
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

            Util.WriteIntPtr(type, TypeOffset.tp_methods, mdefStart);

            // XXX: Hard code with mode check.
            if (Runtime.HostedInPython)
            {
                slotsHolder.Set(TypeOffset.tp_methods, (t, offset) =>
                {
                    var p = Util.ReadIntPtr(t, offset);
                    Runtime.PyMem_Free(p);
                    Util.WriteIntPtr(t, offset, IntPtr.Zero);
                });
            }
            return slotsHolder;
        }

        private static IntPtr AddCustomMetaMethod(string name, PyType type, IntPtr mdef, SlotsHolder slotsHolder)
        {
            MethodInfo mi = typeof(MetaType).GetMethod(name);
            ThunkInfo thunkInfo = Interop.GetThunk(mi);
            slotsHolder.KeeapAlive(thunkInfo);

            // XXX: Hard code with mode check.
            if (Runtime.HostedInPython)
            {
                IntPtr mdefAddr = mdef;
                slotsHolder.AddDealloctor(() =>
                {
                    var tp_dict = Util.ReadRef(type, TypeOffset.tp_dict);
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

        /// <summary>
        /// Utility method to allocate a type object &amp; do basic initialization.
        /// </summary>
        internal static PyType AllocateTypeObject(string name, PyType metatype)
        {
            var newType = Runtime.PyType_GenericAlloc(metatype, 0);
            var type = new PyType(newType.StealOrThrow());
            // Clr type would not use __slots__,
            // and the PyMemberDef after PyHeapTypeObject will have other uses(e.g. type handle),
            // thus set the ob_size to 0 for avoiding slots iterations.
            Util.WriteIntPtr(type, TypeOffset.ob_size, IntPtr.Zero);

            // Cheat a little: we'll set tp_name to the internal char * of
            // the Python version of the type name - otherwise we'd have to
            // allocate the tp_name and would have no way to free it.
            using var temp = Runtime.PyString_FromString(name);
            IntPtr raw = Runtime.PyUnicode_AsUTF8(temp.BorrowOrThrow());
            Util.WriteIntPtr(type, TypeOffset.tp_name, raw);
            Util.WriteRef(type, TypeOffset.name, new NewReference(temp).Steal());
            Util.WriteRef(type, TypeOffset.qualname, temp.Steal());

            // Ensure that tp_traverse and tp_clear are always set, since their
            // existence is enforced in newer Python versions in PyType_Ready
            Util.WriteIntPtr(type, TypeOffset.tp_traverse, subtype_traverse);
            Util.WriteIntPtr(type, TypeOffset.tp_clear, subtype_clear);

            InheritSubstructs(type.Reference.DangerousGetAddress());

            return type;
        }

        /// <summary>
        /// Inherit substructs, that are not inherited by default:
        /// https://docs.python.org/3/c-api/typeobj.html#c.PyTypeObject.tp_as_number
        /// </summary>
        static void InheritSubstructs(IntPtr type)
        {
            IntPtr substructAddress = type + TypeOffset.nb_add;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_number, substructAddress);

            substructAddress = type + TypeOffset.sq_length;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_sequence, substructAddress);

            substructAddress = type + TypeOffset.mp_length;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_mapping, substructAddress);

            substructAddress = type + TypeOffset.bf_getbuffer;
            Marshal.WriteIntPtr(type, TypeOffset.tp_as_buffer, substructAddress);
        }

        /// <summary>
        /// Given a newly allocated Python type object and a managed Type that
        /// provides the implementation for the type, connect the type slots of
        /// the Python object to the managed methods of the implementing Type.
        /// </summary>
        internal static void InitializeSlots(PyType type, Type impl, SlotsHolder? slotsHolder = null)
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

                var initSlot = impl.GetMethod("InitializeSlots", BindingFlags.Static | BindingFlags.Public);
                initSlot?.Invoke(null, parameters: new object?[] { type, seen, slotsHolder });

                impl = impl.BaseType;
            }

            SetRequiredSlots(type, seen);
        }

        private static void SetRequiredSlots(PyType type, HashSet<string> seen)
        {
            foreach (string slot in _requiredSlots)
            {
                if (seen.Contains(slot))
                {
                    continue;
                }
                var offset = TypeOffset.GetSlotOffset(slot);
                Util.WriteIntPtr(type, offset, SlotsHolder.GetDefaultSlot(offset));
            }
        }

        static void InitializeSlot(BorrowedReference type, ThunkInfo thunk, string name, SlotsHolder? slotsHolder)
        {
            if (!Enum.TryParse<TypeSlotID>(name, out _))
            {
                throw new NotSupportedException("Bad slot name " + name);
            }
            int offset = TypeOffset.GetSlotOffset(name);
            InitializeSlot(type, offset, thunk, slotsHolder);
        }

        static void InitializeSlot(BorrowedReference type, int slotOffset, MethodInfo method, SlotsHolder slotsHolder)
        {
            var thunk = Interop.GetThunk(method);
            InitializeSlot(type, slotOffset, thunk, slotsHolder);
        }

        internal static void InitializeSlot(BorrowedReference type, int slotOffset, Delegate impl, SlotsHolder slotsHolder)
        {
            var thunk = Interop.GetThunk(impl);
            InitializeSlot(type, slotOffset, thunk, slotsHolder);
        }

        internal static void InitializeSlotIfEmpty(BorrowedReference type, int slotOffset, Delegate impl, SlotsHolder slotsHolder)
        {
            if (slotsHolder.IsHolding(slotOffset)) return;
            InitializeSlot(type, slotOffset, impl, slotsHolder);
        }

        static void InitializeSlot(BorrowedReference type, int slotOffset, ThunkInfo thunk, SlotsHolder? slotsHolder)
        {
            Util.WriteIntPtr(type, slotOffset, thunk.Address);
            if (slotsHolder != null)
            {
                slotsHolder.Set(slotOffset, thunk);
            }
        }

        /// <summary>
        /// Utility method to copy slots from a given type to another type.
        /// </summary>
        internal static void CopySlot(BorrowedReference from, BorrowedReference to, int offset)
        {
            IntPtr fp = Util.ReadIntPtr(from, offset);
            Util.WriteIntPtr(to, offset, fp);
        }

        internal static SlotsHolder CreateSlotsHolder(PyType type)
        {
            type = new PyType(type);
            var holder = new SlotsHolder(type);
            _slotsHolders.Add(type, holder);
            return holder;
        }
    }


    class SlotsHolder
    {
        public delegate void Resetor(PyType type, int offset);

        private readonly Dictionary<int, ThunkInfo> _slots = new();
        private readonly List<ThunkInfo> _keepalive = new();
        private readonly Dictionary<int, Resetor> _customResetors = new();
        private readonly List<Action> _deallocators = new();
        private bool _alreadyReset = false;

        private readonly PyType Type;

        public string?[] Holds => _slots.Keys.Select(TypeOffset.GetSlotName).ToArray();

        /// <summary>
        /// Create slots holder for holding the delegate of slots and be able  to reset them.
        /// </summary>
        /// <param name="type">Steals a reference to target type</param>
        public SlotsHolder(PyType type)
        {
            this.Type = type;
        }

        public bool IsHolding(int offset) => _slots.ContainsKey(offset);

        public ICollection<int> Slots => _slots.Keys;

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

        public static void ResetSlots(BorrowedReference type, IEnumerable<int> slots)
        {
            foreach (int offset in slots)
            {
                IntPtr ptr = GetDefaultSlot(offset);
#if DEBUG
                //DebugUtil.Print($"Set slot<{TypeOffsetHelper.GetSlotNameByOffset(offset)}> to 0x{ptr.ToString("X")} at {typeName}<0x{_type}>");
#endif
                Util.WriteIntPtr(type, offset, ptr);
            }
        }

        public void ResetSlots()
        {
            if (_alreadyReset)
            {
                return;
            }
            _alreadyReset = true;
#if DEBUG
            IntPtr tp_name = Util.ReadIntPtr(Type, TypeOffset.tp_name);
            string typeName = Marshal.PtrToStringAnsi(tp_name);
#endif
            ResetSlots(Type, _slots.Keys);

            foreach (var action in _deallocators)
            {
                action();
            }

            foreach (var pair in _customResetors)
            {
                int offset = pair.Key;
                var resetor = pair.Value;
                resetor?.Invoke(Type, offset);
            }

            _customResetors.Clear();
            _slots.Clear();
            _keepalive.Clear();
            _deallocators.Clear();

            // Custom reset
            if (Type != Runtime.CLRMetaType)
            {
                var metatype = Runtime.PyObject_TYPE(Type);
                ManagedType.TryFreeGCHandle(Type, metatype);
            }
            Runtime.PyType_Modified(Type);
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
                return Util.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_free);
            }
            else if (offset == TypeOffset.tp_free)
            {
                // PyObject_GC_Del
                return Util.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_free);
            }
            else if (offset == TypeOffset.tp_call)
            {
                return IntPtr.Zero;
            }
            else if (offset == TypeOffset.tp_new)
            {
                // PyType_GenericNew
                return Util.ReadIntPtr(Runtime.PySuper_Type, TypeOffset.tp_new);
            }
            else if (offset == TypeOffset.tp_getattro)
            {
                // PyObject_GenericGetAttr
                return Util.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_getattro);
            }
            else if (offset == TypeOffset.tp_setattro)
            {
                // PyObject_GenericSetAttr
                return Util.ReadIntPtr(Runtime.PyBaseObjectType, TypeOffset.tp_setattro);
            }

            return Util.ReadIntPtr(Runtime.PyTypeType, offset);
        }
    }


    static class SlotHelper
    {
        public static NewReference CreateObjectType()
        {
            using var globals = Runtime.PyDict_New();
            if (Runtime.PyDict_SetItemString(globals.Borrow(), "__builtins__", Runtime.PyEval_GetBuiltins()) != 0)
            {
                globals.Dispose();
                throw PythonException.ThrowLastAsClrException();
            }
            const string code = "class A(object): pass";
            using var resRef = Runtime.PyRun_String(code, RunFlagType.File, globals.Borrow(), globals.Borrow());
            if (resRef.IsNull())
            {
                globals.Dispose();
                throw PythonException.ThrowLastAsClrException();
            }
            resRef.Dispose();
            BorrowedReference A = Runtime.PyDict_GetItemString(globals.Borrow(), "A");
            return new NewReference(A);
        }
    }
}
