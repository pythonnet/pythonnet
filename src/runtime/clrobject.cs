using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    [Serializable]
    internal class CLRObject : ManagedType
    {
        internal object inst;

        internal CLRObject(object ob, IntPtr tp)
        {
            System.Diagnostics.Debug.Assert(tp != IntPtr.Zero);
            IntPtr py = Runtime.PyType_GenericAlloc(tp, 0);

            long flags = Util.ReadCLong(tp, TypeOffset.tp_flags);
            if ((flags & TypeFlags.Subclass) != 0)
            {
                IntPtr dict = Marshal.ReadIntPtr(py, ObjectOffset.DictOffset(tp));
                if (dict == IntPtr.Zero)
                {
                    dict = Runtime.PyDict_New();
                    Marshal.WriteIntPtr(py, ObjectOffset.DictOffset(tp), dict);
                }
            }

            GCHandle gc = AllocGCHandle(TrackTypes.Wrapper);
            Marshal.WriteIntPtr(py, ObjectOffset.magic(tp), (IntPtr)gc);
            tpHandle = tp;
            pyHandle = py;
            inst = ob;

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            Exceptions.SetArgsAndCause(py);
        }

        protected CLRObject()
        {
        }


        internal static CLRObject GetInstance(object ob, IntPtr pyType)
        {
            return new CLRObject(ob, pyType);
        }


        internal static CLRObject GetInstance(object ob)
        {
            ClassBase cc = ClassManager.GetClass(ob.GetType());
            return GetInstance(ob, cc.tpHandle);
        }


        internal static IntPtr GetInstHandle(object ob, IntPtr pyType)
        {
            CLRObject co = GetInstance(ob, pyType);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(object ob, Type type)
        {
            ClassBase cc = ClassManager.GetClass(type);
            CLRObject co = GetInstance(ob, cc.tpHandle);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(object ob)
        {
            CLRObject co = GetInstance(ob);
            return co.pyHandle;
        }

        /// <summary>
        /// Creates <see cref="CLRObject"/> proxy for the given object,
        /// and returns a <see cref="NewReference"/> to it.
        /// </summary>
        internal static NewReference MakeNewReference(object obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));

            // TODO: CLRObject currently does not have Dispose or finalizer which might change in the future
            return NewReference.DangerousFromPointer(GetInstHandle(obj));
        }

        internal static CLRObject Restore(object ob, IntPtr pyHandle, PyObjectSerializeContext context)
        {
            CLRObject co = new CLRObject()
            {
                inst = ob,
                pyHandle = pyHandle,
                tpHandle = Runtime.PyObject_TYPE(pyHandle)
            };
            co.Load(context);
            return co;
        }

        protected override void OnSave(PyObjectSerializeContext context)
        {
            base.OnSave(context);
            Runtime.XIncref(pyHandle);
        }

        protected override void OnLoad(PyObjectSerializeContext context)
        {
            base.OnLoad(context);
            GCHandle gc = AllocGCHandle(TrackTypes.Wrapper);
            Marshal.WriteIntPtr(pyHandle, ObjectOffset.magic(tpHandle), (IntPtr)gc);
        }
    }
}
