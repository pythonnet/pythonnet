namespace Python.Runtime
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents a reference to a Python object, that is being lent, and
    /// can only be safely used until execution returns to the caller.
    /// </summary>
    readonly ref struct BorrowedReference
    {
        readonly IntPtr pointer;
        public bool IsNull => this.pointer == IntPtr.Zero;

        /// <summary>Gets a raw pointer to the Python object</summary>
        [DebuggerHidden]
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.pointer;
        /// <summary>Gets a raw pointer to the Python object</summary>
        public IntPtr DangerousGetAddressOrNull() => this.pointer;

        public static BorrowedReference Null => new BorrowedReference();

        /// <summary>
        /// Creates new instance of <see cref="BorrowedReference"/> from raw pointer. Unsafe.
        /// </summary>
        public BorrowedReference(IntPtr pointer)
        {
            this.pointer = pointer;
        }

        public static bool operator ==(BorrowedReference a, BorrowedReference b)
            => a.pointer == b.pointer;
        public static bool operator !=(BorrowedReference a, BorrowedReference b)
            => a.pointer != b.pointer;
        public static bool operator ==(BorrowedReference reference, NullOnly? @null)
            => reference.IsNull;
        public static bool operator !=(BorrowedReference reference, NullOnly? @null)
            => !reference.IsNull;
        public static bool operator ==(NullOnly? @null, BorrowedReference reference)
            => reference.IsNull;
        public static bool operator !=(NullOnly? @null, BorrowedReference reference)
            => !reference.IsNull;

        public override bool Equals(object obj) {
            if (obj is IntPtr ptr)
                return ptr == pointer;

            return false;
        }

        public static implicit operator BorrowedReference(PyObject pyObject) => pyObject.Reference;
        public static implicit operator BorrowedReference(NullOnly? @null) => Null;

        public override int GetHashCode() => pointer.GetHashCode();
    }
}
