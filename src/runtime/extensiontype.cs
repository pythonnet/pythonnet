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

            //int rc = (int)Marshal.ReadIntPtr(tp, TypeOffset.ob_refcnt);
            //if (rc > 1050)
            //{
            //    DebugUtil.Print("tp is: ", tp);
            //    DebugUtil.DumpType(tp);
            //}

            NewReference py = Runtime.PyType_GenericAlloc(tp, 0);

            // Borrowed reference. Valid as long as pyHandle is valid.
            tpHandle = tp.DangerousGetAddress();
            pyHandle = py.DangerousMoveToPointer();

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


        protected virtual void Dealloc()
        {
            var type = Runtime.PyObject_TYPE(this.ObjectReference);
            Runtime.PyObject_GC_Del(this.pyHandle);
            // Not necessary for decref of `tpHandle` - it is borrowed

            this.FreeGCHandle();

            // we must decref our type: https://docs.python.org/3/c-api/typeobj.html#c.PyTypeObject.tp_dealloc
            Runtime.XDecref(type.DangerousGetAddress());
        }

        /// <summary>DecRefs and nulls any fields pointing back to Python</summary>
        protected virtual void Clear()
        {
            ClearObjectDict(this.pyHandle);
            // Not necessary for decref of `tpHandle` - it is borrowed
        }

        /// <summary>
        /// Type __setattr__ implementation.
        /// </summary>
        public static int tp_setattro(IntPtr ob, IntPtr key, IntPtr val)
        {
            var message = "type does not support setting attributes";
            if (val == IntPtr.Zero)
            {
                message = "readonly attribute";
            }
            Exceptions.SetError(Exceptions.AttributeError, message);
            return -1;
        }


        /// <summary>
        /// Default __set__ implementation - this prevents descriptor instances
        /// being silently replaced in a type __dict__ by default __setattr__.
        /// </summary>
        public static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val)
        {
            Exceptions.SetError(Exceptions.AttributeError, "attribute is read-only");
            return -1;
        }


        public static void tp_dealloc(IntPtr ob)
        {
            // Clean up a Python instance of this extension type. This
            // frees the allocated Python object and decrefs the type.
            var self = (ExtensionType)GetManagedObject(ob);
            self?.Clear();
            self?.Dealloc();
        }

        public static int tp_clear(IntPtr ob)
        {
            var self = (ExtensionType)GetManagedObject(ob);
            self?.Clear();
            return 0;
        }

        protected override void OnLoad(InterDomainContext context)
        {
            base.OnLoad(context);
            SetupGc();
        }
    }
}
