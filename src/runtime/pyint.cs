using System;

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
        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyInt from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyInt(IntPtr ptr) : base(ptr)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyInt from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python int object.
        /// </remarks>
        public PyInt(PyObject o)
        {
            if (!IsIntType(o))
            {
                throw new ArgumentException("object is not an int");
            }
            Runtime.XIncref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an int32 value.
        /// </remarks>
        public PyInt(int value)
        {
            obj = Runtime.PyInt_FromInt32(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a uint32 value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyInt(uint value) : base(IntPtr.Zero)
        {
            obj = Runtime.PyInt_FromInt64(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from an int64 value.
        /// </remarks>
        public PyInt(long value) : base(IntPtr.Zero)
        {
            obj = Runtime.PyInt_FromInt64(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a uint64 value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyInt(ulong value) : base(IntPtr.Zero)
        {
            obj = Runtime.PyInt_FromInt64((long)value);
            Runtime.CheckExceptionOccurred();
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
        [CLSCompliant(false)]
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
        [CLSCompliant(false)]
        public PyInt(sbyte value) : this((int)value)
        {
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new Python int from a string value.
        /// </remarks>
        public PyInt(string value)
        {
            obj = Runtime.PyInt_FromString(value, IntPtr.Zero, 0);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// IsIntType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python int.
        /// </remarks>
        public static bool IsIntType(PyObject value)
        {
            return Runtime.PyInt_Check(value.obj);
        }


        /// <summary>
        /// AsInt Method
        /// </summary>
        /// <remarks>
        /// Convert a Python object to a Python int if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "int(object)".
        /// </remarks>
        public static PyInt AsInt(PyObject value)
        {
            IntPtr op = Runtime.PyNumber_Int(value.obj);
            Runtime.CheckExceptionOccurred();
            return new PyInt(op);
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
        /// ToInt32 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python int object as an int32.
        /// </remarks>
        public int ToInt32()
        {
            return Runtime.PyInt_AsLong(obj);
        }


        /// <summary>
        /// ToInt64 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python int object as an int64.
        /// </remarks>
        public long ToInt64()
        {
            return Convert.ToInt64(ToInt32());
        }
    }
}
