using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a standard Python list object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/list.html
    /// PY3: https://docs.python.org/3/c-api/list.html
    /// for details.
    /// </summary>
    public class PyList : PySequence
    {
        /// <summary>
        /// PyList Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyList from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyList(IntPtr ptr) : base(ptr)
        {
        }

        /// <summary>
        /// Creates new <see cref="PyList"/> pointing to the same object, as the given reference.
        /// </summary>
        internal PyList(BorrowedReference reference) : base(reference) { }


        private static IntPtr FromObject(PyObject o)
        {
            if (o == null || !IsListType(o))
            {
                throw new ArgumentException("object is not a list");
            }
            Runtime.XIncref(o.obj);
            return o.obj;
        }

        /// <summary>
        /// PyList Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyList from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python list object.
        /// </remarks>
        public PyList(PyObject o) : base(FromObject(o))
        {
        }


        /// <summary>
        /// PyList Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new empty Python list object.
        /// </remarks>
        public PyList() : base(Runtime.PyList_New(0))
        {
            if (obj == IntPtr.Zero)
            {
                throw new PythonException();
            }
        }

        private static IntPtr FromArray(PyObject[] items)
        {
            int count = items.Length;
            IntPtr val = Runtime.PyList_New(count);
            for (var i = 0; i < count; i++)
            {
                IntPtr ptr = items[i].obj;
                Runtime.XIncref(ptr);
                int r = Runtime.PyList_SetItem(val, i, ptr);
                if (r < 0)
                {
                    Runtime.Py_DecRef(val);
                    throw new PythonException();
                }
            }
            return val;
        }

        /// <summary>
        /// PyList Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python list object from an array of PyObjects.
        /// </remarks>
        public PyList(PyObject[] items) : base(FromArray(items))
        {
        }

        /// <summary>
        /// IsListType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python list.
        /// </remarks>
        public static bool IsListType(PyObject value)
        {
            return Runtime.PyList_Check(value.obj);
        }


        /// <summary>
        /// AsList Method
        /// </summary>
        /// <remarks>
        /// Converts a Python object to a Python list if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "list(object)".
        /// </remarks>
        public static PyList AsList(PyObject value)
        {
            IntPtr op = Runtime.PySequence_List(value.obj);
            if (op == IntPtr.Zero)
            {
                throw new PythonException();
            }
            return new PyList(op);
        }


        /// <summary>
        /// Append Method
        /// </summary>
        /// <remarks>
        /// Append an item to the list object.
        /// </remarks>
        public void Append(PyObject item)
        {
            int r = Runtime.PyList_Append(this.Reference, item.obj);
            if (r < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// Insert Method
        /// </summary>
        /// <remarks>
        /// Insert an item in the list object at the given index.
        /// </remarks>
        public void Insert(int index, PyObject item)
        {
            int r = Runtime.PyList_Insert(this.Reference, index, item.obj);
            if (r < 0)
            {
                throw new PythonException();
            }
        }


        /// <summary>
        /// Reverse Method
        /// </summary>
        /// <remarks>
        /// Reverse the order of the list object in place.
        /// </remarks>
        public void Reverse()
        {
            int r = Runtime.PyList_Reverse(this.Reference);
            if (r < 0)
            {
                throw new PythonException();
            }
        }


        /// <summary>
        /// Sort Method
        /// </summary>
        /// <remarks>
        /// Sort the list in place.
        /// </remarks>
        public void Sort()
        {
            int r = Runtime.PyList_Sort(this.Reference);
            if (r < 0)
            {
                throw new PythonException();
            }
        }
    }
}
