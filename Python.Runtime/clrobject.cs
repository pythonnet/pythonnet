using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    internal class CLRObject : ManagedType
    {
        internal object inst;

        internal CLRObject(object ob, IntPtr tp)
        {
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

            GCHandle gc = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(py, ObjectOffset.magic(tp), (IntPtr)gc);
            tpHandle = tp;
            pyHandle = py;
            gcHandle = gc;
            inst = ob;

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            Exceptions.SetArgsAndCause(py);
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
    }
}
