using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python tuple object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/tupleObjects.html
    /// PY3: https://docs.python.org/3/c-api/tupleObjects.html
    /// for details.
    /// </summary>
    public class PyTuple : PySequence
    {
        internal PyTuple(in StolenReference reference) : base(reference) { }
        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyTuple from an existing object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        internal PyTuple(BorrowedReference reference) : base(reference) { }
        protected PyTuple(SerializationInfo info, StreamingContext context) : base(info, context) { }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));

            if (!IsTupleType(o))
            {
                throw new ArgumentException("object is not a tuple");
            }
            return o.Reference;
        }

        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyTuple from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python tuple object.
        /// </remarks>
        public PyTuple(PyObject o) : base(FromObject(o))
        {
        }


        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new empty PyTuple.
        /// </remarks>
        public PyTuple() : base(NewEmtpy()) { }

        private static StolenReference NewEmtpy()
        {
            var ptr = Runtime.PyTuple_New(0);
            return ptr.StealOrThrow();
        }

        private static StolenReference FromArray(PyObject[] items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (items.Any(item => item is null))
                throw new ArgumentException(message: Util.UseNone, paramName: nameof(items));

            int count = items.Length;
            using var val = Runtime.PyTuple_New(count);
            for (var i = 0; i < count; i++)
            {
                int res = Runtime.PyTuple_SetItem(val.Borrow(), i, items[i]);
                if (res != 0)
                {
                    val.Dispose();
                    throw PythonException.ThrowLastAsClrException();
                }
            }
            return val.Steal();
        }

        /// <summary>
        /// PyTuple Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyTuple from an array of PyObject instances.
        /// <para />
        /// See caveats about PyTuple_SetItem:
        /// https://www.coursehero.com/file/p4j2ogg/important-exceptions-to-this-rule-PyTupleSetItem-and-PyListSetItem-These/
        /// </remarks>
        public PyTuple(PyObject[] items) : base(FromArray(items))
        {
        }


        /// <summary>
        /// Returns <c>true</c> if the given object is a Python tuple.
        /// </summary>
        public static bool IsTupleType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return Runtime.PyTuple_Check(value.obj);
        }


        /// <summary>
        /// Convert a Python object to a Python tuple if possible. This is
        /// equivalent to the Python expression "tuple(<paramref name="value"/>)".
        /// </summary>
        /// <exception cref="PythonException">Raised if the object can not be converted to a tuple.</exception>
        public static PyTuple AsTuple(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            NewReference op = Runtime.PySequence_Tuple(value.Reference);
            PythonException.ThrowIfIsNull(op);
            return new PyTuple(op.Steal());
        }
    }
}
