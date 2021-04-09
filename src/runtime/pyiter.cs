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
    public class PyIter : PyObject, IEnumerator<PyObject>
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
            _current = null;
            base.Dispose(disposing);
        }

        public bool MoveNext()
        {
            NewReference next = Runtime.PyIter_Next(Reference);
            if (next.IsNull())
            {
                if (Exceptions.ErrorOccurred())
                {
                    throw new PythonException();
                }

                // stop holding the previous object, if there was one
                _current = null;
                return false;
            }

            _current = next.MoveToPyObject();
            return true;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public PyObject Current => _current;
        object System.Collections.IEnumerator.Current => _current;
    }
}
