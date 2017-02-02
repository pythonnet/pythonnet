using System;
using System.Collections;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a generic Python sequence. The methods of this class are
    /// equivalent to the Python "abstract sequence API". See
    /// PY2: https://docs.python.org/2/c-api/sequence.html
    /// PY3: https://docs.python.org/3/c-api/sequence.html
    /// for details.
    /// </summary>
    public class PySequence : PyObject, IEnumerable
    {
        protected PySequence(IntPtr ptr) : base(ptr)
        {
        }

        protected PySequence()
        {
        }


        /// <summary>
        /// IsSequenceType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object implements the sequence protocol.
        /// </remarks>
        public static bool IsSequenceType(PyObject value)
        {
            return Runtime.PySequence_Check(value.obj);
        }


        /// <summary>
        /// GetSlice Method
        /// </summary>
        /// <remarks>
        /// Return the slice of the sequence with the given indices.
        /// </remarks>
        public PyObject GetSlice(int i1, int i2)
        {
            IntPtr op = Runtime.PySequence_GetSlice(obj, i1, i2);
            if (op == IntPtr.Zero)
            {
                throw new PythonException();
            }
            return new PyObject(op);
        }


        /// <summary>
        /// SetSlice Method
        /// </summary>
        /// <remarks>
        /// Sets the slice of the sequence with the given indices.
        /// </remarks>
        public void SetSlice(int i1, int i2, PyObject v)
        {
            int r = Runtime.PySequence_SetSlice(obj, i1, i2, v.obj);
            if (r < 0)
            {
                throw new PythonException();
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
                throw new PythonException();
            }
        }


        /// <summary>
        /// Index Method
        /// </summary>
        /// <remarks>
        /// Return the index of the given item in the sequence, or -1 if
        /// the item does not appear in the sequence.
        /// </remarks>
        public int Index(PyObject item)
        {
            int r = Runtime.PySequence_Index(obj, item.obj);
            if (r < 0)
            {
                Runtime.PyErr_Clear();
                return -1;
            }
            return r;
        }


        /// <summary>
        /// Contains Method
        /// </summary>
        /// <remarks>
        /// Return true if the sequence contains the given item. This method
        /// throws a PythonException if an error occurs during the check.
        /// </remarks>
        public bool Contains(PyObject item)
        {
            int r = Runtime.PySequence_Contains(obj, item.obj);
            if (r < 0)
            {
                throw new PythonException();
            }
            return r != 0;
        }


        /// <summary>
        /// Concat Method
        /// </summary>
        /// <remarks>
        /// Return the concatenation of the sequence object with the passed in
        /// sequence object.
        /// </remarks>
        public PyObject Concat(PyObject other)
        {
            IntPtr op = Runtime.PySequence_Concat(obj, other.obj);
            if (op == IntPtr.Zero)
            {
                throw new PythonException();
            }
            return new PyObject(op);
        }


        /// <summary>
        /// Repeat Method
        /// </summary>
        /// <remarks>
        /// Return the sequence object repeated N times. This is equivalent
        /// to the Python expression "object * count".
        /// </remarks>
        public PyObject Repeat(int count)
        {
            IntPtr op = Runtime.PySequence_Repeat(obj, count);
            if (op == IntPtr.Zero)
            {
                throw new PythonException();
            }
            return new PyObject(op);
        }
    }
}
