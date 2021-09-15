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
        internal PyList(in StolenReference reference) : base(reference) { }
        /// <summary>
        /// Creates new <see cref="PyList"/> pointing to the same object, as the given reference.
        /// </summary>
        internal PyList(BorrowedReference reference) : base(reference) { }


        private static BorrowedReference FromObject(PyObject o)
        {
            if (o == null || !IsListType(o))
            {
                throw new ArgumentException("object is not a list");
            }
            return o.Reference;
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
        /// Creates a new empty Python list object.
        /// </summary>
        public PyList() : base(NewEmtpy().Steal())
        {
        }

        private static NewReference NewEmtpy()
        {
            IntPtr ptr = Runtime.PyList_New(0);
            PythonException.ThrowIfIsNull(ptr);
            return NewReference.DangerousFromPointer(ptr);
        }

        private static NewReference FromArray(PyObject[] items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

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
                    throw PythonException.ThrowLastAsClrException();
                }
            }
            return NewReference.DangerousFromPointer(val);
        }

        /// <summary>
        /// Creates a new Python list object from an array of objects.
        /// </summary>
        public PyList(PyObject[] items) : base(FromArray(items).Steal())
        {
        }

        /// <summary>
        /// Returns true if the given object is a Python list.
        /// </summary>
        public static bool IsListType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyList_Check(value.obj);
        }


        /// <summary>
        /// Converts a Python object to a Python list if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "list(object)".
        /// </summary>
        public static PyList AsList(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            NewReference op = Runtime.PySequence_List(value.Reference);
            if (op.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyList(op.Steal());
        }


        /// <summary>
        /// Append an item to the list object.
        /// </summary>
        public void Append(PyObject item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            int r = Runtime.PyList_Append(this.Reference, item.Reference);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <summary>
        /// Insert an item in the list object at the given index.
        /// </summary>
        public void Insert(int index, PyObject item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            int r = Runtime.PyList_Insert(this.Reference, index, item.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
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
                throw PythonException.ThrowLastAsClrException();
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
                throw PythonException.ThrowLastAsClrException();
            }
        }
    }
}
