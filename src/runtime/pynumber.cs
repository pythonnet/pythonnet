using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a generic Python number. The methods of this class are
    /// equivalent to the Python "abstract number API". See
    /// PY2: https://docs.python.org/2/c-api/number.html
    /// PY3: https://docs.python.org/3/c-api/number.html
    /// for details.
    /// </summary>
    /// <remarks>
    /// TODO: add all of the PyNumber_XXX methods.
    /// </remarks>
    public class PyNumber : PyObject
    {
        protected PyNumber(IntPtr ptr) : base(ptr)
        {
        }

        protected PyNumber()
        {
        }


        /// <summary>
        /// IsNumberType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python numeric type.
        /// </remarks>
        public static bool IsNumberType(PyObject value)
        {
            return Runtime.PyNumber_Check(value.obj);
        }
    }
}
