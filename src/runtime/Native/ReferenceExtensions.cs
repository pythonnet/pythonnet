namespace Python.Runtime
{
    using System.Diagnostics.Contracts;

    static class ReferenceExtensions
    {
        /// <summary>
        /// Checks if the reference points to Python object <c>None</c>.
        /// </summary>
        [Pure]
        public static bool IsNone(this in NewReference reference)
            => reference.BorrowNullable() == Runtime.PyNone;
        /// <summary>
        /// Checks if the reference points to Python object <c>None</c>.
        /// </summary>
        [Pure]
        public static bool IsNone(this BorrowedReference reference)
            => reference == Runtime.PyNone;
    }
}
