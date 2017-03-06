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


        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyAnsiString from a generic PyObject.
        /// An ArgumentException will be thrown if the given object is not
        /// a Python string object.
        /// </remarks>
        public PyAnsiString(PyObject o)
        {
            if (!IsStringType(o))
            {
                throw new ArgumentException("object is not a string");
            }
            Runtime.XIncref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyAnsiString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a Python string from a managed string.
        /// </remarks>
        public PyAnsiString(string s)
        {
            obj = Runtime.PyString_FromString(s);
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
