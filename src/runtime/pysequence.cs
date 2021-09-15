using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a generic Python sequence. The methods of this class are
    /// equivalent to the Python "abstract sequence API". See
    /// PY2: https://docs.python.org/2/c-api/sequence.html
    /// PY3: https://docs.python.org/3/c-api/sequence.html
    /// for details.
    /// </summary>
    public class PySequence : PyIterable
    {
        internal PySequence(BorrowedReference reference) : base(reference) { }
        internal PySequence(in StolenReference reference) : base(reference) { }


        /// <summary>
        /// Returns <c>true</c> if the given object implements the sequence protocol.
        /// </summary>
        public static bool IsSequenceType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PySequence_Check(value.obj);
        }

        /// <summary>
        /// Return the slice of the sequence with the given indices.
        /// </summary>
        public PyObject GetSlice(int i1, int i2)
        {
            IntPtr op = Runtime.PySequence_GetSlice(obj, i1, i2);
            if (op == IntPtr.Zero)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyObject(op);
        }


        /// <summary>
        /// Sets the slice of the sequence with the given indices.
        /// </summary>
        public void SetSlice(int i1, int i2, PyObject v)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));

            int r = Runtime.PySequence_SetSlice(obj, i1, i2, v.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// DelSlice Method
        /// </summary>
        /// <remarks>
        /// Deletes the slice of the sequence with the given indices.
        /// </remarks>
        public void DelSlice(int i1, int i2)
        {
            int r = Runtime.PySequence_DelSlice(obj, i1, i2);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// Return the index of the given item in the sequence, or -1 if
        /// the item does not appear in the sequence.
        /// </summary>
        public int Index(PyObject item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            int r = Runtime.PySequence_Index(obj, item.obj);
            if (r < 0)
            {
                Runtime.PyErr_Clear();
                return -1;
            }
            return r;
        }


        /// <summary>
        /// Return true if the sequence contains the given item. This method
        /// throws a PythonException if an error occurs during the check.
        /// </summary>
        public bool Contains(PyObject item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            int r = Runtime.PySequence_Contains(obj, item.obj);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return r != 0;
        }


        /// <summary>
        /// Return the concatenation of the sequence object with the passed in
        /// sequence object.
        /// </summary>
        public PyObject Concat(PyObject other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            IntPtr op = Runtime.PySequence_Concat(obj, other.obj);
            if (op == IntPtr.Zero)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyObject(op);
        }


        /// <summary>
        /// Return the sequence object repeated N times. This is equivalent
        /// to the Python expression "object * count".
        /// </summary>
        public PyObject Repeat(int count)
        {
            IntPtr op = Runtime.PySequence_Repeat(obj, count);
            if (op == IntPtr.Zero)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyObject(op);
        }
    }
}
