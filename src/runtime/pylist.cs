using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a standard Python list object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/list.html
    /// PY3: https://docs.python.org/3/c-api/list.html
    /// for details.
    /// </summary>
    [Serializable]
    public class PyList : PySequence
    {
        internal PyList(in StolenReference reference) : base(reference) { }
        /// <summary>
        /// Creates new <see cref="PyList"/> pointing to the same object, as the given reference.
        /// </summary>
        internal PyList(BorrowedReference reference) : base(reference) { }


        protected PyList(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

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
        public PyList() : base(Runtime.PyList_New(0).StealOrThrow())
        {
        }

        private static StolenReference FromArray(PyObject[] items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (items.Any(item => item is null))
                throw new ArgumentException(message: Util.UseNone, paramName: nameof(items));

            int count = items.Length;
            using var val = Runtime.PyList_New(count);
            for (var i = 0; i < count; i++)
            {
                int r = Runtime.PyList_SetItem(val.Borrow(), i, new NewReference(items[i]).Steal());
                if (r < 0)
                {
                    val.Dispose();
                    throw PythonException.ThrowLastAsClrException();
                }
            }
            return val.Steal();
        }

        /// <summary>
        /// Creates a new Python list object from an array of objects.
        /// </summary>
        public PyList(PyObject[] items) : base(FromArray(items))
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

            int r = Runtime.PyList_Insert(this, index, item);
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

        public override int GetHashCode() => rawPtr.GetHashCode();

        public override bool Equals(PyObject? other)
        {
            if (other is null) return false;
            if (obj == other.obj) return true;
            if (other is PyList || IsListType(other)) return base.Equals(other);
            return false;
        }
    }
}
