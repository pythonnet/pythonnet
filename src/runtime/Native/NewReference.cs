namespace Python.Runtime
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a reference to a Python object, that is tracked by Python's reference counting.
    /// </summary>
    [NonCopyable]
    ref struct NewReference
    {
        IntPtr pointer;

        /// <summary>Creates a <see cref="NewReference"/> pointing to the same object</summary>
        [DebuggerHidden]
        public NewReference(BorrowedReference reference, bool canBeNull = false)
        {
            var address = canBeNull
                ? reference.DangerousGetAddressOrNull()
                : reference.DangerousGetAddress();
#pragma warning disable CS0618 // Type or member is obsolete
            Runtime.XIncref(reference);
#pragma warning restore CS0618 // Type or member is obsolete
            this.pointer = address;
        }

        /// <summary>Creates a <see cref="NewReference"/> pointing to the same object</summary>
        public NewReference(in NewReference reference, bool canBeNull = false)
            : this(reference.BorrowNullable(), canBeNull) { }

        /// <summary>
        /// Returns <see cref="PyObject"/> wrapper around this reference, which now owns
        /// the pointer. Sets the original reference to <c>null</c>, as it no longer owns it.
        /// </summary>
        public PyObject MoveToPyObject()
        {
            if (this.IsNull()) throw new NullReferenceException();

            return new PyObject(this.StealNullable());
        }

        /// <summary>
        /// Creates new instance of <see cref="NewReference"/> which now owns the pointer.
        /// Sets the original reference to <c>null</c>, as it no longer owns the pointer.
        /// </summary>
        public NewReference Move()
        {
            var result = DangerousFromPointer(this.DangerousGetAddress());
            this.pointer = default;
            return result;
        }

        /// <summary>Moves ownership of this instance to unmanged pointer</summary>
        public IntPtr DangerousMoveToPointer()
        {
            if (this.IsNull()) throw new NullReferenceException();

            var result = this.pointer;
            this.pointer = IntPtr.Zero;
            return result;
        }

        /// <summary>Moves ownership of this instance to unmanged pointer</summary>
        public IntPtr DangerousMoveToPointerOrNull()
        {
            var result = this.pointer;
            this.pointer = IntPtr.Zero;
            return result;
        }

        /// <summary>
        /// Returns <see cref="PyObject"/> wrapper around this reference, which now owns
        /// the pointer. Sets the original reference to <c>null</c>, as it no longer owns it.
        /// </summary>
        public PyObject? MoveToPyObjectOrNull() => this.IsNull() ? null : this.MoveToPyObject();

        /// <summary>
        /// Call this method to move ownership of this reference to a Python C API function,
        /// that steals reference passed to it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public StolenReference StealNullable() => StolenReference.TakeNullable(ref this.pointer);

        /// <summary>
        /// Call this method to move ownership of this reference to a Python C API function,
        /// that steals reference passed to it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public StolenReference Steal()
        {
            if (this.IsNull()) throw new NullReferenceException();

            return this.StealNullable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerHidden]
        public StolenReference StealOrThrow()
        {
            if (this.IsNull()) throw PythonException.ThrowLastAsClrException();

            return this.StealNullable();
        }

        /// <summary>
        /// Removes this reference to a Python object, and sets it to <c>null</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (this.IsNull())
            {
                return;
            }
            Runtime.XDecref(this.Steal());
        }

        /// <summary>
        /// Creates <see cref="NewReference"/> from a raw pointer
        /// </summary>
        [Pure]
        public static NewReference DangerousFromPointer(IntPtr pointer)
            => new() { pointer = pointer};

        [Pure]
        internal static IntPtr DangerousGetAddressOrNull(in NewReference reference) => reference.pointer;
        [Pure]
        internal static IntPtr DangerousGetAddress(in NewReference reference)
            => IsNull(reference) ? throw new NullReferenceException() : reference.pointer;
        [Pure]
        [DebuggerHidden]
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
        [DebuggerHidden]
        public static IntPtr DangerousGetAddress(this in NewReference reference)
            => NewReference.DangerousGetAddress(reference);
        [Pure]
        [DebuggerHidden]
        public static bool IsNull(this in NewReference reference)
            => NewReference.IsNull(reference);


        [Pure]
        [DebuggerHidden]
        public static BorrowedReference BorrowNullable(this in NewReference reference)
            => new(NewReference.DangerousGetAddressOrNull(reference));
        [Pure]
        [DebuggerHidden]
        public static BorrowedReference Borrow(this in NewReference reference)
            => reference.IsNull() ? throw new NullReferenceException() : reference.BorrowNullable();
        [Pure]
        [DebuggerHidden]
        public static BorrowedReference BorrowOrThrow(this in NewReference reference)
            => reference.IsNull() ? throw PythonException.ThrowLastAsClrException() : reference.BorrowNullable();
    }
}
