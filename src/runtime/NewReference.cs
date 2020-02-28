namespace Python.Runtime
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Represents a reference to a Python object, that is tracked by Python's reference counting.
    /// </summary>
    [NonCopyable]
    ref struct NewReference
    {
        IntPtr pointer;

        /// <summary>
        /// Returns <see cref="PyObject"/> wrapper around this reference, which now owns
        /// the pointer. Sets the original reference to <c>null</c>, as it no longer owns it.
        /// </summary>
        public PyObject MoveToPyObject()
        {
            if (this.IsNull()) throw new NullReferenceException();

            var result = new PyObject(this.pointer);
            this.pointer = IntPtr.Zero;
            return result;
        }
        /// <summary>
        /// Removes this reference to a Python object, and sets it to <c>null</c>.
        /// </summary>
        public void Dispose()
        {
            if (!this.IsNull())
                Runtime.XDecref(this.pointer);
            this.pointer = IntPtr.Zero;
        }

        [Pure]
        internal static IntPtr DangerousGetAddress(in NewReference reference)
            => IsNull(reference) ? throw new NullReferenceException() : reference.pointer;
        [Pure]
        internal static bool IsNull(in NewReference reference)
            => reference.pointer == IntPtr.Zero;
    }

    /// <summary>
    /// These members can not be directly in <see cref="NewReference"/> type,
    /// because <c>this</c> is always passed by value, which we need to avoid.
    /// (note <code>this in NewReference</code> vs the usual <code>this NewReference</code>)
    /// </summary>
    static class NewReferenceExtensions
    {
        /// <summary>Gets a raw pointer to the Python object</summary>
        [Pure]
        public static IntPtr DangerousGetAddress(this in NewReference reference)
            => NewReference.DangerousGetAddress(reference);
        [Pure]
        public static bool IsNull(this in NewReference reference)
            => NewReference.IsNull(reference);
    }
}
