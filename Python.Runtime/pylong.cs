using System;

namespace Python.Runtime
{
    /// <summary>
    /// Represents a Python long int object. See the documentation at
    /// PY2: https://docs.python.org/2/c-api/long.html
    /// PY3: https://docs.python.org/3/c-api/long.html
    /// for details.
    /// </summary>
    public class PyLong : PyNumber
    {
        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>
        public PyLong(IntPtr ptr) : base(ptr)
        {
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Copy constructor - obtain a PyLong from a generic PyObject. An
        /// ArgumentException will be thrown if the given object is not a
        /// Python long object.
        /// </remarks>
        public PyLong(PyObject o)
        {
            if (!IsLongType(o))
            {
                throw new ArgumentException("object is not a long");
            }
            Runtime.XIncref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an int32 value.
        /// </remarks>
        public PyLong(int value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from a uint32 value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyLong(uint value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an int64 value.
        /// </remarks>
        public PyLong(long value)
        {
            obj = Runtime.PyLong_FromLongLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from a uint64 value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyLong(ulong value)
        {
            obj = Runtime.PyLong_FromUnsignedLongLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an int16 value.
        /// </remarks>
        public PyLong(short value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an uint16 value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyLong(ushort value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from a byte value.
        /// </remarks>
        public PyLong(byte value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an sbyte value.
        /// </remarks>
        [CLSCompliant(false)]
        public PyLong(sbyte value)
        {
            obj = Runtime.PyLong_FromLong(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from an double value.
        /// </remarks>
        public PyLong(double value)
        {
            obj = Runtime.PyLong_FromDouble(value);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        /// <remarks>
        /// Creates a new PyLong from a string value.
        /// </remarks>
        public PyLong(string value)
        {
            obj = Runtime.PyLong_FromString(value, IntPtr.Zero, 0);
            Runtime.CheckExceptionOccurred();
        }


        /// <summary>
        /// IsLongType Method
        /// </summary>
        /// <remarks>
        /// Returns true if the given object is a Python long.
        /// </remarks>
        public static bool IsLongType(PyObject value)
        {
            return Runtime.PyLong_Check(value.obj);
        }


        /// <summary>
        /// AsLong Method
        /// </summary>
        /// <remarks>
        /// Convert a Python object to a Python long if possible, raising
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "long(object)".
        /// </remarks>
        public static PyLong AsLong(PyObject value)
        {
            IntPtr op = Runtime.PyNumber_Long(value.obj);
            Runtime.CheckExceptionOccurred();
            return new PyLong(op);
        }

        /// <summary>
        /// ToInt16 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python long object as an int16.
        /// </remarks>
        public short ToInt16()
        {
            return Convert.ToInt16(ToInt64());
        }


        /// <summary>
        /// ToInt32 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python long object as an int32.
        /// </remarks>
        public int ToInt32()
        {
            return Convert.ToInt32(ToInt64());
        }


        /// <summary>
        /// ToInt64 Method
        /// </summary>
        /// <remarks>
        /// Return the value of the Python long object as an int64.
        /// </remarks>
        public long ToInt64()
        {
            return Runtime.PyLong_AsLongLong(obj);
        }
    }
}
