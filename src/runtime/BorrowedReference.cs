namespace Python.Runtime
{
    using System;
    readonly ref struct BorrowedReference
    {
        public readonly IntPtr Pointer;
        public bool IsNull => this.Pointer == IntPtr.Zero;

        public PyObject ToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            Runtime.XIncref(this.Pointer);
            return new PyObject(this.Pointer);
        }

        [Obsolete("Use overloads, that take BorrowedReference or NewReference")]
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.Pointer;

        BorrowedReference(IntPtr pointer)
        {
            this.Pointer = pointer;
        }
    }
}
