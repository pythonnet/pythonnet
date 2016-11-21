using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    internal class CLRObject : ManagedType
    {
        internal Object inst;

        internal CLRObject(Object ob, IntPtr tp) : base()
        {
            IntPtr py = Runtime.PyType_GenericAlloc(tp, 0);

            int flags = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_flags);
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
            this.tpHandle = tp;
            this.pyHandle = py;
            this.gcHandle = gc;
            inst = ob;

            // Fix the BaseException args (and __cause__ in case of Python 3)
            // slot if wrapping a CLR exception
            Exceptions.SetArgsAndCause(py);
        }


        internal static CLRObject GetInstance(Object ob, IntPtr pyType)
        {
            return new CLRObject(ob, pyType);
        }


        internal static CLRObject GetInstance(Object ob)
        {
            ClassBase cc = ClassManager.GetClass(ob.GetType());
            return GetInstance(ob, cc.tpHandle);
        }


        internal static IntPtr GetInstHandle(Object ob, IntPtr pyType)
        {
            CLRObject co = GetInstance(ob, pyType);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(Object ob, Type type)
        {
            ClassBase cc = ClassManager.GetClass(type);
            CLRObject co = GetInstance(ob, cc.tpHandle);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(Object ob)
        {
            CLRObject co = GetInstance(ob);
            return co.pyHandle;
        }
    }
}
