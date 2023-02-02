using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

using Python.Runtime.StateSerialization;

namespace Python.Runtime
{
    /// <summary>
    /// The managed metatype. This object implements the type of all reflected
    /// types. It also provides support for single-inheritance from reflected
    /// managed types.
    /// </summary>
    internal sealed class MetaType : ManagedType
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // set in Initialize
        private static PyType PyCLRMetaType;
        private static SlotsHolder _metaSlotsHodler;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        internal static readonly string[] CustomMethods = new string[]
        {
            "__instancecheck__",
            "__subclasscheck__",
        };

        /// <summary>
        /// Metatype initialization. This bootstraps the CLR metatype to life.
        /// </summary>
        public static PyType Initialize()
        {
            PyCLRMetaType = TypeManager.CreateMetaType(typeof(MetaType), out _metaSlotsHodler);
            return PyCLRMetaType;
        }

        public static void Release()
        {
            if (Runtime.HostedInPython)
            {
                _metaSlotsHodler.ResetSlots();
            }
            PyCLRMetaType.Dispose();
        }

        internal static MetatypeState SaveRuntimeData() => new() { CLRMetaType = PyCLRMetaType };

        internal static PyType RestoreRuntimeData(MetatypeState storage)
        {
            PyCLRMetaType = storage.CLRMetaType;
            _metaSlotsHodler = new SlotsHolder(PyCLRMetaType);
            TypeManager.InitializeSlots(PyCLRMetaType, typeof(MetaType), _metaSlotsHodler);

            IntPtr mdef = Util.ReadIntPtr(PyCLRMetaType, TypeOffset.tp_methods);
            foreach (var methodName in CustomMethods)
            {
                var mi = typeof(MetaType).GetMethod(methodName);
                ThunkInfo thunkInfo = Interop.GetThunk(mi);
                _metaSlotsHodler.KeeapAlive(thunkInfo);
                mdef = TypeManager.WriteMethodDef(mdef, methodName, thunkInfo.Address);
            }
            return PyCLRMetaType;
        }

