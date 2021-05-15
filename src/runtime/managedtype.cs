#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;

namespace Python.Runtime
{
    /// <summary>
    /// Common base class for all objects that are implemented in managed
    /// code. It defines the common fields that associate CLR and Python
    /// objects and common utilities to convert between those identities.
    /// </summary>
    [Serializable]
    internal abstract class ManagedType
    {
        internal enum TrackTypes
        {
            Untrack,
            Extension,
            Wrapper,
        }

        [NonSerialized]
        internal GCHandle gcHandle; // Native handle

        internal IntPtr pyHandle; // PyObject *
        internal IntPtr tpHandle; // PyType *

        internal BorrowedReference ObjectReference => new(pyHandle);
        internal BorrowedReference TypeReference => new(tpHandle);

        private static readonly Dictionary<ManagedType, TrackTypes> _managedObjs = new Dictionary<ManagedType, TrackTypes>();

        internal void IncrRefCount()
        {
            Runtime.XIncref(pyHandle);
        }

        internal void DecrRefCount()
        {
            Runtime.XDecref(pyHandle);
        }

        internal long RefCount
        {
            get
            {
                var gs = Runtime.PyGILState_Ensure();
                try
                {
                    return Runtime.Refcount(pyHandle);
                }
                finally
                {
                    Runtime.PyGILState_Release(gs);
                }
            }
        }

        internal GCHandle AllocGCHandle(TrackTypes track = TrackTypes.Untrack)
        {
            gcHandle = GCHandle.Alloc(this);
            if (track != TrackTypes.Untrack)
            {
                _managedObjs.Add(this, track);
            }
            return gcHandle;
        }

        internal void FreeGCHandle()
        {
            _managedObjs.Remove(this);
            if (gcHandle.IsAllocated)
            {
                gcHandle.Free();
                gcHandle = default;
            }
        }

        /// <summary>
        /// Given a Python object, return the associated managed object or null.
        /// </summary>
        internal static ManagedType? GetManagedObject(BorrowedReference ob)
            => GetManagedObject(ob.DangerousGetAddress());

