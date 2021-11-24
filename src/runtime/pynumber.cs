using System;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a generic Python number. The methods of this class are
    /// equivalent to the Python "abstract number API". See
    /// PY3: https://docs.python.org/3/c-api/number.html
    /// for details.
    /// </summary>
    /// <remarks>
    /// TODO: add all of the PyNumber_XXX methods.
    /// </remarks>
    public class PyNumber : PyObject
    {
        internal PyNumber(in StolenReference reference) : base(reference) { }
        internal PyNumber(BorrowedReference reference) : base(reference) { }
        protected PyNumber(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        /// <summary>
        /// IsNumberType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python numeric type.
        /// </remarks>
        public static bool IsNumberType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyNumber_Check(value.obj);
        }
    }
}
