namespace Python.Runtime
{
    using System;
    [NonCopyable]
    ref struct NewReference
    {
        public IntPtr Pointer { get; set; }
        public bool IsNull => this.Pointer == IntPtr.Zero;

        public PyObject ToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            var result = new PyObject(this.Pointer);
            this.Pointer = IntPtr.Zero;
            return result;
        }

        public void Dispose()
        {
            if (!this.IsNull)
                Runtime.XDecref(this.Pointer);
            this.Pointer = IntPtr.Zero;
        }
    }
}
