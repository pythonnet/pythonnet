using System;

namespace Python.Runtime
{
    public class PyAnsiString : PySequence
    {
        /// <summary>
        /// PyAnsiString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyAnsiString from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyAnsiString(IntPtr ptr) : base(ptr)
        {
        }


        private static IntPtr FromObject(PyObject o)
        {
            if (o == null || !IsStringType(o))
            {
                throw new ArgumentException("object is not a string");
            }
            Runtime.XIncref(o.obj);
            return o.obj;
        }

        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyAnsiString from a generic PyObject.
        /// An ArgumentException will be thrown if the given object is not
        /// a Python string object.
        /// </remarks>
        public PyAnsiString(PyObject o) : base(FromObject(o))
        {
        }
        private static IntPtr FromString(string s)
        {
            IntPtr val = Runtime.PyString_FromString(s);
            PythonException.ThrowIfIsNull(val);
            return val;
        }


        /// <summary>
        /// PyAnsiString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a Python string from a managed string.
        /// </remarks>
        public PyAnsiString(string s) : base(FromString(s))
        {
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
