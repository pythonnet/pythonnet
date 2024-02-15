using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python (ANSI) string object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/string.html
    /// PY3: No Equivalent
    /// for details.
    /// </summary>
    /// <remarks>
    /// 2011-01-29: ...Then why does the string constructor call PyUnicode_FromUnicode()???
    /// </remarks>
    [Serializable]
    public class PyString : PySequence, IComparable<string>, IEquatable<string>
    {
        internal PyString(in StolenReference reference) : base(reference) { }
        internal PyString(BorrowedReference reference) : base(reference) { }
        protected PyString(SerializationInfo info, StreamingContext context) : base(info, context) { }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            if (!IsStringType(o))
            {
                throw new ArgumentException("object is not a string");
            }
            return o.Reference;
        }

        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyString from a generic PyObject.
        /// An ArgumentException will be thrown if the given object is not
        /// a Python string object.
        /// </remarks>
        public PyString(PyObject o) : base(FromObject(o))
        {
        }

        /// <summary>
        /// PyString Constructor
        /// </summary>
        /// <remarks>
        /// Creates a Python string from a managed string.
        /// </remarks>
        public PyString(string s) : base(Runtime.PyString_FromString(s).StealOrThrow())
        {
        }


        /// <summary>
        /// Returns true if the given object is a Python string.
        /// </summary>
        public static bool IsStringType(PyObject value)
        {
            return Runtime.PyString_Check(value.obj);
        }

        public override TypeCode GetTypeCode() => TypeCode.String;

        internal string ToStringUnderGIL()
        {
            string? result = Runtime.GetManagedString(this.Reference);
            Debug.Assert(result is not null);
            return result!;
        }

        public bool Equals(string? other)
            => this.ToStringUnderGIL().Equals(other, StringComparison.CurrentCulture);
        public int CompareTo(string? other)
            => string.Compare(this.ToStringUnderGIL(), other, StringComparison.CurrentCulture);

        public override string ToString()
        {
            using var _ = Py.GIL();
            return this.ToStringUnderGIL();
        }
    }
}
