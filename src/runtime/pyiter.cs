using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a standard Python iterator object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/iterator.html
    /// PY3: https://docs.python.org/3/c-api/iterator.html
    /// for details.
    /// </summary>
    public class PyIter : PyObject, IEnumerator<object>
    {
        private PyObject _current;

        /// <summary>
        /// PyIter Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyIter from an existing iterator reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyIter(IntPtr ptr) : base(ptr)
        {
        }

        /// <summary>
        /// PyIter factory function.
        /// </summary>
        /// <remarks>
        /// Create a new PyIter from a given iterable.  Like doing "iter(iterable)" in python.
        /// </remarks>
        /// <param name="iterable"></param>
        /// <returns></returns>
        public static PyIter GetIter(PyObject iterable)
        {
            if (iterable == null)
            {
                throw new ArgumentNullException();
            }
            IntPtr val = Runtime.PyObject_GetIter(iterable.obj);
            PythonException.ThrowIfIsNull(val);
            return new PyIter(val);
        }

        protected override void Dispose(bool disposing)
        {
            if (null != _current)
            {
                _current.Dispose();
                _current = null;
            }
            base.Dispose(disposing);
        }

        public bool MoveNext()
        {
            // dispose of the previous object, if there was one
            if (null != _current)
            {
                _current.Dispose();
                _current = null;
            }

            IntPtr next = Runtime.PyIter_Next(obj);
            if (next == IntPtr.Zero)
            {
                return false;
            }

            _current = new PyObject(next);
            return true;
        }

        public void Reset()
        {
            //Not supported in python.
        }

        public object Current
        {
            get { return _current; }
        }
    }
}
