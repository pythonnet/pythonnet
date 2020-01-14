using System;

namespace Python.Runtime
{
    /// <summary>
    /// A MethodWrapper wraps a static method of a managed type,
    /// making it callable by Python as a PyCFunction object. This is
    /// currently used mainly to implement special cases like the CLR
    /// import hook.
    /// </summary>
    internal class MethodWrapper
    {
        public IntPtr mdef;
        public IntPtr ptr;
        private bool _disposed = false;

        private ThunkInfo _thunk;

        public MethodWrapper(Type type, string name, string funcType = null)
        {
            // Turn the managed method into a function pointer

            _thunk = Interop.GetThunk(type.GetMethod(name), funcType);

            // Allocate and initialize a PyMethodDef structure to represent
            // the managed method, then create a PyCFunction.

            mdef = Runtime.PyMem_Malloc(4 * IntPtr.Size);
            TypeManager.WriteMethodDef(mdef, name, _thunk.Address, 0x0003);
            ptr = Runtime.PyCFunction_NewEx(mdef, IntPtr.Zero, IntPtr.Zero);
        }

        public IntPtr Call(IntPtr args, IntPtr kw)
        {
            return Runtime.PyCFunction_Call(ptr, args, kw);
        }

        public void Release()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            bool freeDef = Runtime.Refcount(ptr) == 1;
            Runtime.XDecref(ptr);
            if (freeDef && mdef != IntPtr.Zero)
            {
                Runtime.PyMem_Free(mdef);
                mdef = IntPtr.Zero;
            }
        }
    }
}
