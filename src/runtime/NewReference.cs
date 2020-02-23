namespace Python.Runtime
{
    using System;
    [NonCopyable]
    ref struct NewReference
    {
        IntPtr pointer;
        public bool IsNull => this.pointer == IntPtr.Zero;

        /// <summary>Gets a raw pointer to the Python object</summary>
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.pointer;

        public PyObject MoveToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            var result = new PyObject(this.pointer);
            this.pointer = IntPtr.Zero;
            return result;
        }

        public void Dispose()
        {
            if (!this.IsNull)
                Runtime.XDecref(this.pointer);
            this.pointer = IntPtr.Zero;
        }
    }
}
