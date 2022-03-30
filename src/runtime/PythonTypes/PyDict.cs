using System;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python dictionary object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/dict.html
    /// PY3: https://docs.python.org/3/c-api/dict.html
    /// for details.
    /// </summary>
    [Serializable]
    public class PyDict : PyIterable
    {
        internal PyDict(BorrowedReference reference) : base(reference) { }
        internal PyDict(in StolenReference reference) : base(reference) { }

        /// <summary>
        /// Creates a new Python dictionary object.
        /// </summary>
        public PyDict() : base(Runtime.PyDict_New().StealOrThrow()) { }

        /// <summary>
        /// Wraps existing dictionary object.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if the given object is not a Python dictionary object
        /// </exception>
        public PyDict(PyObject o) : base(o is null ? throw new ArgumentNullException(nameof(o)) : o.Reference)
        {
            if (!IsDictType(o))
            {
                throw new ArgumentException("object is not a dict");
            }
        }

        protected PyDict(SerializationInfo info, StreamingContext context)
            : base(info, context) { }


        /// <summary>
        /// IsDictType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python dictionary.
        /// </remarks>
        public static bool IsDictType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return Runtime.PyDict_Check(value.obj);
        }


        /// <summary>
        /// HasKey Method
        /// </summary>
        /// <remarks>
        /// Returns true if the object key appears in the dictionary.
        /// </remarks>
        public bool HasKey(PyObject key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            return Runtime.PyMapping_HasKey(obj, key.obj) != 0;
        }


        /// <summary>
        /// HasKey Method
        /// </summary>
        /// <remarks>
        /// Returns true if the string key appears in the dictionary.
        /// </remarks>
        public bool HasKey(string key)
        {
            using var str = new PyString(key);
            return HasKey(str);
        }


        /// <summary>
        /// Keys Method
        /// </summary>
        /// <remarks>
        /// Returns a sequence containing the keys of the dictionary.
        /// </remarks>
        public PyIterable Keys()
        {
            using var items = Runtime.PyDict_Keys(Reference);
            if (items.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyIterable(items.Steal());
        }


        /// <summary>
        /// Values Method
        /// </summary>
        /// <remarks>
        /// Returns a sequence containing the values of the dictionary.
        /// </remarks>
        public PyIterable Values()
        {
            using var items = Runtime.PyDict_Values(obj);
            return new PyIterable(items.StealOrThrow());
        }


        /// <summary>
        /// Items Method
        /// </summary>
        /// <remarks>
        /// Returns a sequence containing the items of the dictionary.
        /// </remarks>
        public PyIterable Items()
        {
            using var items = Runtime.PyDict_Items(this.Reference);
            if (items.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyIterable(items.Steal());
        }


        /// <summary>
        /// Copy Method
        /// </summary>
        /// <remarks>
        /// Returns a copy of the dictionary.
        /// </remarks>
        public PyDict Copy()
        {
            var op = Runtime.PyDict_Copy(Reference);
            if (op.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return new PyDict(op.Steal());
        }


        /// <summary>
        /// Update Method
        /// </summary>
        /// <remarks>
        /// Update the dictionary from another dictionary.
        /// </remarks>
        public void Update(PyObject other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            int result = Runtime.PyDict_Update(Reference, other.Reference);
            if (result < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }


        /// <summary>
        /// Clear Method
        /// </summary>
        /// <remarks>
        /// Clears the dictionary.
        /// </remarks>
        public void Clear()
        {
            Runtime.PyDict_Clear(obj);
        }

        public override int GetHashCode() => rawPtr.GetHashCode();

        public override bool Equals(PyObject? other)
        {
            if (other is null) return false;
            if (obj == other.obj) return true;
            if (other is PyDict || IsDictType(other)) return base.Equals(other);
            return false;
        }
    }
}
