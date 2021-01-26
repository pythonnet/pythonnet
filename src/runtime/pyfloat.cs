using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python float object. See the documentation at
    /// PY3: https://docs.python.org/3/c-api/float.html
    /// for details.
    /// </summary>
    public class PyFloat : PyNumber
    {
        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyFloat from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyFloat(IntPtr ptr) : base(ptr)
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
        public PyFloat(double value) : base(FromDouble(value))
        {
        }

        private static IntPtr FromObject(PyObject o)
        {
            if (o == null || !IsFloatType(o))
            {
                throw new ArgumentException("object is not a float");
            }
            Runtime.XIncref(o.obj);
            return o.obj;
        }

        private static IntPtr FromDouble(double value)
        {
            IntPtr val = Runtime.PyFloat_FromDouble(value);
            PythonException.ThrowIfIsNull(val);
            return val;
        }

        private static IntPtr FromString(string value)
        {
            using (var s = new PyString(value))
            {
                NewReference val = Runtime.PyFloat_FromString(s.Reference);
                PythonException.ThrowIfIsNull(val);
                return val.DangerousMoveToPointerOrNull();
            }
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


        /// <summary>
        /// IsFloatType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python float.
        /// </remarks>
        public static bool IsFloatType(PyObject value)
        {
            return Runtime.PyFloat_Check(value.obj);
        }


        /// <summary>
        /// AsFloat Method
        /// </summary>
        /// <remarks>
        /// Convert a Python object to a Python float if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "float(object)".
        /// </remarks>
        public static PyFloat AsFloat(PyObject value)
        {
            IntPtr op = Runtime.PyNumber_Float(value.obj);
            PythonException.ThrowIfIsNull(op);
            return new PyFloat(op);
        }
    }
}
