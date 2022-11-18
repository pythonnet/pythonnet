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
        /// <summary>
        /// Given a Python object, return the associated managed object or null.
        /// </summary>
        internal static ManagedType? GetManagedObject(BorrowedReference ob)
        {
            if (ob != null)
            {
                BorrowedReference tp = Runtime.PyObject_TYPE(ob);
                var flags = PyType.GetFlags(tp);
                if ((flags & TypeFlags.HasClrInstance) != 0)
                {
                    var gc = TryGetGCHandle(ob, tp);
                    return (ManagedType?)gc?.Target;
                }
            }
            return null;
        }

        internal static bool IsInstanceOfManagedType(BorrowedReference ob)
        {
            if (ob != null)
            {
                BorrowedReference tp = Runtime.PyObject_TYPE(ob);
                if (tp == Runtime.PyTypeType || tp == Runtime.PyCLRMetaType)
                {
                    tp = ob;
                }

                return IsManagedType(tp);
            }
            return false;
        }

        internal static bool IsManagedType(BorrowedReference type)
        {
            var flags = PyType.GetFlags(type);
            return (flags & TypeFlags.HasClrInstance) != 0;
        }

        internal static BorrowedReference GetUnmanagedBaseType(BorrowedReference managedType)
        {
            Debug.Assert(managedType != null && IsManagedType(managedType));
            do
            {
                managedType = PyType.GetBase(managedType);
                Debug.Assert(managedType != null);
            } while (IsManagedType(managedType));
            return managedType;
        }

        internal unsafe static int PyVisit(BorrowedReference ob, IntPtr visit, IntPtr arg)
        {
            if (ob == null)
            {
                return 0;
            }
            var visitFunc = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)(visit);
            return visitFunc(ob, arg);
        }

        internal static unsafe void DecrefTypeAndFree(StolenReference ob)
        {
            if (ob == null) throw new ArgumentNullException(nameof(ob));
            var borrowed = new BorrowedReference(ob.DangerousGetAddress());

            var type = Runtime.PyObject_TYPE(borrowed);

            var freePtr = Util.ReadIntPtr(type, TypeOffset.tp_free);
            Debug.Assert(freePtr != IntPtr.Zero);
            var free = (delegate* unmanaged[Cdecl]<StolenReference, void>)freePtr;
            free(ob);

            Runtime.XDecref(StolenReference.DangerousFromPointer(type.DangerousGetAddress()));
        }

        internal static int CallClear(BorrowedReference ob)
            => CallTypeClear(ob, Runtime.PyObject_TYPE(ob));

        /// <summary>
        /// Wrapper for calling tp_clear
        /// </summary>
        internal static unsafe int CallTypeClear(BorrowedReference ob, BorrowedReference tp)
        {
            if (ob == null) throw new ArgumentNullException(nameof(ob));
            if (tp == null) throw new ArgumentNullException(nameof(tp));

            var clearPtr = Util.ReadIntPtr(tp, TypeOffset.tp_clear);
            if (clearPtr == IntPtr.Zero)
            {
                return 0;
            }
            var clearFunc = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)clearPtr;
            return clearFunc(ob);
        }

        internal Dictionary<string, object?>? Save(BorrowedReference ob)
        {
            return OnSave(ob);
        }

        internal void Load(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            OnLoad(ob, context);
        }

        protected virtual Dictionary<string, object?>? OnSave(BorrowedReference ob) => null;
        protected virtual void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context) { }

        /// <summary>
        /// Initializes given object, or returns <c>false</c> and sets Python error on failure
        /// </summary>
        public virtual bool Init(BorrowedReference obj, BorrowedReference args, BorrowedReference kw)
        {
            // this just calls obj.__init__(*args, **kw)
            using var init = Runtime.PyObject_GetAttr(obj, PyIdentifier.__init__);
            Runtime.PyErr_Clear();

            if (!init.IsNull())
            {
                using var result = Runtime.PyObject_Call(init.Borrow(), args, kw);

                if (result.IsNull())
                {
                    return false;
                }
            }

            return true;
        }

        protected static void ClearObjectDict(BorrowedReference ob)
        {
            BorrowedReference type = Runtime.PyObject_TYPE(ob);
            int instanceDictOffset = Util.ReadInt32(type, TypeOffset.tp_dictoffset);
            // Debug.Assert(instanceDictOffset > 0);
            if (instanceDictOffset > 0)
                Runtime.Py_CLEAR(ob, instanceDictOffset);
        }

        protected static BorrowedReference GetObjectDict(BorrowedReference ob)
        {
            BorrowedReference type = Runtime.PyObject_TYPE(ob);
            int instanceDictOffset = Util.ReadInt32(type, TypeOffset.tp_dictoffset);
            Debug.Assert(instanceDictOffset > 0);
            return Util.ReadRef(ob, instanceDictOffset);
        }

        protected static void SetObjectDict(BorrowedReference ob, StolenReference value)
        {
            if (value.Pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(value));
            SetObjectDictNullable(ob, value.AnalyzerWorkaround());
        }
        protected static void SetObjectDictNullable(BorrowedReference ob, StolenReference value)
        {
            BorrowedReference type = Runtime.PyObject_TYPE(ob);
            int instanceDictOffset = Util.ReadInt32(type, TypeOffset.tp_dictoffset);
            Debug.Assert(instanceDictOffset > 0);
            Runtime.ReplaceReference(ob, instanceDictOffset, value.AnalyzerWorkaround());
        }

        internal static void GetGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type, out IntPtr handle)
        {
            Debug.Assert(reflectedClrObject != null);
            Debug.Assert(IsManagedType(type) || IsManagedType(reflectedClrObject));
            Debug.Assert(Runtime.PyObject_TypeCheck(reflectedClrObject, type));

            int gcHandleOffset = Util.ReadInt32(type, Offsets.tp_clr_inst_offset);
            Debug.Assert(gcHandleOffset > 0);

            handle = Util.ReadIntPtr(reflectedClrObject, gcHandleOffset);
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
            Debug.Assert(type != null);
            Debug.Assert(reflectedClrObject != null);
            Debug.Assert(IsManagedType(type) || IsManagedType(reflectedClrObject));
            Debug.Assert(Runtime.PyObject_TypeCheck(reflectedClrObject, type));

            int offset = Util.ReadInt32(type, Offsets.tp_clr_inst_offset);
            Debug.Assert(offset > 0);

            Util.WriteIntPtr(reflectedClrObject, offset, (IntPtr)newHandle);
        }
        internal static void SetGCHandle(BorrowedReference reflectedClrObject, GCHandle newHandle)
            => SetGCHandle(reflectedClrObject, Runtime.PyObject_TYPE(reflectedClrObject), newHandle);

        internal static bool TryFreeGCHandle(BorrowedReference reflectedClrObject)
            => TryFreeGCHandle(reflectedClrObject, Runtime.PyObject_TYPE(reflectedClrObject));

        internal static bool TryFreeGCHandle(BorrowedReference reflectedClrObject, BorrowedReference type)
        {
            Debug.Assert(type != null);
            Debug.Assert(reflectedClrObject != null);
            Debug.Assert(IsManagedType(type) || IsManagedType(reflectedClrObject));
            Debug.Assert(Runtime.PyObject_TypeCheck(reflectedClrObject, type));

            int offset = Util.ReadInt32(type, Offsets.tp_clr_inst_offset);
            Debug.Assert(offset > 0);

            IntPtr raw = Util.ReadIntPtr(reflectedClrObject, offset);
            if (raw == IntPtr.Zero) return false;

            var handle = (GCHandle)raw;
            handle.Free();

            Util.WriteIntPtr(reflectedClrObject, offset, IntPtr.Zero);
            return true;
        }

        internal static class Offsets
        {
            static Offsets()
            {
                int pyTypeSize = Util.ReadInt32(Runtime.PyTypeType, TypeOffset.tp_basicsize);
                if (pyTypeSize < 0) throw new InvalidOperationException();

                tp_clr_inst_offset = pyTypeSize;
                tp_clr_inst = tp_clr_inst_offset + IntPtr.Size;
            }
            public static int tp_clr_inst_offset { get; }
            public static int tp_clr_inst { get;  }
        }
    }
}
