namespace Python.Runtime
{
    using System;
    /// <summary>
    /// Represents a reference to a Python object, that is tracked by Python's reference counting.
    /// </summary>
    [NonCopyable]
    ref struct NewReference
    {
        IntPtr pointer;
        public bool IsNull => this.pointer == IntPtr.Zero;

        /// <summary>Gets a raw pointer to the Python object</summary>
        public IntPtr DangerousGetAddress()
            => this.IsNull ? throw new NullReferenceException() : this.pointer;

        /// <summary>
        /// Returns <see cref="PyObject"/> wrapper around this reference, which now owns
        /// the pointer. Sets the original reference to <c>null</c>, as it no longer owns it.
        /// </summary>
        public PyObject MoveToPyObject()
        {
            if (this.IsNull) throw new NullReferenceException();

            var result = new PyObject(this.pointer);
            this.pointer = IntPtr.Zero;
            return result;
        }
        /// <summary>
        /// Removes this reference to a Python object, and sets it to <c>null</c>.
        /// </summary>
        public void Dispose()
        {
            if (!this.IsNull)
                Runtime.XDecref(this.pointer);
            this.pointer = IntPtr.Zero;
        }
    }
}
