using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Base class for extensions whose instances *share* a single Python
    /// type object, such as the types that represent CLR methods, fields,
    /// etc. Instances implemented by this class do not support sub-typing.
    /// </summary>
    [Serializable]
    internal abstract class ExtensionType : ManagedType
    {
        public virtual NewReference Alloc()
        {
            // Create a new PyObject whose type is a generated type that is
            // implemented by the particular concrete ExtensionType subclass.
            // The Python instance object is related to an instance of a
            // particular concrete subclass with a hidden CLR gchandle.

            BorrowedReference tp = TypeManager.GetTypeReference(GetType());

            //int rc = (int)Util.ReadIntPtr(tp, TypeOffset.ob_refcnt);
            //if (rc > 1050)
            //{
            //    DebugUtil.Print("tp is: ", tp);
            //    DebugUtil.DumpType(tp);
            //}

            NewReference py = Runtime.PyType_GenericAlloc(tp, 0);

#if DEBUG
            GetGCHandle(py.BorrowOrThrow(), tp, out var existing);
            System.Diagnostics.Debug.Assert(existing == IntPtr.Zero);
#endif
            SetupGc(py.Borrow(), tp);

            return py;
        }

        public PyObject AllocObject() => new PyObject(Alloc().Steal());

        // "borrowed" references
        internal static readonly HashSet<IntPtr> loadedExtensions = new();
        void SetupGc (BorrowedReference ob, BorrowedReference tp)
        {
            GCHandle gc = GCHandle.Alloc(this);
            InitGCHandle(ob, tp, gc);

            bool isNew = loadedExtensions.Add(ob.DangerousGetAddress());
            Debug.Assert(isNew);

            // We have to support gc because the type machinery makes it very
            // hard not to - but we really don't have a need for it in most
            // concrete extension types, so untrack the object to save calls
            // from Python into the managed runtime that are pure overhead.

            Runtime.PyObject_GC_UnTrack(ob);
        }

        /// <summary>
        /// Type __setattr__ implementation.
        /// </summary>
        public static int tp_setattro(BorrowedReference ob, BorrowedReference key, BorrowedReference val)
        {
            var message = "type does not support setting attributes";
            if (val == null)
            {
                message = "readonly attribute";
            }
            Exceptions.SetError(Exceptions.AttributeError, message);
            return -1;
        }

        public unsafe static void tp_dealloc(NewReference lastRef)
        {
            Runtime.PyObject_GC_UnTrack(lastRef.Borrow());

            tp_clear(lastRef.Borrow());

            // we must decref our type: https://docs.python.org/3/c-api/typeobj.html#c.PyTypeObject.tp_dealloc
            DecrefTypeAndFree(lastRef.Steal());
        }

        public static int tp_clear(BorrowedReference ob)
        {
            var weakrefs = Runtime.PyObject_GetWeakRefList(ob);
            if (weakrefs != null)
            {
                Runtime.PyObject_ClearWeakRefs(ob);
            }

            if (TryFreeGCHandle(ob))
            {
                bool deleted = loadedExtensions.Remove(ob.DangerousGetAddress());
                Debug.Assert(deleted);
            }

            int res = ClassBase.BaseUnmanagedClear(ob);
            return res;
        }

        protected override void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            base.OnLoad(ob, context);
            SetupGc(ob, Runtime.PyObject_TYPE(ob));
        }
    }
}
