using System;

namespace Python.Runtime
{
    /// <summary>
    /// Implements reflected generic types. Note that the Python behavior
    /// is the same for both generic type definitions and constructed open
    /// generic types. Both are essentially factories for creating closed
    /// types based on the required generic type parameters.
    /// </summary>
    [Serializable]
    internal class GenericType : ClassBase
    {
        internal GenericType(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __new__ for reflected generic types.
        /// </summary>
        public static NewReference tp_new(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            Exceptions.SetError(Exceptions.TypeError, "cannot instantiate an open generic type");
            return default;
        }


        /// <summary>
        /// Implements __call__ for reflected generic types.
        /// </summary>
        public static NewReference tp_call(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            Exceptions.SetError(Exceptions.TypeError, "object is not callable");
            return default;
        }
    }
}
