using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

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
        private PyObject? _current;

        /// <summary>
        /// PyIter Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyIter from an existing iterator reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        internal PyIter(in StolenReference reference) : base(reference)
        {
        }

        /// <summary>
        /// Creates new <see cref="PyIter"/> from an untyped reference to Python object.
        /// The object must support iterator protocol.
        /// </summary>
        public PyIter(PyObject pyObject) : base(FromPyObject(pyObject)) { }
        static BorrowedReference FromPyObject(PyObject pyObject) {
            if (pyObject is null) throw new ArgumentNullException(nameof(pyObject));

            if (!Runtime.PyIter_Check(pyObject.Reference))
                throw new ArgumentException("Object does not support iterator protocol");

            return pyObject.Reference;
        }

        internal PyIter(BorrowedReference reference) : base(reference) { }

        /// <summary>
        /// Create a new <see cref="PyIter"/> from a given iterable.
        ///
        /// Like doing "iter(<paramref name="iterable"/>)" in Python.
        /// </summary>
        public static PyIter GetIter(PyObject iterable)
        {
            if (iterable == null)
            {
                throw new ArgumentNullException();
            }
            var val = Runtime.PyObject_GetIter(iterable.Reference);
            PythonException.ThrowIfIsNull(val);
            return new PyIter(val.Steal());
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
                    throw PythonException.ThrowLastAsClrException();
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

        public PyObject Current => _current ?? throw new InvalidOperationException();
        object System.Collections.IEnumerator.Current => Current;

        protected PyIter(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _current = (PyObject?)info.GetValue("c", typeof(PyObject));
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("c", _current);
        }
    }
}
