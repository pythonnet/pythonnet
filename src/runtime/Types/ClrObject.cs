using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    [Serializable]
    [DebuggerDisplay("clrO: {inst}")]
    internal sealed class CLRObject : ManagedType
    {
        internal readonly object inst;

        internal static bool creationBlocked = false;

        // "borrowed" references
        internal static readonly HashSet<IntPtr> reflectedObjects = new();
        static NewReference Create(object ob, BorrowedReference tp)
        {
            if (creationBlocked)
                throw new InvalidOperationException("Reflected objects should not be created anymore.");

            Debug.Assert(tp != null);
            var py = Runtime.PyType_GenericAlloc(tp, 0);

            var self = new CLRObject(ob);

            GCHandle gc = GCHandle.Alloc(self);
            InitGCHandle(py.Borrow(), type: tp, gc);

            bool isNew = reflectedObjects.Add(py.DangerousGetAddress());
            Debug.Assert(isNew);

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            if (ob is Exception e) Exceptions.SetArgsAndCause(py.Borrow(), e);

            return py;
        }

        CLRObject(object inst)
        {
            this.inst = inst;
        }

        internal static NewReference GetReference(object ob, BorrowedReference pyType)
            => Create(ob, pyType);

        internal static NewReference GetReference(object ob, Type type)
        {
            BorrowedReference cc = ClassManager.GetClass(type);
            return Create(ob, cc);
        }

        internal static NewReference GetReference(object ob)
        {
            BorrowedReference cc = ClassManager.GetClass(ob.GetType());
            return Create(ob, cc);
        }

        internal static void Restore(object ob, BorrowedReference pyHandle, Dictionary<string, object?> context)
        {
            var co = new CLRObject(ob);
            co.OnLoad(pyHandle, context);
        }

        protected override void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            if (creationBlocked)
                throw new InvalidOperationException("Reflected objects should not be loaded anymore.");

            base.OnLoad(ob, context);
            GCHandle gc = GCHandle.Alloc(this);
            SetGCHandle(ob, gc);

            bool isNew = reflectedObjects.Add(ob.DangerousGetAddress());
            Debug.Assert(isNew);
        }
    }
}
