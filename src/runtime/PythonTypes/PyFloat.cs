using System;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python float object. See the documentation at
    /// PY3: https://docs.python.org/3/c-api/float.html
    /// for details.
    /// </summary>
    public partial class PyFloat : PyNumber
    {
        internal PyFloat(in StolenReference ptr) : base(ptr)
        {
        }


        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyFloat from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python float object.
        /// </remarks>
        public PyFloat(PyObject o) : base(FromObject(o))
        {
        }


        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python float from a double value.
        /// </remarks>
        public PyFloat(double value) : base(Runtime.PyFloat_FromDouble(value).StealOrThrow())
        {
        }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));

            if (!IsFloatType(o))
            {
                throw new ArgumentException("object is not a float");
            }
            return o.Reference;
        }

        private static StolenReference FromString(string value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            using var s = new PyString(value);
            NewReference val = Runtime.PyFloat_FromString(s.Reference);
            PythonException.ThrowIfIsNull(val);
            return val.Steal();
        }

        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python float from a string value.
        /// </remarks>
        public PyFloat(string value) : base(FromString(value))
        {
        }

        protected PyFloat(SerializationInfo info, StreamingContext context)
            : base(info, context) { }


        /// <summary>
        /// IsFloatType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python float.
        /// </remarks>
        public static bool IsFloatType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return Runtime.PyFloat_Check(value.obj);
        }


        /// <summary>
        /// Convert a Python object to a Python float if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "float(object)".
        /// </summary>
        public static PyFloat AsFloat(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            var op = Runtime.PyNumber_Float(value.Reference);
            PythonException.ThrowIfIsNull(op);
            return new PyFloat(op.Steal());
        }

        public double ToDouble() => Runtime.PyFloat_AsDouble(obj);

        public override TypeCode GetTypeCode() => TypeCode.Double;
    }
}
