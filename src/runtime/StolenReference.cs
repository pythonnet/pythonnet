namespace Python.Runtime
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Should only be used for the arguments of Python C API functions, that steal references,
    /// and internal <see cref="PyObject"/> constructors.
    /// </summary>
    [NonCopyable]
    readonly ref struct StolenReference
    {
        internal readonly IntPtr Pointer;

        internal StolenReference(IntPtr pointer)
        {
            Pointer = pointer;
        }

        [Pure]
        public static bool operator ==(in StolenReference reference, NullOnly @null)
            => reference.Pointer == IntPtr.Zero;
        [Pure]
        public static bool operator !=(in StolenReference reference, NullOnly @null)
            => reference.Pointer != IntPtr.Zero;

        [Pure]
        public override bool Equals(object obj)
        {
            if (obj is IntPtr ptr)
                return ptr == Pointer;

            return false;
        }

        [Pure]
        public override int GetHashCode() => Pointer.GetHashCode();
    }

    static class StolenReferenceExtensions
    {
        [Pure]
        public static IntPtr DangerousGetAddressOrNull(this in StolenReference reference)
            => reference.Pointer;
    }
}
