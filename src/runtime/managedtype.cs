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
        internal static ManagedType GetManagedObject(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                var flags = Util.ReadCLong(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.Managed) != 0)
                {
                    IntPtr op = tp == ob
                        ? Marshal.ReadIntPtr(tp, TypeOffset.magic())
                        : Marshal.ReadIntPtr(ob, ObjectOffset.magic(tp));
                    if (op == IntPtr.Zero)
                    {
                        return null;
                    }
                    var gc = (GCHandle)op;
                    return (ManagedType)gc.Target;
                }
            }
            return null;
        }

        /// <summary>
        /// Given a Python object, return the associated managed object type or null.
        /// </summary>
        internal static ManagedType GetManagedObjectType(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                var flags = Util.ReadCLong(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.Managed) != 0)
                {
                    tp = Marshal.ReadIntPtr(tp, TypeOffset.magic());
                    var gc = (GCHandle)tp;
                    return (ManagedType)gc.Target;
                }
            }
            return null;
        }


        internal static ManagedType GetManagedObjectErr(IntPtr ob)
        {
            ManagedType result = GetManagedObject(ob);
            if (result == null)
            {
                Exceptions.SetError(Exceptions.TypeError, "invalid argument, expected CLR type");
            }
            return result;
        }


        internal static bool IsManagedType(IntPtr ob)
        {
            if (ob != IntPtr.Zero)
            {
                IntPtr tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                var flags = Util.ReadCLong(tp, TypeOffset.tp_flags);
                if ((flags & TypeFlags.Managed) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsTypeObject()
        {
            return pyHandle == tpHandle;
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
            var visitFunc = (Interop.ObjObjFunc)Marshal.GetDelegateForFunctionPointer(visit, typeof(Interop.ObjObjFunc));
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
            var clearPtr = Marshal.ReadIntPtr(tpHandle, TypeOffset.tp_clear);
            if (clearPtr == IntPtr.Zero)
            {
                return;
            }
            var clearFunc = (Interop.InquiryFunc)Marshal.GetDelegateForFunctionPointer(clearPtr, typeof(Interop.InquiryFunc));
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
            var traversePtr = Marshal.ReadIntPtr(tpHandle, TypeOffset.tp_traverse);
            if (traversePtr == IntPtr.Zero)
            {
                return;
            }
            var traverseFunc = (Interop.ObjObjArgFunc)Marshal.GetDelegateForFunctionPointer(traversePtr, typeof(Interop.ObjObjArgFunc));
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
            return Marshal.ReadIntPtr(ob, ObjectOffset.TypeDictOffset(type));
        }

        protected static void SetObjectDict(IntPtr ob, IntPtr value)
        {
            IntPtr type = Runtime.PyObject_TYPE(ob);
            Marshal.WriteIntPtr(ob, ObjectOffset.TypeDictOffset(type), value);
        }
    }
}
