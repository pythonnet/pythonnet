using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    [Serializable]
    [DebuggerDisplay("clrO: {inst}")]
    internal class CLRObject : ManagedType
    {
        internal object inst;

        internal CLRObject(object ob, PyType tp)
        {
            Debug.Assert(tp != null);
            using var py = Runtime.PyType_GenericAlloc(tp, 0);

            tpHandle = tp;
            pyHandle = py.MoveToPyObject();
            inst = ob;

            GCHandle gc = AllocGCHandle(TrackTypes.Wrapper);
            InitGCHandle(ObjectReference, type: TypeReference, gc);

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            if (ob is Exception e) Exceptions.SetArgsAndCause(ObjectReference, e);
        }

        protected CLRObject()
        {
        }

        static CLRObject GetInstance(object ob, PyType pyType)
        {
            return new CLRObject(ob, pyType);
        }


        static CLRObject GetInstance(object ob)
        {
            ClassBase cc = ClassManager.GetClass(ob.GetType());
            return GetInstance(ob, cc.tpHandle);
        }

        internal static NewReference GetReference(object ob, BorrowedReference pyType)
        {
            CLRObject co = GetInstance(ob, new PyType(pyType));
            return new NewReference(co.pyHandle);
        }

        internal static NewReference GetReference(object ob, Type type)
        {
            ClassBase cc = ClassManager.GetClass(type);
            CLRObject co = GetInstance(ob, cc.tpHandle);
            return new NewReference(co.pyHandle);
        }


        internal static NewReference GetReference(object ob)
        {
            CLRObject co = GetInstance(ob);
            return new NewReference(co.pyHandle);
        }

        internal static CLRObject Restore(object ob, BorrowedReference pyHandle, InterDomainContext context)
        {
            var pyObj = new PyObject(pyHandle);
            CLRObject co = new CLRObject()
            {
                inst = ob,
                pyHandle = pyObj,
                tpHandle = pyObj.GetPythonType(),
            };
            Debug.Assert(co.tpHandle != null);
            co.Load(context);
            return co;
        }

        protected override void OnSave(InterDomainContext context)
        {
            base.OnSave(context);
            Runtime.XIncref(pyHandle);
        }

        protected override void OnLoad(InterDomainContext context)
        {
            base.OnLoad(context);
            GCHandle gc = AllocGCHandle(TrackTypes.Wrapper);
            SetGCHandle(ObjectReference, TypeReference, gc);
        }
    }
}
