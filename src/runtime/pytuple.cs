using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python tuple object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/tupleObjects.html
    /// PY3: https://docs.python.org/3/c-api/tupleObjects.html
    /// for details.
    /// </summary>
    public class PyTuple : PySequence
    {
        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyTuple from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyTuple(IntPtr ptr) : base(ptr)
        {
        }


        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyTuple from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python tuple object.
        /// </remarks>
        public PyTuple(PyObject o)
        {
            if (!IsTupleType(o))
            {
                throw new ArgumentException("object is not a tuple");
            }
            Runtime.XIncref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new empty PyTuple.
        /// </remarks>
        public PyTuple()
        {
            obj = Runtime.PyTuple_New(0);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyTuple from an array of PyObject instances.
        /// <para />
        /// See caveats about PyTuple_SetItem:
        /// https://www.coursehero.com/file/p4j2ogg/important-exceptions-to-this-rule-PyTupleSetItem-and-PyListSetItem-These/
        /// </remarks>
        public PyTuple(PyObject[] items)
        {
            int count = items.Length;
            obj = Runtime.PyTuple_New(count);
            for (var i = 0; i < count; i++)
            {
                IntPtr ptr = items[i].obj;
                Runtime.XIncref(ptr);
                Runtime.PyTuple_SetItem(obj, i, ptr);
                Runtime.CheckExceptionOccurred();
            }
        }


        /// <summary>
        /// IsTupleType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python tuple.
        /// </remarks>
        public static bool IsTupleType(PyObject value)
        {
            return Runtime.PyTuple_Check(value.obj);
        }


        /// <summary>
        /// AsTuple Method
        /// </summary>
        /// <remarks>
        /// Convert a Python object to a Python tuple if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "tuple(object)".
        /// </remarks>
        public static PyTuple AsTuple(PyObject value)
        {
            IntPtr op = Runtime.PySequence_Tuple(value.obj);
            Runtime.CheckExceptionOccurred();
            return new PyTuple(op);
        }
    }
}
