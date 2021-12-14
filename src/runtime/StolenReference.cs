namespace Python.Runtime
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Should only be used for the arguments of Python C API functions, that steal references,
    /// and internal <see cref="PyObject"/> constructors.
    /// </summary>
    [NonCopyable]
    readonly ref struct StolenReference
    {
        internal readonly IntPtr Pointer;

        [DebuggerHidden]
        StolenReference(IntPtr pointer)
        {
            Pointer = pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StolenReference Take(ref IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));
            return TakeNullable(ref ptr);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public static StolenReference TakeNullable(ref IntPtr ptr)
        {
            var stolenAddr = ptr;
            ptr = IntPtr.Zero;
            return new StolenReference(stolenAddr);
        }

        [Pure]
        public static bool operator ==(in StolenReference reference, NullOnly? @null)
            => reference.Pointer == IntPtr.Zero;
        [Pure]
        public static bool operator !=(in StolenReference reference, NullOnly? @null)
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

        [Pure]
        public static StolenReference DangerousFromPointer(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));
            return new StolenReference(ptr);
        }
    }

    static class StolenReferenceExtensions
    {
        [Pure]
        [DebuggerHidden]
        public static IntPtr DangerousGetAddressOrNull(this in StolenReference reference)
            => reference.Pointer;
        [Pure]
        [DebuggerHidden]
        public static IntPtr DangerousGetAddress(this in StolenReference reference)
            => reference.Pointer == IntPtr.Zero ? throw new NullReferenceException() : reference.Pointer;
        [DebuggerHidden]
        public static StolenReference AnalyzerWorkaround(this in StolenReference reference)
        {
            IntPtr ptr = reference.DangerousGetAddressOrNull();
            return StolenReference.TakeNullable(ref ptr);
        }
    }
}
