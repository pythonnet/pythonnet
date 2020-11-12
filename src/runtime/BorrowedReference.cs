namespace Python.Runtime
{
    using System;
    /// <summary>
    /// Represents a reference to a Python object, that is being lent, and
    /// can only be safely used until execution returns to the caller.
    /// </summary>
    readonly ref struct BorrowedReference
    {
        readonly IntPtr pointer;
        public bool IsNull => this.pointer == IntPtr.Zero;


        /// <summary>Gets a raw pointer to the Python object. Performs null check</summary>
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.pointer;
        /// <summary>Gets a raw pointer to the Python object</summary>
        public IntPtr DangerousGetAddressOrNull() => this.pointer;

        /// <summary>
        /// Creates new instance of <see cref="BorrowedReference"/> from raw pointer. Unsafe.
        /// </summary>
        public BorrowedReference(IntPtr pointer)
        {
            this.pointer = pointer;
        }

        public static BorrowedReference Null => default;
    }
}
