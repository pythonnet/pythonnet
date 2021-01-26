using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;

namespace Python.Runtime
{
    /// <summary>
    /// Performs data conversions between managed types and Python types.
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal class Converter
    {
        private Converter()
        {
        }

        private static NumberFormatInfo nfi;
        private static Type objectType;
        private static Type stringType;
        private static Type singleType;
        private static Type doubleType;
        private static Type decimalType;
        private static Type int16Type;
        private static Type int32Type;
        private static Type int64Type;
        private static Type flagsType;
        private static Type boolType;
        private static Type typeType;

        static Converter()
        {
            nfi = NumberFormatInfo.InvariantInfo;
            objectType = typeof(Object);
            stringType = typeof(String);
            int16Type = typeof(Int16);
            int32Type = typeof(Int32);
            int64Type = typeof(Int64);
            singleType = typeof(Single);
            doubleType = typeof(Double);
            decimalType = typeof(Decimal);
            flagsType = typeof(FlagsAttribute);
            boolType = typeof(Boolean);
            typeType = typeof(Type);
        }


        /// <summary>
        /// Given a builtin Python type, return the corresponding CLR type.
        /// </summary>
        internal static Type GetTypeByAlias(IntPtr op)
        {
            if (op == Runtime.PyStringType)
                return stringType;

            if (op == Runtime.PyUnicodeType)
                return stringType;

            if (op == Runtime.PyIntType)
                return int32Type;

            if (op == Runtime.PyLongType)
                return int64Type;

            if (op == Runtime.PyFloatType)
                return doubleType;

            if (op == Runtime.PyBoolType)
                return boolType;

            return null;
        }

        internal static IntPtr GetPythonTypeByAlias(Type op)
        {
            if (op == stringType)
                return Runtime.PyUnicodeType;

            if (op == int16Type)
                return Runtime.PyIntType;

            if (op == int32Type)
                return Runtime.PyIntType;

            if (op == int64Type)
                return Runtime.PyIntType;

            if (op == doubleType)
                return Runtime.PyFloatType;

            if (op == singleType)
                return Runtime.PyFloatType;

            if (op == boolType)
                return Runtime.PyBoolType;

            return IntPtr.Zero;
        }


        /// <summary>
        /// Return a Python object for the given native object, converting
        /// basic types (string, int, etc.) into equivalent Python objects.
        /// This always returns a new reference. Note that the System.Decimal
        /// type has no Python equivalent and converts to a managed instance.
        /// </summary>
        internal static IntPtr ToPython<T>(T value)
        {
            return ToPython(value, typeof(T));
        }

        private static readonly Func<object, bool> IsTransparentProxy = GetIsTransparentProxy();

        private static bool Never(object _) => false;

        private static Func<object, bool> GetIsTransparentProxy()
        {
            var remoting = typeof(int).Assembly.GetType("System.Runtime.Remoting.RemotingServices");
            if (remoting is null) return Never;

            var isProxy = remoting.GetMethod("IsTransparentProxy", new[] { typeof(object) });
            if (isProxy is null) return Never;

            return (Func<object, bool>)Delegate.CreateDelegate(
              typeof(Func<object, bool>), isProxy,
              throwOnBindFailure: true);
        }

        internal static IntPtr ToPython(object value, Type type)
        {
            if (value is PyObject)
            {
                IntPtr handle = ((PyObject)value).Handle;
                Runtime.XIncref(handle);
                return handle;
            }
            IntPtr result = IntPtr.Zero;

            // Null always converts to None in Python.

            if (value == null)
            {
                result = Runtime.PyNone;
                Runtime.XIncref(result);
                return result;
            }

            if (Type.GetTypeCode(type) == TypeCode.Object && value.GetType() != typeof(object)) {
                var encoded = PyObjectConversions.TryEncode(value, type);
                if (encoded != null) {
                    result = encoded.Handle;
                    Runtime.XIncref(result);
                    return result;
                }
            }

            if (value is IList && !(value is INotifyPropertyChanged) && value.GetType().IsGenericType)
            {
                using (var resultlist = new PyList())
                {
                    foreach (object o in (IEnumerable)value)
                    {
                        using (var p = new PyObject(ToPython(o, o?.GetType())))
                        {
                            resultlist.Append(p);
                        }
                    }
                    Runtime.XIncref(resultlist.Handle);
                    return resultlist.Handle;
                }
            }

            if (type.IsInterface)
            {
                var ifaceObj = (InterfaceObject)ClassManager.GetClass(type);
                return ifaceObj.WrapObject(value);
            }

            // We need to special case interface array handling to ensure we
            // produce the correct type. Value may be an array of some concrete
            // type (FooImpl[]), but we want access to go via the interface type
            // (IFoo[]).
            if (type.IsArray && type.GetElementType().IsInterface)
            {
                return CLRObject.GetInstHandle(value, type);
            }

            // it the type is a python subclass of a managed type then return the
            // underlying python object rather than construct a new wrapper object.
            var pyderived = value as IPythonDerivedType;
            if (null != pyderived)
            {
                if (!IsTransparentProxy(pyderived))
                    return ClassDerivedObject.ToPython(pyderived);
            }

            // hmm - from Python, we almost never care what the declared
            // type is. we'd rather have the object bound to the actual
            // implementing class.

            type = value.GetType();

            TypeCode tc = Type.GetTypeCode(type);

            switch (tc)
            {
                case TypeCode.Object:
                    return CLRObject.GetInstHandle(value, type);

                case TypeCode.String:
                    return Runtime.PyUnicode_FromString((string)value);

                case TypeCode.Int32:
                    return Runtime.PyInt_FromInt32((int)value);

                case TypeCode.Boolean:
                    if ((bool)value)
                    {
                        Runtime.XIncref(Runtime.PyTrue);
                        return Runtime.PyTrue;
                    }
                    Runtime.XIncref(Runtime.PyFalse);
                    return Runtime.PyFalse;

                case TypeCode.Byte:
                    return Runtime.PyInt_FromInt32((int)((byte)value));

                case TypeCode.Char:
                    return Runtime.PyUnicode_FromOrdinal((int)((char)value));

                case TypeCode.Int16:
                    return Runtime.PyInt_FromInt32((int)((short)value));

                case TypeCode.Int64:
                    return Runtime.PyLong_FromLongLong((long)value);

                case TypeCode.Single:
                    // return Runtime.PyFloat_FromDouble((double)((float)value));
                    string ss = ((float)value).ToString(nfi);
                    IntPtr ps = Runtime.PyString_FromString(ss);
                    NewReference op = Runtime.PyFloat_FromString(new BorrowedReference(ps));;
                    Runtime.XDecref(ps);
                    return op.DangerousMoveToPointerOrNull();

                case TypeCode.Double:
                    return Runtime.PyFloat_FromDouble((double)value);

                case TypeCode.SByte:
                    return Runtime.PyInt_FromInt32((int)((sbyte)value));

                case TypeCode.UInt16:
                    return Runtime.PyInt_FromInt32((int)((ushort)value));

                case TypeCode.UInt32:
                    return Runtime.PyLong_FromUnsignedLong((uint)value);

                case TypeCode.UInt64:
                    return Runtime.PyLong_FromUnsignedLongLong((ulong)value);

                default:
                    if (value is IEnumerable)
                    {
                        using (var resultlist = new PyList())
                        {
                            foreach (object o in (IEnumerable)value)
                            {
                                using (var p = new PyObject(ToPython(o, o?.GetType())))
                                {
                                    resultlist.Append(p);
                                }
                            }
                            Runtime.XIncref(resultlist.Handle);
                            return resultlist.Handle;
                        }
                    }
                    result = CLRObject.GetInstHandle(value, type);
                    return result;
            }
        }


        /// <summary>
        /// In a few situations, we don't have any advisory type information
        /// when we want to convert an object to Python.
        /// </summary>
        internal static IntPtr ToPythonImplicit(object value)
        {
            if (value == null)
            {
                IntPtr result = Runtime.PyNone;
                Runtime.XIncref(result);
                return result;
            }

            return ToPython(value, objectType);
        }


        /// <summary>
        /// Return a managed object for the given Python object, taking funny
        /// byref types into account.
        /// </summary>
        /// <param name="value">A Python object</param>
        /// <param name="type">The desired managed type</param>
        /// <param name="result">Receives the managed object</param>
        /// <param name="setError">If true, call <c>Exceptions.SetError</c> with the reason for failure.</param>
        /// <returns>True on success</returns>
        internal static bool ToManaged(IntPtr value, Type type,
            out object result, bool setError)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            return Converter.ToManagedValue(value, type, out result, setError);
        }

