using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a standard Python iterator object. See the documentation at
    /// http://www.python.org/doc/2.4.4/api/iterator.html for details.
    /// </summary>
    public class PyIter : PyObject, IEnumerator<object>
    {
        private PyObject _current = null;

        /// <summary>
        /// PyIter Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyIter from an existing iterator reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyIter(IntPtr ptr) : base(ptr)
        {
        }

        /// <summary>
        /// PyIter Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a Python iterator from an iterable. Like doing "iter(iterable)" in python.
        /// </remarks>
        public PyIter(PyObject iterable) : base()
        {
            obj = Runtime.PyObject_GetIter(iterable.obj);
            if (obj == IntPtr.Zero)
                throw new PythonException();
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

        #region IEnumerator Members

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
                return false;

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

        #endregion
    }
}