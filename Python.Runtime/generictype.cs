using System;

namespace Python.Runtime
{
    /// <summary>
    /// Implements reflected generic types. Note that the Python behavior
    /// is the same for both generic type definitions and constructed open
    /// generic types. Both are essentially factories for creating closed
    /// types based on the required generic type parameters.
    /// </summary>
    internal class GenericType : ClassBase
    {
        internal GenericType(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __new__ for reflected generic types.
        /// </summary>
        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            Exceptions.SetError(Exceptions.TypeError, "cannot instantiate an open generic type");
            return IntPtr.Zero;
        }


        /// <summary>
        /// Implements __call__ for reflected generic types.
        /// </summary>
        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw)
        {
            Exceptions.SetError(Exceptions.TypeError, "object is not callable");
            return IntPtr.Zero;
        }
    }
}