        /// <summary>
        /// Given a Python object, return the associated managed object or null.
        /// </summary>
        internal static ManagedType? GetManagedObject(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                var flags = (TypeFlags)Util.ReadCLong(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.HasClrInstance) != 0)
                {
                    var gc = TryGetGCHandle(new BorrowedReference(ob));
                    return (ManagedType)gc?.Target;
                }
            }
            return null;
        }

        /// <summary>
        /// Given a Python object, return the associated managed object type or null.
        /// </summary>
        internal static ManagedType? GetManagedObjectType(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                var flags = (TypeFlags)Util.ReadCLong(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.HasClrInstance) != 0)
                {
                    var gc = GetGCHandle(new BorrowedReference(tp), Runtime.CLRMetaType);
                    return (ManagedType)gc.Target;
                }
            }
            return null;
        }

        internal static bool IsInstanceOfManagedType(BorrowedReference ob)
            => IsInstanceOfManagedType(ob.DangerousGetAddressOrNull());
        internal static bool IsInstanceOfManagedType(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                return IsManagedType(new BorrowedReference(tp));
            }
            return false;
        }

        internal static bool IsManagedType(BorrowedReference type)
        {
            var flags = (TypeFlags)Util.ReadCLong(type.DangerousGetAddress(), TypeOffset.tp_flags);
            return (flags & TypeFlags.HasClrInstance) != 0;
        }

        public bool IsClrMetaTypeInstance()
        {
            Debug.Assert(Runtime.PyCLRMetaType != IntPtr.Zero);
            Debug.Assert(pyHandle != IntPtr.Zero);
            return Runtime.PyObject_TYPE(pyHandle) == Runtime.PyCLRMetaType;
        }

        internal static IDictionary<ManagedType, TrackTypes> GetManagedObjects()
        {
            return _managedObjs;
        }

        internal static void ClearTrackedObjects()
        {
            _managedObjs.Clear();
        }

        internal static int PyVisit(IntPtr ob, IntPtr visit, IntPtr arg)
        {
            if (ob == IntPtr.Zero)
            {
                return 0;
            }
            var visitFunc = NativeCall.GetDelegate<Interop.ObjObjFunc>(visit);
            return visitFunc(ob, arg);
        }

        /// <summary>
        /// Wrapper for calling tp_clear
        /// </summary>
        internal void CallTypeClear()
        {
            if (tpHandle == IntPtr.Zero || pyHandle == IntPtr.Zero)
            {
                return;
            }

            var clearPtr = Runtime.PyType_GetSlot(TypeReference, TypeSlotID.tp_clear);
            if (clearPtr == IntPtr.Zero)
            {
                return;
            }
            var clearFunc = NativeCall.GetDelegate<Interop.InquiryFunc>(clearPtr);
            clearFunc(pyHandle);
        }

        /// <summary>
        /// Wrapper for calling tp_traverse
        /// </summary>
        internal void CallTypeTraverse(Interop.ObjObjFunc visitproc, IntPtr arg)
        {
            if (tpHandle == IntPtr.Zero || pyHandle == IntPtr.Zero)
            {
                return;
            }
            var traversePtr = Runtime.PyType_GetSlot(TypeReference, TypeSlotID.tp_traverse);
            if (traversePtr == IntPtr.Zero)
            {
                return;
            }
            var traverseFunc = NativeCall.GetDelegate<Interop.ObjObjArgFunc>(traversePtr);

            var visiPtr = Marshal.GetFunctionPointerForDelegate(visitproc);
            traverseFunc(pyHandle, visiPtr, arg);
        }

        protected void TypeClear()
        {
            ClearObjectDict(pyHandle);
        }

        internal void Save(InterDomainContext context)
        {
            OnSave(context);
        }

        internal void Load(InterDomainContext context)
        {
            OnLoad(context);
        }

        protected virtual void OnSave(InterDomainContext context) { }
        protected virtual void OnLoad(InterDomainContext context) { }

        protected static void ClearObjectDict(IntPtr ob)
        {
            IntPtr dict = GetObjectDict(ob);
            if (dict == IntPtr.Zero)
            {
                return;
            }
            SetObjectDict(ob, IntPtr.Zero);
            Runtime.XDecref(dict);
        }

        protected static IntPtr GetObjectDict(IntPtr ob)
        {
            IntPtr type = Runtime.PyObject_TYPE(ob);
            int instanceDictOffset = Marshal.ReadInt32(type, TypeOffset.tp_dictoffset);
            Debug.Assert(instanceDictOffset > 0);
            return Marshal.ReadIntPtr(ob, instanceDictOffset);
        }

        protected static void SetObjectDict(IntPtr ob, IntPtr value)
        {
            IntPtr type = Runtime.PyObject_TYPE(ob);
            int instanceDictOffset = Marshal.ReadInt32(type, TypeOffset.tp_dictoffset);
            Debug.Assert(instanceDictOffset > 0);
            Marshal.WriteIntPtr(ob, instanceDictOffset, value);
        }

        internal static void GetGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type, out IntPtr handle)
        {
            Debug.Assert(reflectedClrObject != null);
            Debug.Assert(IsManagedType(type) || type == Runtime.CLRMetaType);
            Debug.Assert(Runtime.PyObject_TypeCheck(reflectedClrObject, type));

            int gcHandleOffset = Marshal.ReadInt32(type.DangerousGetAddress(), Offsets.tp_clr_inst_offset);
            Debug.Assert(gcHandleOffset > 0);

            handle = Marshal.ReadIntPtr(reflectedClrObject.DangerousGetAddress(), gcHandleOffset);
        }

        internal static GCHandle? TryGetGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type)
        {
            GetGCHandle(reflectedClrObject, type, out IntPtr handle);
            return handle == IntPtr.Zero ? null : (GCHandle)handle;
        }
        internal static GCHandle? TryGetGCHandle(BorrowedReference reflectedClrObject)
        {
            BorrowedReference reflectedType = Runtime.PyObject_TYPE(reflectedClrObject);

            return TryGetGCHandle(reflectedClrObject, reflectedType);
        }

        internal static GCHandle GetGCHandle(BorrowedReference reflectedClrObject)
            => TryGetGCHandle(reflectedClrObject) ?? throw new InvalidOperationException();
        internal static GCHandle GetGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type)
            => TryGetGCHandle(reflectedClrObject, type) ?? throw new InvalidOperationException();

        internal static void InitGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type, GCHandle handle)
        {
            Debug.Assert(TryGetGCHandle(reflectedClrObject) == null);

            SetGCHandle(reflectedClrObject, type: type, handle);
        }
        internal static void InitGCHandle(BorrowedReference reflectedClrObject, GCHandle handle)
            => InitGCHandle(reflectedClrObject, Runtime.PyObject_TYPE(reflectedClrObject), handle);

        internal static void SetGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type, GCHandle newHandle)
        {
            Debug.Assert(Runtime.PyObject_TypeCheck(reflectedClrObject, type));

            int offset = Marshal.ReadInt32(type.DangerousGetAddress(), Offsets.tp_clr_inst_offset);
            Debug.Assert(offset > 0);

            Marshal.WriteIntPtr(reflectedClrObject.DangerousGetAddress(), offset, (IntPtr)newHandle);
        }
        internal static void SetGCHandle(BorrowedReference reflectedClrObject, GCHandle newHandle)
            => SetGCHandle(reflectedClrObject, Runtime.PyObject_TYPE(reflectedClrObject), newHandle);

        internal static class Offsets
        {
            static Offsets()
            {
                int pyTypeSize = Marshal.ReadInt32(Runtime.PyTypeType, TypeOffset.tp_basicsize);
                if (pyTypeSize < 0) throw new InvalidOperationException();

                tp_clr_inst_offset = pyTypeSize;
                tp_clr_inst = tp_clr_inst_offset + IntPtr.Size;
            }
            public static int tp_clr_inst_offset { get; }
            public static int tp_clr_inst { get;  }
        }
    }
}
