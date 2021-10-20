using System;
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
        public ExtensionType()
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

            // Borrowed reference. Valid as long as pyHandle is valid.
            tpHandle = new PyType(tp, prevalidated: true);
            pyHandle = py.MoveToPyObject();

#if DEBUG
            GetGCHandle(ObjectReference, TypeReference, out var existing);
            System.Diagnostics.Debug.Assert(existing == IntPtr.Zero);
#endif
            SetupGc();
        }

        void SetupGc ()
        {
            GCHandle gc = AllocGCHandle(TrackTypes.Extension);
            InitGCHandle(ObjectReference, TypeReference, gc);

            // We have to support gc because the type machinery makes it very
            // hard not to - but we really don't have a need for it in most
            // concrete extension types, so untrack the object to save calls
            // from Python into the managed runtime that are pure overhead.

            Runtime.PyObject_GC_UnTrack(pyHandle);
        }


        protected virtual void Dealloc(NewReference lastRef)
        {
            var type = Runtime.PyObject_TYPE(lastRef.Borrow());
            Runtime.PyObject_GC_Del(lastRef.Steal());

            this.FreeGCHandle();

            // we must decref our type: https://docs.python.org/3/c-api/typeobj.html#c.PyTypeObject.tp_dealloc
            Runtime.XDecref(StolenReference.DangerousFromPointer(type.DangerousGetAddress()));
        }

        /// <summary>DecRefs and nulls any fields pointing back to Python</summary>
        protected virtual void Clear(BorrowedReference ob)
        {
            if (this.pyHandle?.IsDisposed == false)
            {
                ClearObjectDict(this.ObjectReference);
            }
            // Not necessary for decref of `tpHandle` - it is borrowed
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

        public static void tp_dealloc(NewReference lastRef)
        {
            // Clean up a Python instance of this extension type. This
            // frees the allocated Python object and decrefs the type.
            var self = (ExtensionType?)GetManagedObject(lastRef.Borrow());
            self?.Clear(lastRef.Borrow());
            self?.Dealloc(lastRef.AnalyzerWorkaround());
        }

        public static int tp_clear(BorrowedReference ob)
        {
            var self = (ExtensionType?)GetManagedObject(ob);
            self?.Clear(ob);
            return 0;
        }

        protected override void OnLoad(InterDomainContext context)
        {
            base.OnLoad(context);
            SetupGc();
        }
    }
}
