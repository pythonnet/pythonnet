using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    [Serializable]
    public class PyIterable : PyObject, IEnumerable<PyObject>
    {
        internal PyIterable(BorrowedReference reference) : base(reference) { }
        internal PyIterable(in StolenReference reference) : base(reference) { }
        protected PyIterable(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        /// <summary>
        /// Creates new instance from an existing object.
        /// </summary>
        /// <remarks>This constructor does not check if <paramref name="o"/> is actually iterable.</remarks>
        public PyIterable(PyObject o) : base(FromObject(o)) { }

        static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            return o.Reference;
        }

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
