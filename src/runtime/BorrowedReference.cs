namespace Python.Runtime
{
    using System;
    readonly ref struct BorrowedReference
    {
        readonly IntPtr pointer;
        public bool IsNull => this.pointer == IntPtr.Zero;

        public PyObject ToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            Runtime.XIncref(this.pointer);
            return new PyObject(this.pointer);
        }

        [Obsolete("Use overloads, that take BorrowedReference or NewReference")]
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.pointer;

        BorrowedReference(IntPtr pointer)
        {
            this.pointer = pointer;
        }
    }
}
