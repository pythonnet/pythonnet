using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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

        private static readonly Type objectType;
        private static readonly Type stringType;
        private static readonly Type singleType;
        private static readonly Type doubleType;
        private static readonly Type int16Type;
        private static readonly Type int32Type;
        private static readonly Type int64Type;
        private static readonly Type boolType;
        private static readonly Type typeType;

        static Converter()
        {
            objectType = typeof(Object);
            stringType = typeof(String);
            int16Type = typeof(Int16);
            int32Type = typeof(Int32);
            int64Type = typeof(Int64);
            singleType = typeof(Single);
            doubleType = typeof(Double);
            boolType = typeof(Boolean);
            typeType = typeof(Type);
        }


        /// <summary>
        /// Given a builtin Python type, return the corresponding CLR type.
        /// </summary>
        internal static Type? GetTypeByAlias(BorrowedReference op)
        {
            if (op == Runtime.PyStringType)
                return stringType;

            if (op == Runtime.PyUnicodeType)
                return stringType;

            if (op == Runtime.PyLongType)
                return int32Type;

            if (op == Runtime.PyLongType)
                return int64Type;

            if (op == Runtime.PyFloatType)
                return doubleType;

            if (op == Runtime.PyBoolType)
                return boolType;

            return null;
        }

        internal static BorrowedReference GetPythonTypeByAlias(Type op)
        {
            if (op == stringType)
                return Runtime.PyUnicodeType.Reference;

            if (op == int16Type)
                return Runtime.PyLongType.Reference;

            if (op == int32Type)
                return Runtime.PyLongType.Reference;

            if (op == int64Type)
                return Runtime.PyLongType.Reference;

            if (op == doubleType)
                return Runtime.PyFloatType.Reference;

            if (op == singleType)
                return Runtime.PyFloatType.Reference;

            if (op == boolType)
                return Runtime.PyBoolType.Reference;

            return BorrowedReference.Null;
        }


        internal static NewReference ToPython<T>(T value)
            => ToPython(value, typeof(T));

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

        internal static NewReference ToPythonDetectType(object? value)
            => value is null ? new NewReference(Runtime.PyNone) : ToPython(value, value.GetType());
        internal static NewReference ToPython(object? value, Type type)
        {
            if (value is PyObject pyObj)
            {
                return new NewReference(pyObj);
            }

            // Null always converts to None in Python.
            if (value == null)
            {
                return new NewReference(Runtime.PyNone);
            }

            if (EncodableByUser(type, value))
            {
                var encoded = PyObjectConversions.TryEncode(value, type);
                if (encoded != null) {
                    return new NewReference(encoded);
                }
            }

            if (type.IsInterface)
            {
                var ifaceObj = (InterfaceObject)ClassManager.GetClassImpl(type);
                return ifaceObj.TryWrapObject(value);
            }

            if (type.IsArray || type.IsEnum)
            {
                return CLRObject.GetReference(value, type);
            }

            // it the type is a python subclass of a managed type then return the
            // underlying python object rather than construct a new wrapper object.
            if (value is IPythonDerivedType pyderived)
            {
                if (!IsTransparentProxy(pyderived))
                    return ClassDerivedObject.ToPython(pyderived);
            }

            // ModuleObjects are created in a way that their wrapping them as
            // a CLRObject fails, the ClassObject has no tpHandle. Return the
            // pyHandle as is, do not convert.
            if (value is ModuleObject)
            {
                throw new NotImplementedException();
            }

            // hmm - from Python, we almost never care what the declared
            // type is. we'd rather have the object bound to the actual
            // implementing class.

            type = value.GetType();

            if (type.IsEnum)
            {
                return CLRObject.GetReference(value, type);
            }

            TypeCode tc = Type.GetTypeCode(type);

            switch (tc)
            {
                case TypeCode.Object:
                    return CLRObject.GetReference(value, type);

                case TypeCode.String:
                    return Runtime.PyString_FromString((string)value);

                case TypeCode.Int32:
                    return Runtime.PyInt_FromInt32((int)value);

                case TypeCode.Boolean:
                    if ((bool)value)
                    {
                        return new NewReference(Runtime.PyTrue);
                    }
                    return new NewReference(Runtime.PyFalse);

                case TypeCode.Byte:
                    return Runtime.PyInt_FromInt32((byte)value);

                case TypeCode.Char:
                    return Runtime.PyUnicode_FromOrdinal((int)((char)value));

                case TypeCode.Int16:
                    return Runtime.PyInt_FromInt32((short)value);

                case TypeCode.Int64:
                    return Runtime.PyLong_FromLongLong((long)value);

                case TypeCode.Single:
                    return Runtime.PyFloat_FromDouble((float)value);

                case TypeCode.Double:
                    return Runtime.PyFloat_FromDouble((double)value);

                case TypeCode.SByte:
                    return Runtime.PyInt_FromInt32((sbyte)value);

                case TypeCode.UInt16:
                    return Runtime.PyInt_FromInt32((ushort)value);

                case TypeCode.UInt32:
                    return Runtime.PyLong_FromUnsignedLongLong((uint)value);

                case TypeCode.UInt64:
                    return Runtime.PyLong_FromUnsignedLongLong((ulong)value);

                default:
                    return CLRObject.GetReference(value, type);
            }
        }

        static bool EncodableByUser(Type type, object value)
        {
            TypeCode typeCode = Type.GetTypeCode(type);
            return type.IsEnum
                   || typeCode is TypeCode.DateTime or TypeCode.Decimal
                   || typeCode == TypeCode.Object && value.GetType() != typeof(object) && value is not Type;
        }

        /// <summary>
        /// In a few situations, we don't have any advisory type information
        /// when we want to convert an object to Python.
        /// </summary>
        internal static NewReference ToPythonImplicit(object? value)
        {
            if (value == null)
            {
                return new NewReference(Runtime.PyNone);
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
        internal static bool ToManaged(BorrowedReference value, Type type,
            out object? result, bool setError)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            return Converter.ToManagedValue(value, type, out result, setError);
        }

        internal static bool ToManagedValue(BorrowedReference value, Type obType,
            out object? result, bool setError)
        {
            if (obType == typeof(PyObject))
            {
                result = new PyObject(value);
                return true;
            }

            if (obType.IsSubclassOf(typeof(PyObject))
                && !obType.IsAbstract
                && obType.GetConstructor(new[] { typeof(PyObject) }) is { } ctor)
            {
                var untyped = new PyObject(value);
                result = ToPyObjectSubclass(ctor, untyped, setError);
                return result is not null;
            }

            // Common case: if the Python value is a wrapped managed object
            // instance, just return the wrapped object.
            result = null;
            switch (ManagedType.GetManagedObject(value))
            {
                case CLRObject co:
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

                case ClassBase cb:
                    if (!cb.type.Valid)
                    {
                        Exceptions.SetError(Exceptions.TypeError, cb.type.DeletedMessage);
                        return false;
                    }
                    result = cb.type.Value;
                    return true;

                case null:
                    break;

                default:
                    throw new ArgumentException("We should never receive instances of other managed types");
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

            // Conversion to 'Object' is done based on some reasonable default
            // conversions (Python string -> managed string).
            if (obType == objectType)
            {
                if (Runtime.PyString_CheckExact(value))
                {
                    return ToPrimitive(value, stringType, out result, setError);
                }

                if (Runtime.PyBool_CheckExact(value))
                {
                    return ToPrimitive(value, boolType, out result, setError);
                }

                if (Runtime.PyFloat_CheckExact(value))
                {
                    return ToPrimitive(value, doubleType, out result, setError);
                }

                // give custom codecs a chance to take over conversion
                // of ints, sequences, and types derived from primitives
                BorrowedReference pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }

                if (Runtime.PyString_Check(value))
                {
                    return ToPrimitive(value, stringType, out result, setError);
                }

                if (Runtime.PyBool_Check(value))
                {
                    return ToPrimitive(value, boolType, out result, setError);
                }

                if (Runtime.PyFloat_Check(value))
                {
                    return ToPrimitive(value, doubleType, out result, setError);
                }

                if (Runtime.PyInt_Check(value))
                {
                    result = new PyInt(value);
                    return true;
                }

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

                if (value == Runtime.PyLongType)
                {
                    result = typeof(PyInt);
                    return true;
                }

                if (value == Runtime.PyFloatType)
                {
                    result = doubleType;
                    return true;
                }

                if (value == Runtime.PyListType)
                {
                    result = typeof(PyList);
                    return true;
                }

                if (value == Runtime.PyTupleType)
                {
                    result = typeof(PyTuple);
                    return true;
                }

                if (setError)
                {
                    Exceptions.SetError(Exceptions.TypeError, "value cannot be converted to Type");
                }

                return false;
            }

            if (DecodableByUser(obType))
            {
                BorrowedReference pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }
            }

            if (obType == typeof(System.Numerics.BigInteger)
                && Runtime.PyInt_Check(value))
            {
                using var pyInt = new PyInt(value);
                result = pyInt.ToBigInteger();
                return true;
            }

            return ToPrimitive(value, obType, out result, setError);
        }

        /// <remarks>
        /// Unlike <see cref="ToManaged(BorrowedReference, Type, out object?, bool)"/>,
        /// this method does not have a <c>setError</c> parameter, because it should
        /// only be called after <see cref="ToManaged(BorrowedReference, Type, out object?, bool)"/>.
        /// </remarks>
        internal static bool ToManagedExplicit(BorrowedReference value, Type obType,
            out object? result)
        {
            result = null;

            // this method would potentially clean any existing error resulting in information loss
            Debug.Assert(Runtime.PyErr_Occurred() == null);

            string? converterName =
                  IsInteger(obType) ? "__int__"
                : IsFloatingNumber(obType) ? "__float__"
                : null;

            if (converterName is null) return false;

            Debug.Assert(obType.IsPrimitive);

            using var converter = Runtime.PyObject_GetAttrString(value, converterName);
            if (converter.IsNull())
            {
                Exceptions.Clear();
                return false;
            }

            using var explicitlyCoerced = Runtime.PyObject_CallObject(converter.Borrow(), BorrowedReference.Null);
            if (explicitlyCoerced.IsNull())
            {
                Exceptions.Clear();
                return false;
            }
            return ToPrimitive(explicitlyCoerced.Borrow(), obType, out result, false);
        }

        static object? ToPyObjectSubclass(ConstructorInfo ctor, PyObject instance, bool setError)
        {
            try
            {
                return ctor.Invoke(new object[] { instance });
            }
            catch (TargetInvocationException ex)
            {
                if (setError)
                {
                    Exceptions.SetError(ex.InnerException);
                }
                return null;
            }
            catch (SecurityException ex)
            {
                if (setError)
                {
                    Exceptions.SetError(ex);
                }
                return null;
            }
        }

        static bool DecodableByUser(Type type)
        {
            TypeCode typeCode = Type.GetTypeCode(type);
            return type.IsEnum
                   || typeCode is TypeCode.Object or TypeCode.Decimal or TypeCode.DateTime;
        }

        internal delegate bool TryConvertFromPythonDelegate(BorrowedReference pyObj, out object? result);

        internal static int ToInt32(BorrowedReference value)
        {
            nint num = Runtime.PyLong_AsSignedSize_t(value);
            if (num == -1 && Exceptions.ErrorOccurred())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return checked((int)num);
        }

        /// <summary>
        /// Convert a Python value to an instance of a primitive managed type.
        /// </summary>
        internal static bool ToPrimitive(BorrowedReference value, Type obType, out object? result, bool setError)
        {
            result = null;
            if (obType.IsEnum)
            {
                if (setError)
                {
                    Exceptions.SetError(Exceptions.TypeError, "since Python.NET 3.0 int can not be converted to Enum implicitly. Use Enum(int_value)");
                }
                return false;
            }

            TypeCode tc = Type.GetTypeCode(obType);

            switch (tc)
            {
                case TypeCode.String:
                    string? st = Runtime.GetManagedString(value);
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
                    if (value == Runtime.PyTrue)
                    {
                        result = true;
                        return true;
                    }
                    if (value == Runtime.PyFalse)
                    {
                        result = false;
                        return true;
                    }
                    if (setError)
                    {
                        goto type_error;
                    }
                    return false;

                case TypeCode.Byte:
                    {
                        if (Runtime.PyObject_TypeCheck(value, Runtime.PyBytesType))
                        {
                            if (Runtime.PyBytes_Size(value) == 1)
                            {
                                IntPtr bytePtr = Runtime.PyBytes_AsString(value);
                                result = (byte)Marshal.ReadByte(bytePtr);
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
                                IntPtr bytePtr = Runtime.PyBytes_AsString(value);
                                result = (sbyte)Marshal.ReadByte(bytePtr);
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
                                IntPtr bytePtr = Runtime.PyBytes_AsString(value);
                                result = (char)Marshal.ReadByte(bytePtr);
                                return true;
                            }
                            goto type_error;
                        }
                        else if (Runtime.PyObject_TypeCheck(value, Runtime.PyUnicodeType))
                        {
                            if (Runtime.PyUnicode_GetLength(value) == 1)
                            {
                                IntPtr unicodePtr = Runtime.PyUnicode_AsUnicode(value);
                                Char[] buff = new Char[1];
                                Marshal.Copy(unicodePtr, buff, 0, 1);
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
                            if (!Runtime.PyInt_Check(value))
                            {
                                goto type_error;
                            }
                            long? num = Runtime.PyLong_AsLongLong(value);
                            if (num is null)
                            {
                                goto convert_error;
                            }
                            result = num.Value;
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
                        ulong? num = Runtime.PyLong_AsUnsignedLongLong(value);
                        if (num is null)
                        {
                            goto convert_error;
                        }
                        result = num.Value;
                        return true;
                    }

                case TypeCode.Single:
                    {
                        if (!Runtime.PyFloat_Check(value) && !Runtime.PyInt_Check(value))
                        {
                            goto type_error;
                        }
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
                        if (!Runtime.PyFloat_Check(value) && !Runtime.PyInt_Check(value))
                        {
                            goto type_error;
                        }
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
            if (setError)
            {
                Exceptions.SetError(Exceptions.OverflowError, "value too large to convert");
            }
            return false;
        }

        private static void SetConversionError(BorrowedReference value, Type target)
        {
            // PyObject_Repr might clear the error
            Runtime.PyErr_Fetch(out var causeType, out var causeVal, out var causeTrace);

            var ob = Runtime.PyObject_Repr(value);
            string src = "'object has no repr'";
            if (ob.IsNull())
            {
                Exceptions.Clear();
            }
            else
            {
                src = Runtime.GetManagedString(ob.Borrow()) ?? src;
            }
            ob.Dispose();

            Runtime.PyErr_Restore(causeType.StealNullable(), causeVal.StealNullable(), causeTrace.StealNullable());
            Exceptions.RaiseTypeError($"Cannot convert {src} to {target}");
        }


        /// <summary>
        /// Convert a Python value to a correctly typed managed array instance.
        /// The Python value must support the Python iterator protocol or and the
        /// items in the sequence must be convertible to the target array type.
        /// </summary>
        private static bool ToArray(BorrowedReference value, Type obType, out object? result, bool setError)
        {
            Type elementType = obType.GetElementType();
            result = null;

            using var IterObject = Runtime.PyObject_GetIter(value);
            if (IterObject.IsNull())
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
                object[] constructorArgs = Array.Empty<object>();
                if (IsSeqObj)
                {
                    var len = Runtime.PySequence_Size(value);
                    if (len >= 0)
                    {
                        if (len <= int.MaxValue)
                        {
                            constructorArgs = new object[] { (int)len };
                        }
                    }
                    else
                    {
                        // for the sequences, that explicitly deny calling __len__()
                        Exceptions.Clear();
                    }
                }
                // CreateInstance can throw even if MakeGenericType succeeded.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.activator.createinstance#System_Activator_CreateInstance_System_Type_
                list = (IList)Activator.CreateInstance(constructedListType, args: constructorArgs);
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

            while (true)
            {
                using var item = Runtime.PyIter_Next(IterObject.Borrow());
                if (item.IsNull()) break;

                if (!Converter.ToManaged(item.Borrow(), elementType, out var obj, setError))
                {
                    return false;
                }

                list.Add(obj);
            }

            if (Exceptions.ErrorOccurred())
            {
                if (!setError) Exceptions.Clear();
                return false;
            }

            Array items = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(items, 0);

            result = items;
            return true;
        }

        internal static bool IsFloatingNumber(Type type) => type == typeof(float) || type == typeof(double);
        internal static bool IsInteger(Type type)
            => type == typeof(Byte) || type == typeof(SByte)
            || type == typeof(Int16) || type == typeof(UInt16)
            || type == typeof(Int32) || type == typeof(UInt32)
            || type == typeof(Int64) || type == typeof(UInt64);
    }

    public static class ConverterExtension
    {
        public static PyObject ToPython(this object? o)
        {
            if (o is null) return Runtime.None;
            return Converter.ToPython(o, o.GetType()).MoveToPyObject();
        }
    }
}
