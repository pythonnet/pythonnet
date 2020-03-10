using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    struct PyHandle : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PyObjectStruct
        {
#if PYTHON_WITH_PYDEBUG
            public IntPtr _ob_next;
            public IntPtr _ob_prev;
#endif
            public IntPtr ob_refcnt;
            public IntPtr ob_type;
            public IntPtr ob_dict;
            public IntPtr ob_data;
        }

        public static readonly PyHandle Null = new PyHandle(IntPtr.Zero);

        private IntPtr _handle;

        public unsafe long RefCount
        {
            get
            {
                return (long)((PyObjectStruct*)_handle)->ob_refcnt;
            }
            set
            {
                ((PyObjectStruct*)_handle)->ob_refcnt = new IntPtr(value);
            }
        }

        public PyHandle(IntPtr op)
        {
            _handle = op;
        }

        public PyHandle(long op)
        {
            _handle = new IntPtr(op);
        }

        public unsafe PyHandle(void* op)
        {
            _handle = (IntPtr)op;
        }

        public override string ToString()
        {
            // Make the PyHandle be more readable for printing or debugging.
#if !PYTHON2
            // Check GIL directly make sure PyHandle can get a string description
            // when it didn't hold the GIL.
            if (!Runtime.PyGILState_Check())
            {
                return $"<object at {_handle}>";
            }
            var s = Runtime.PyObject_Str(_handle);
            PythonException.ThrowIfIsNull(s);
            try
            {
                return Runtime.GetManagedString(s);
            }
            finally
            {
                Runtime.XDecref(s);
            }
#else
            // Pytyhon2 didn't has PyGILState_Check, always print its pointer only.
            return $"<object at {_handle}>";
#endif
        }

        public void XIncref()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }
            IncrefInternal();
        }

        public void Incref()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new NullReferenceException();
            }
            IncrefInternal();
        }

        public void XDecref()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }
            DecrefInternal();
        }

        public void Decref()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new NullReferenceException();
            }
            DecrefInternal();
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }
            DecrefInternal();
            _handle = IntPtr.Zero;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PyHandle))
            {
                return false;
            }
            return _handle == ((PyHandle)obj)._handle;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        private unsafe void IncrefInternal()
        {
            ((PyObjectStruct*)_handle)->ob_refcnt = ((PyObjectStruct*)_handle)->ob_refcnt + 1;
        }

        private unsafe void DecrefInternal()
        {
            var p = (PyObjectStruct*)_handle;
            p->ob_refcnt = p->ob_refcnt - 1;
            if (p->ob_refcnt == IntPtr.Zero)
            {
                IntPtr tp_dealloc = Marshal.ReadIntPtr(p->ob_type, TypeOffset.tp_dealloc);
                if (tp_dealloc == IntPtr.Zero)
                {
                    return;
                }
                NativeCall.Void_Call_1(tp_dealloc, _handle);
            }
        }

        public static bool operator ==(PyHandle a, PyHandle b) => a._handle == b._handle;
        public static bool operator !=(PyHandle a, PyHandle b) => a._handle != b._handle;
        public static bool operator ==(PyHandle a, IntPtr ptr) => a._handle == ptr;
        public static bool operator !=(PyHandle a, IntPtr ptr) => a._handle != ptr;

        public static unsafe explicit operator void*(PyHandle handle) => (void*)handle._handle;
        public static implicit operator IntPtr(PyHandle handle) => handle._handle;
        public static implicit operator PyHandle(IntPtr op) => new PyHandle(op);

        public static implicit operator PyHandle(BorrowedReference reference)
            => new PyHandle(reference.DangerousGetAddress());
        public static implicit operator PyHandle(NewReference reference)
            => new PyHandle(reference.DangerousGetAddress());
    }
}