        internal static bool ToManagedValue(BorrowedReference value, Type obType,
            out object result, bool setError)
            => ToManagedValue(value.DangerousGetAddress(), obType, out result, setError);
        internal static bool ToManagedValue(IntPtr value, Type obType,
            out object result, bool setError)
        {
            if (obType == typeof(PyObject))
            {
                Runtime.XIncref(value); // PyObject() assumes ownership
                result = new PyObject(value);
                return true;
            }

            // Common case: if the Python value is a wrapped managed object
            // instance, just return the wrapped object.
            ManagedType mt = ManagedType.GetManagedObject(value);
            result = null;

            if (mt != null)
            {
                if (mt is CLRObject co)
                {
                    object tmp = co.inst;
                    if (obType.IsInstanceOfType(tmp))
                    {
                        result = tmp;
                        return true;
                    }
                    if (setError)
                    {
                        string typeString = tmp is null ? "null" : tmp.GetType().ToString();
                        Exceptions.SetError(Exceptions.TypeError, $"{typeString} value cannot be converted to {obType}");
                    }
                    return false;
                }
                if (mt is ClassBase cb)
                {
                    if (!cb.type.Valid)
                    {
                        Exceptions.SetError(Exceptions.TypeError, cb.type.DeletedMessage);
                        return false;
                    }
                    result = cb.type.Value;
                    return true;
                }
                // shouldn't happen
                return false;
            }

            if (value == Runtime.PyNone && !obType.IsValueType)
            {
                result = null;
                return true;
            }

            if (obType.IsGenericType && obType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if( value == Runtime.PyNone )
                {
                    result = null;
                    return true;
                }
                // Set type to underlying type
                obType = obType.GetGenericArguments()[0];
            }

            if (obType.ContainsGenericParameters)
            {
                if (setError)
                {
                    Exceptions.SetError(Exceptions.TypeError, $"Cannot create an instance of the open generic type {obType}");
                }
                return false;
            }

            if (obType.IsArray)
            {
                return ToArray(value, obType, out result, setError);
            }

            if (obType.IsEnum)
            {
                return ToEnum(value, obType, out result, setError);
            }

            // Conversion to 'Object' is done based on some reasonable default
            // conversions (Python string -> managed string, Python int -> Int32 etc.).
            if (obType == objectType)
            {
                if (Runtime.IsStringType(value))
                {
                    return ToPrimitive(value, stringType, out result, setError);
                }

                if (Runtime.PyBool_Check(value))
                {
                    return ToPrimitive(value, boolType, out result, setError);
                }

                if (Runtime.PyInt_Check(value))
                {
                    return ToPrimitive(value, int32Type, out result, setError);
                }

                if (Runtime.PyLong_Check(value))
                {
                    return ToPrimitive(value, int64Type, out result, setError);
                }

                if (Runtime.PyFloat_Check(value))
                {
                    return ToPrimitive(value, doubleType, out result, setError);
                }

                // give custom codecs a chance to take over conversion of sequences
                IntPtr pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }

                if (Runtime.PySequence_Check(value))
                {
                    return ToArray(value, typeof(object[]), out result, setError);
                }

                Runtime.XIncref(value); // PyObject() assumes ownership
                result = new PyObject(value);
                return true;
            }

