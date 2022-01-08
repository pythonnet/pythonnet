using System;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python integer object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/int.html
    /// PY3: No equivalent
    /// for details.
    /// </summary>
    public class PyInt : PyNumber
    {
        internal PyInt(in StolenReference ptr) : base(ptr)
        {
        }

        internal PyInt(BorrowedReference reference): base(reference)
        {
            if (!Runtime.PyInt_Check(reference)) throw new ArgumentException("object is not an int");
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyInt from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python int object.
        /// </remarks>
        public PyInt(PyObject o) : base(FromObject(o))
        {
        }

        private static BorrowedReference FromObject(PyObject o)
        {
            if (o is null) throw new ArgumentNullException(nameof(o));
            if (!IsIntType(o))
            {
                throw new ArgumentException("object is not an int");
            }
            return o.Reference;
        }

        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an int32 value.
        /// </remarks>
        public PyInt(int value) : base(Runtime.PyInt_FromInt32(value).StealOrThrow())
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a uint32 value.
        /// </remarks>
        public PyInt(uint value) : this((long)value)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an int64 value.
        /// </remarks>
        public PyInt(long value) : base(Runtime.PyInt_FromInt64(value).StealOrThrow())
        {
        }

        /// <summary>
        /// Creates a new Python int from a <see cref="UInt64"/> value.
        /// </summary>
        public PyInt(ulong value) : base(Runtime.PyLong_FromUnsignedLongLong(value).StealOrThrow())
        {
        }

        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an int16 value.
        /// </remarks>
        public PyInt(short value) : this((int)value)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a uint16 value.
        /// </remarks>
        public PyInt(ushort value) : this((int)value)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a byte value.
        /// </remarks>
        public PyInt(byte value) : this((int)value)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an sbyte value.
        /// </remarks>
        public PyInt(sbyte value) : this((int)value)
        {
        }

        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a string value.
        /// </remarks>
        public PyInt(string value) : base(Runtime.PyLong_FromString(value, 0).StealOrThrow())
        {
        }

        protected PyInt(SerializationInfo info, StreamingContext context)
            : base(info, context) { }


        /// <summary>
        /// IsIntType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python int.
        /// </remarks>
        public static bool IsIntType(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            return Runtime.PyInt_Check(value.obj);
        }


        /// <summary>
        /// Convert a Python object to a Python int if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "int(object)".
        /// </summary>
        public static PyInt AsInt(PyObject value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            var op = Runtime.PyNumber_Long(value.Reference);
            PythonException.ThrowIfIsNull(op);
            return new PyInt(op.Steal());
        }


        /// <summary>
        /// ToInt16 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python int object as an int16.
        /// </remarks>
        public short ToInt16()
        {
            return Convert.ToInt16(ToInt32());
        }


        /// <summary>
        /// Return the value of the Python int object as an <see cref="Int32"/>.
        /// </summary>
        public int ToInt32() => Converter.ToInt32(Reference);

        /// <summary>
        /// ToInt64 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python int object as an int64.
        /// </remarks>
        public long ToInt64()
        {
            long? val = Runtime.PyLong_AsLongLong(obj);
            if (val is null)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return val.Value;
        }
    }
}