        /// <summary>
        /// Metatype __new__ implementation. This is called to create a new
        /// class / type when a reflected class is subclassed.
        /// </summary>
        public static NewReference tp_new(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            var len = Runtime.PyTuple_Size(args);
            if (len < 3)
            {
                return Exceptions.RaiseTypeError("invalid argument list");
            }

            BorrowedReference name = Runtime.PyTuple_GetItem(args, 0);
            BorrowedReference bases = Runtime.PyTuple_GetItem(args, 1);
            BorrowedReference dict = Runtime.PyTuple_GetItem(args, 2);

            // Extract interface types and base class types.
            var interfaces = new List<Type>();

            // More than one base type case be declared, but an exception will be thrown
            // if more than one is a class/not an interface.
            var baseTypes = new List<ClassBase>();

            var baseClassCount = Runtime.PyTuple_Size(bases);
            if (baseClassCount == 0)
            {
                return Exceptions.RaiseTypeError("zero base classes ");
            }

            for (nint i = 0; i < baseClassCount; i++)
            {
                var baseTypeIt = Runtime.PyTuple_GetItem(bases, (int)i);

                if (GetManagedObject(baseTypeIt) is ClassBase classBaseIt)
                {
                    if (!classBaseIt.type.Valid)
                    {
                        return Exceptions.RaiseTypeError("Invalid type used as a super type.");
                    }
                    if (classBaseIt.type.Value.IsInterface)
                    {
                        interfaces.Add(classBaseIt.type.Value);
                    }
                    else
                    {
                        baseTypes.Add(classBaseIt);
                    }
                }
                else
                {
                    return Exceptions.RaiseTypeError("Non .NET type used as super class for meta type. This is not supported.");
                }
            }
            // if the base type count is 0, there might still be interfaces to implement.
            if (baseTypes.Count == 0)
            {
                baseTypes.Add(new ClassBase(typeof(object)));
            }

            // Multiple inheritance is not supported, unless the other types are interfaces
            if (baseTypes.Count > 1)
            {
                var types = string.Join(", ", baseTypes.Select(baseType => baseType.type.Value));
                return Exceptions.RaiseTypeError($"Multiple inheritance with managed classes cannot be used. Types: {types} ");
            }

            // check if the list of interfaces contains no duplicates.
            if (interfaces.Distinct().Count() != interfaces.Count)
            {
                // generate a string containing the problematic types.
                var duplicateTypes = interfaces.GroupBy(type => type)
                    .Where(typeGroup => typeGroup.Count() > 1)
                    .Select(typeGroup => typeGroup.Key);
                var duplicateTypesString = string.Join(", ", duplicateTypes);

                return Exceptions.RaiseTypeError($"An interface can only be implemented once. Duplicate types: {duplicateTypesString}");
            }

            var cb = baseTypes[0];
            try
            {
                if (!cb.CanSubclass())
                {
                    return Exceptions.RaiseTypeError("delegates, enums and array types cannot be subclassed");
                }
            }
            catch (SerializationException)
            {
                return Exceptions.RaiseTypeError($"Underlying C# Base class {cb.type} has been deleted");
            }

            BorrowedReference slots = Runtime.PyDict_GetItem(dict, PyIdentifier.__slots__);
            if (slots != null)
            {
                return Exceptions.RaiseTypeError("subclasses of managed classes do not support __slots__");
            }

            // If __assembly__ or __namespace__ are in the class dictionary then create
            // a managed sub type.
            // This creates a new managed type that can be used from .net to call back
            // into python.
            if (null != dict)
            {
                using var clsDict = new PyDict(dict);
                if (clsDict.HasKey("__assembly__") || clsDict.HasKey("__namespace__"))
                {
                    return TypeManager.CreateSubType(name, baseTypes[0], interfaces, clsDict);
                }
            }

            var base_type = Runtime.PyTuple_GetItem(bases, 0);

            // otherwise just create a basic type without reflecting back into the managed side.
            IntPtr func = Util.ReadIntPtr(Runtime.PyTypeType, TypeOffset.tp_new);
            NewReference type = NativeCall.Call_3(func, tp, args, kw);
            if (type.IsNull())
            {
                return default;
            }

            var flags = PyType.GetFlags(type.Borrow());
            if (!flags.HasFlag(TypeFlags.Ready))
                throw new NotSupportedException("PyType.tp_new returned an incomplete type");
            flags |= TypeFlags.HasClrInstance;
            flags |= TypeFlags.HeapType;
            flags |= TypeFlags.BaseType;
            flags |= TypeFlags.Subclass;
            flags |= TypeFlags.HaveGC;
            PyType.SetFlags(type.Borrow(), flags);

            TypeManager.CopySlot(base_type, type.Borrow(), TypeOffset.tp_dealloc);

            // Hmm - the standard subtype_traverse, clear look at ob_size to
            // do things, so to allow gc to work correctly we need to move
            // our hidden handle out of ob_size. Then, in theory we can
            // comment this out and still not crash.
            TypeManager.CopySlot(base_type, type.Borrow(), TypeOffset.tp_traverse);
            TypeManager.CopySlot(base_type, type.Borrow(), TypeOffset.tp_clear);

            // derived types must have their GCHandle at the same offset as the base types
            int clrInstOffset = Util.ReadInt32(base_type, Offsets.tp_clr_inst_offset);
            Debug.Assert(clrInstOffset > 0
                      && clrInstOffset < Util.ReadInt32(type.Borrow(), TypeOffset.tp_basicsize));
            Util.WriteInt32(type.Borrow(), Offsets.tp_clr_inst_offset, clrInstOffset);

            // for now, move up hidden handle...
            var gc = (GCHandle)Util.ReadIntPtr(base_type, Offsets.tp_clr_inst);
            Util.WriteIntPtr(type.Borrow(), Offsets.tp_clr_inst, (IntPtr)GCHandle.Alloc(gc.Target));

            Runtime.PyType_Modified(type.Borrow());

            return type;
        }


        public static NewReference tp_alloc(BorrowedReference mt, nint n)
            => Runtime.PyType_GenericAlloc(mt, n);


        public static void tp_free(NewReference tp)
        {
            Runtime.PyObject_GC_Del(tp.Steal());
        }


        /// <summary>
        /// Metatype __call__ implementation. This is needed to ensure correct
        /// initialization (__init__ support), because the tp_call we inherit
        /// from PyType_Type won't call __init__ for metatypes it doesn't know.
        /// </summary>
        public static NewReference tp_call(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            IntPtr tp_new = Util.ReadIntPtr(tp, TypeOffset.tp_new);
            if (tp_new == IntPtr.Zero)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }

            using NewReference obj = NativeCall.Call_3(tp_new, tp, args, kw);
            if (obj.IsNull())
            {
                return default;
            }

            var type = GetManagedObject(tp)!;