            // Conversion to 'Type' is done using the same mappings as above for objects.
            if (obType == typeType)
            {
                if (value == Runtime.PyStringType)
                {
                    result = stringType;
                    return true;
                }

                if (value == Runtime.PyBoolType)
                {
                    result = boolType;
                    return true;
                }

                if (value == Runtime.PyIntType)
                {
                    result = int32Type;
                    return true;
                }

                if (value == Runtime.PyLongType)
                {
                    result = int64Type;
                    return true;
                }

                if (value == Runtime.PyFloatType)
                {
                    result = doubleType;
                    return true;
                }

                if (value == Runtime.PyListType || value == Runtime.PyTupleType)
                {
                    result = typeof(object[]);
                    return true;
                }

                if (setError)
                {
                    Exceptions.SetError(Exceptions.TypeError, "value cannot be converted to Type");
                }

                return false;
            }

            TypeCode typeCode = Type.GetTypeCode(obType);
            if (typeCode == TypeCode.Object)
            {
                IntPtr pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }
            }

            return ToPrimitive(value, obType, out result, setError);
        }

        internal delegate bool TryConvertFromPythonDelegate(IntPtr pyObj, out object result);

        /// <summary>
        /// Convert a Python value to an instance of a primitive managed type.
        /// </summary>
        private static bool ToPrimitive(IntPtr value, Type obType, out object result, bool setError)
        {
            TypeCode tc = Type.GetTypeCode(obType);
            result = null;
            IntPtr op = IntPtr.Zero;

            switch (tc)
            {
                case TypeCode.String:
                    string st = Runtime.GetManagedString(value);
                    if (st == null)
                    {
                        goto type_error;
                    }
                    result = st;
                    return true;

                case TypeCode.Int32:
                    {
                        // Python3 always use PyLong API
                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > Int32.MaxValue || num < Int32.MinValue)
                        {
                            goto overflow;
                        }
                        result = (int)num;
                        return true;
                    }

                case TypeCode.Boolean:
                    result = Runtime.PyObject_IsTrue(value) != 0;
                    return true;

                case TypeCode.Byte:
                    {
                        if (Runtime.PyObject_TypeCheck(value, Runtime.PyBytesType))
                        {
                            if (Runtime.PyBytes_Size(value) == 1)
                            {
                                op = Runtime.PyBytes_AS_STRING(value);
                                result = (byte)Marshal.ReadByte(op);
                                return true;
                            }
                            goto type_error;
                        }

                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > Byte.MaxValue || num < Byte.MinValue)
                        {
                            goto overflow;
                        }
                        result = (byte)num;
                        return true;
                    }

                case TypeCode.SByte:
                    {
                        if (Runtime.PyObject_TypeCheck(value, Runtime.PyBytesType))
                        {
                            if (Runtime.PyBytes_Size(value) == 1)
                            {
                                op = Runtime.PyBytes_AS_STRING(value);
                                result = (byte)Marshal.ReadByte(op);
                                return true;
                            }
                            goto type_error;
                        }

                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > SByte.MaxValue || num < SByte.MinValue)
                        {
                            goto overflow;
                        }
                        result = (sbyte)num;
                        return true;
                    }

                case TypeCode.Char:
                    {
                        if (Runtime.PyObject_TypeCheck(value, Runtime.PyBytesType))
                        {
                            if (Runtime.PyBytes_Size(value) == 1)
                            {
                                op = Runtime.PyBytes_AS_STRING(value);
                                result = (byte)Marshal.ReadByte(op);
                                return true;
                            }
                            goto type_error;
                        }
                        else if (Runtime.PyObject_TypeCheck(value, Runtime.PyUnicodeType))
                        {
                            if (Runtime.PyUnicode_GetSize(value) == 1)
                            {
                                op = Runtime.PyUnicode_AsUnicode(value);
                                Char[] buff = new Char[1];
                                Marshal.Copy(op, buff, 0, 1);
                                result = buff[0];
                                return true;
                            }
                            goto type_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > Char.MaxValue || num < Char.MinValue)
                        {
                            goto overflow;
                        }
                        result = (char)num;
                        return true;
                    }

                case TypeCode.Int16:
                    {
                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > Int16.MaxValue || num < Int16.MinValue)
                        {
                            goto overflow;
                        }
                        result = (short)num;
                        return true;
                    }

                case TypeCode.Int64:
                    {
                        if (Runtime.Is32Bit)
                        {
                            if (!Runtime.PyLong_Check(value))
                            {
                                goto type_error;
                            }
                            long num = Runtime.PyExplicitlyConvertToInt64(value);
                            if (num == -1 && Exceptions.ErrorOccurred())
                            {
                                goto convert_error;
                            }
                            result = num;
                            return true;
                        }
                        else
                        {
                            nint num = Runtime.PyLong_AsSignedSize_t(value);
                            if (num == -1 && Exceptions.ErrorOccurred())
                            {
                                goto convert_error;
                            }
                            result = (long)num;
                            return true;
                        }
                    }

                case TypeCode.UInt16:
                    {
                        nint num = Runtime.PyLong_AsSignedSize_t(value);
                        if (num == -1 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > UInt16.MaxValue || num < UInt16.MinValue)
                        {
                            goto overflow;
                        }
                        result = (ushort)num;
                        return true;
                    }

                case TypeCode.UInt32:
                    {
                        nuint num = Runtime.PyLong_AsUnsignedSize_t(value);
                        if (num == unchecked((nuint)(-1)) && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > UInt32.MaxValue)
                        {
                            goto overflow;
                        }
                        result = (uint)num;
                        return true;
                    }

                case TypeCode.UInt64:
                    {
                        ulong num = Runtime.PyLong_AsUnsignedLongLong(value);
                        if (num == ulong.MaxValue && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        result = num;
                        return true;
                    }

                case TypeCode.Single:
                    {
                        double num = Runtime.PyFloat_AsDouble(value);
                        if (num == -1.0 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        if (num > Single.MaxValue || num < Single.MinValue)
                        {
                            if (!double.IsInfinity(num))
                            {
                                goto overflow;
                            }
                        }
                        result = (float)num;
                        return true;
                    }

                case TypeCode.Double:
                    {
                        double num = Runtime.PyFloat_AsDouble(value);
                        if (num == -1.0 && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        result = num;
                        return true;
                    }
                default:
                    goto type_error;
            }

        convert_error:
            if (op != value)
            {
                Runtime.XDecref(op);
            }
            if (!setError)
            {
                Exceptions.Clear();
            }
            return false;

        type_error:
            if (setError)
            {
                string tpName = Runtime.PyObject_GetTypeName(value);
                Exceptions.SetError(Exceptions.TypeError, $"'{tpName}' value cannot be converted to {obType}");
            }
            return false;

        overflow:
            // C# level overflow error
            if (op != value)
            {
                Runtime.XDecref(op);
            }
            if (setError)
            {
                Exceptions.SetError(Exceptions.OverflowError, "value too large to convert");
            }
            return false;
        }


        private static void SetConversionError(IntPtr value, Type target)
        {
            IntPtr ob = Runtime.PyObject_Repr(value);
            string src = Runtime.GetManagedString(ob);
            Runtime.XDecref(ob);
            Exceptions.RaiseTypeError($"Cannot convert {src} to {target}");
        }


        /// <summary>
        /// Convert a Python value to a correctly typed managed array instance.
        /// The Python value must support the Python iterator protocol or and the
        /// items in the sequence must be convertible to the target array type.
        /// </summary>
        private static bool ToArray(IntPtr value, Type obType, out object result, bool setError)
        {
            Type elementType = obType.GetElementType();
            result = null;

            IntPtr IterObject = Runtime.PyObject_GetIter(value);
            if (IterObject == IntPtr.Zero)
            {
                if (setError)
                {
                    SetConversionError(value, obType);
                }
                else
                {
                    // PyObject_GetIter will have set an error
                    Exceptions.Clear();
                }
                return false;
            }

            IList list;
            try
            {
                // MakeGenericType can throw because elementType may not be a valid generic argument even though elementType[] is a valid array type.
                // For example, if elementType is a pointer type.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.type.makegenerictype#System_Type_MakeGenericType_System_Type
                var constructedListType = typeof(List<>).MakeGenericType(elementType);
                bool IsSeqObj = Runtime.PySequence_Check(value);
                if (IsSeqObj)
                {
                    var len = Runtime.PySequence_Size(value);
                    list = (IList)Activator.CreateInstance(constructedListType, new Object[] { (int)len });
                }
                else
                {
                    // CreateInstance can throw even if MakeGenericType succeeded.
                    // See https://docs.microsoft.com/en-us/dotnet/api/system.activator.createinstance#System_Activator_CreateInstance_System_Type_
                    list = (IList)Activator.CreateInstance(constructedListType);
                }
            }
            catch (Exception e)
            {
                if (setError)
                {
                    Exceptions.SetError(e);
                    SetConversionError(value, obType);
                }
                return false;
            }

            IntPtr item;

            while ((item = Runtime.PyIter_Next(IterObject)) != IntPtr.Zero)
            {
                object obj;

                if (!Converter.ToManaged(item, elementType, out obj, setError))
                {
                    Runtime.XDecref(item);
                    return false;
                }

                list.Add(obj);
                Runtime.XDecref(item);
            }
            Runtime.XDecref(IterObject);

            Array items = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(items, 0);

            result = items;
            return true;
        }


        /// <summary>
        /// Convert a Python value to a correctly typed managed enum instance.
        /// </summary>
        private static bool ToEnum(IntPtr value, Type obType, out object result, bool setError)
        {
            Type etype = Enum.GetUnderlyingType(obType);
            result = null;

            if (!ToPrimitive(value, etype, out result, setError))
            {
                return false;
            }

            if (Enum.IsDefined(obType, result))
            {
                result = Enum.ToObject(obType, result);
                return true;
            }

            if (obType.GetCustomAttributes(flagsType, true).Length > 0)
            {
                result = Enum.ToObject(obType, result);
                return true;
            }

            if (setError)
            {
                Exceptions.SetError(Exceptions.ValueError, "invalid enumeration value");
            }

            return false;
        }
    }

    public static class ConverterExtension
    {
        public static PyObject ToPython(this object o)
        {
            return new PyObject(Converter.ToPython(o, o?.GetType()));
        }
    }
}
