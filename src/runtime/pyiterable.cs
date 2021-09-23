using System;
using System.Collections;
using System.Collections.Generic;

namespace Python.Runtime
{
    public class PyIterable : PyObject, IEnumerable<PyObject>
    {
        internal PyIterable(IntPtr ptr) : base(ptr)
        {
        }

        internal PyIterable(BorrowedReference reference) : base(reference) { }
        internal PyIterable(in StolenReference reference) : base(reference) { }

        /// <summary>
        /// Return a new PyIter object for the object. This allows any iterable
        /// python object to be iterated over in C#. A PythonException will be
        /// raised if the object is not iterable.
        /// </summary>
        public PyIter GetEnumerator()
        {
            return PyIter.GetIter(this);
        }
        IEnumerator<PyObject> IEnumerable<PyObject>.GetEnumerator() => this.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
