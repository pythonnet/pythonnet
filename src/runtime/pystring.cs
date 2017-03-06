using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python (ANSI) string object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/string.html
    /// PY3: No Equivalent
    /// for details.
    /// </summary>
    /// <remarks>
    /// 2011-01-29: ...Then why does the string constructor call PyUnicode_FromUnicode()???
    /// </remarks>
    public class PyString : PySequence
    {
        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyString from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyString(IntPtr ptr) : base(ptr)
        {
        }


        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyString from a generic PyObject.
        /// An ArgumentException will be thrown if the given object is not
        /// a Python string object.
        /// </remarks>
        public PyString(PyObject o)
        {
            if (!IsStringType(o))
            {
                throw new ArgumentException("object is not a string");
            }
            Runtime.XIncref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a Python string from a managed string.
        /// </remarks>
        public PyString(string s)
        {
            obj = Runtime.PyUnicode_FromUnicode(s, s.Length);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// IsStringType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python string.
        /// </remarks>
        public static bool IsStringType(PyObject value)
        {
            return Runtime.PyString_Check(value.obj);
        }
    }
}