            return type.Init(obj.Borrow(), args, kw)
                ? obj.Move()
                : default;
        }

        /// <summary>
        /// Type __setattr__ implementation for reflected types. Note that this
        /// is slightly different than the standard setattr implementation for
        /// the normal Python metatype (PyTypeType). We need to look first in
        /// the type object of a reflected type for a descriptor in order to
        /// support the right setattr behavior for static fields and properties.
        /// </summary>
        public static int tp_setattro(BorrowedReference tp, BorrowedReference name, BorrowedReference value)
        {
            BorrowedReference descr = Runtime._PyType_Lookup(tp, name);

            if (descr != null)
            {
                BorrowedReference dt = Runtime.PyObject_TYPE(descr);

                if (dt == Runtime.PyWrapperDescriptorType
                    || dt == Runtime.PyMethodType
                    || typeof(ExtensionType).IsInstanceOfType(GetManagedObject(descr))
                )
                {
                    IntPtr fp = Util.ReadIntPtr(dt, TypeOffset.tp_descr_set);
                    if (fp != IntPtr.Zero)
                    {
                        return NativeCall.Int_Call_3(fp, descr, name, value);
                    }
                    Exceptions.SetError(Exceptions.AttributeError, "attribute is read-only");
                    return -1;
                }
            }

            int res = Runtime.PyObject_GenericSetAttr(tp, name, value);
            Runtime.PyType_Modified(tp);

            return res;
        }

        /// <summary>
        /// The metatype has to implement [] semantics for generic types, so
        /// here we just delegate to the generic type def implementation. Its
        /// own mp_subscript
        /// </summary>
        public static NewReference mp_subscript(BorrowedReference tp, BorrowedReference idx)
        {
            if (GetManagedObject(tp) is ClassBase cb)
            {
                return cb.type_subscript(idx);
            }
            return Exceptions.RaiseTypeError("unsubscriptable object");
        }

        /// <summary>
        /// Dealloc implementation. This is called when a Python type generated
        /// by this metatype is no longer referenced from the Python runtime.
        /// </summary>
        public static void tp_dealloc(NewReference lastRef)
        {
            var weakrefs = Runtime.PyObject_GetWeakRefList(lastRef.Borrow());
            if (weakrefs != null)
            {
                Runtime.PyObject_ClearWeakRefs(lastRef.Borrow());
            }

            // Fix this when we dont cheat on the handle for subclasses!

            var flags = PyType.GetFlags(lastRef.Borrow());
            if ((flags & TypeFlags.Subclass) == 0)
            {
                TryGetGCHandle(lastRef.Borrow())?.Free();
#if DEBUG
                // prevent ExecutionEngineException in debug builds in case we have a bug
                // this would allow using managed debugger to investigate the issue
                SetGCHandle(lastRef.Borrow(), default);
#endif
            }

            var op = Runtime.PyObject_TYPE(lastRef.Borrow());
            Debug.Assert(Runtime.PyCLRMetaType is null || Runtime.PyCLRMetaType == op);
            var builtinType = Runtime.PyObject_TYPE(Runtime.PyObject_TYPE(op));

            // Delegate the rest of finalization the Python metatype. Note
            // that the PyType_Type implementation of tp_dealloc will call
            // tp_free on the type of the type being deallocated - in this
            // case our CLR metatype. That is why we implement tp_free.
            IntPtr tp_dealloc = Util.ReadIntPtr(builtinType, TypeOffset.tp_dealloc);
            NativeCall.CallDealloc(tp_dealloc, lastRef.Steal());

            // We must decref our type.
            // type_dealloc from PyType will use it to get tp_free so we must keep the value
            Runtime.XDecref(StolenReference.DangerousFromPointer(op.DangerousGetAddress()));
        }

        private static NewReference DoInstanceCheck(BorrowedReference tp, BorrowedReference args, bool checkType)
        {
            if (GetManagedObject(tp) is not ClassBase cb || !cb.type.Valid)
            {
                return new NewReference(Runtime.PyFalse);
            }

            using var argsObj = new PyList(args);
            if (argsObj.Length() != 1)
            {
                return Exceptions.RaiseTypeError("Invalid parameter count");
            }

            PyObject arg = argsObj[0];
            var otherType = checkType ? arg : arg.GetPythonType();

            if (Runtime.PyObject_TYPE(otherType) != PyCLRMetaType)
            {
                return new NewReference(Runtime.PyFalse);
            }

            if (GetManagedObject(otherType) is ClassBase otherCb && otherCb.type.Valid)
            {
                return Converter.ToPython(cb.type.Value.IsAssignableFrom(otherCb.type.Value));
            }
            return new NewReference(Runtime.PyFalse);
        }

        public static NewReference __instancecheck__(BorrowedReference tp, BorrowedReference args)
        {
            return DoInstanceCheck(tp, args, false);
        }

        public static NewReference __subclasscheck__(BorrowedReference tp, BorrowedReference args)
        {
            return DoInstanceCheck(tp, args, true);
        }
    }
}
