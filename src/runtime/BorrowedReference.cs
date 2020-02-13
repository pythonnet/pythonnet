namespace Python.Runtime
{
    using System;
    [NonCopyable]
    ref struct BorrowedReference
    {
        public IntPtr Pointer;
        public bool IsNull => this.Pointer == IntPtr.Zero;

        public PyObject ToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            Runtime.XIncref(this.Pointer);
            return new PyObject(this.Pointer);
        }
    }

    static class BorrowedReferenceExtensions {
        [Obsolete("Use overloads, that take BorrowedReference or NewReference")]
        public static IntPtr DangerousGetAddress(this in BorrowedReference reference)
            => reference.IsNull() ? throw new NullReferenceException() : reference.Pointer;
        public static bool IsNull(this in BorrowedReference reference)
            => reference.Pointer == IntPtr.Zero;
    }
}
