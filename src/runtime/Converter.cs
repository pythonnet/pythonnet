using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

using Python.Runtime.Native;

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
        private static PyObject dateTimeCtor;
        private static PyObject timeSpanCtor;
        private static Lazy<PyObject> tzInfoCtor;
        private static PyObject pyTupleNoKind;
        private static PyObject pyTupleKind;

        private static StrPtr yearPtr;
        private static StrPtr monthPtr;
        private static StrPtr dayPtr;
        private static StrPtr hourPtr;
        private static StrPtr minutePtr;
        private static StrPtr secondPtr;
        private static StrPtr microsecondPtr;

        private static StrPtr tzinfoPtr;
        private static StrPtr hoursPtr;
        private static StrPtr minutesPtr;

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

            var dateTimeMod = Runtime.PyImport_ImportModule("datetime");
            PythonException.ThrowIfIsNull(dateTimeMod);

            dateTimeCtor = Runtime.PyObject_GetAttrString(dateTimeMod.Borrow(), "datetime").MoveToPyObject();
            PythonException.ThrowIfIsNull(dateTimeCtor);

            timeSpanCtor = Runtime.PyObject_GetAttrString(dateTimeMod.Borrow(), "timedelta").MoveToPyObject();
            PythonException.ThrowIfIsNull(timeSpanCtor);

            tzInfoCtor = new Lazy<PyObject>(() =>
            {
                var tzInfoMod = PyModule.FromString("custom_tzinfo", @"
from datetime import timedelta, tzinfo
class GMT(tzinfo):
    def __init__(self, hours, minutes):
        self.hours = hours
        self.minutes = minutes
    def utcoffset(self, dt):
        return timedelta(hours=self.hours, minutes=self.minutes)
    def tzname(self, dt):
        return f'GMT {self.hours:00}:{self.minutes:00}'
    def dst (self, dt):
        return timedelta(0)").BorrowNullable();

                var result = Runtime.PyObject_GetAttrString(tzInfoMod, "GMT").MoveToPyObject();
                PythonException.ThrowIfIsNull(result);
                return result;
            });

            pyTupleNoKind = Runtime.PyTuple_New(7).MoveToPyObject();
            pyTupleKind = Runtime.PyTuple_New(8).MoveToPyObject();

            yearPtr = new StrPtr("year", Encoding.UTF8);
            monthPtr = new StrPtr("month", Encoding.UTF8);
            dayPtr = new StrPtr("day", Encoding.UTF8);
            hourPtr = new StrPtr("hour", Encoding.UTF8);
            minutePtr = new StrPtr("minute", Encoding.UTF8);
            secondPtr = new StrPtr("second", Encoding.UTF8);
            microsecondPtr = new StrPtr("microsecond", Encoding.UTF8);

            tzinfoPtr = new StrPtr("tzinfo", Encoding.UTF8);
            hoursPtr = new StrPtr("hours", Encoding.UTF8);
            minutesPtr = new StrPtr("minutes", Encoding.UTF8);
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

            if (op == Runtime.PyDecimalType.Value)
                return decimalType;

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

            if (op == decimalType)
                return Runtime.PyDecimalType.Value.Reference;

            return BorrowedReference.Null;
        }


        /// <summary>
        /// Return a Python object for the given native object, converting
        /// basic types (string, int, etc.) into equivalent Python objects.
        /// This always returns a new reference. Note that the System.Decimal
        /// type has no Python equivalent and converts to a managed instance.
        /// </summary>
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

            type = value.GetType();
            if (type.IsGenericType && value is IList && !(value is INotifyPropertyChanged))
            {
                using var resultlist = new PyList();
                foreach (object o in (IEnumerable)value)
                {
                    using var p = o.ToPython();
                    resultlist.Append(p);
                }
                return resultlist.NewReferenceOrNull();
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

            TypeCode tc = Type.GetTypeCode(type);

            switch (tc)
            {
                case TypeCode.Object:
                    if (value is TimeSpan)
                    {
                        var timespan = (TimeSpan)value;

                        using var timeSpanArgs = Runtime.PyTuple_New(1);
                        Runtime.PyTuple_SetItem(timeSpanArgs.Borrow(), 0, Runtime.PyFloat_FromDouble(timespan.TotalDays).Steal());
                        var returnTimeSpan = Runtime.PyObject_CallObject(timeSpanCtor, timeSpanArgs.Borrow());

                        return returnTimeSpan;
                    }
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
                    return Runtime.PyInt_FromInt32((int)((byte)value));

                case TypeCode.Char:
                    return Runtime.PyUnicode_FromOrdinal((int)((char)value));

                case TypeCode.Int16:
                    return Runtime.PyInt_FromInt32((int)((short)value));

                case TypeCode.Int64:
                    return Runtime.PyLong_FromLongLong((long)value);

                case TypeCode.Single:
                    return Runtime.PyFloat_FromDouble((float)value);

                case TypeCode.Double:
                    return Runtime.PyFloat_FromDouble((double)value);

                case TypeCode.SByte:
                    return Runtime.PyInt_FromInt32((int)((sbyte)value));

                case TypeCode.UInt16:
                    return Runtime.PyInt_FromInt32((int)((ushort)value));

                case TypeCode.UInt32:
                    return Runtime.PyLong_FromUnsignedLongLong((uint)value);

                case TypeCode.UInt64:
                    return Runtime.PyLong_FromUnsignedLongLong((ulong)value);

                case TypeCode.Decimal:
                    // C# decimal to python decimal has a big impact on performance
                    // so we will use C# double and python float
                    return Runtime.PyFloat_FromDouble(decimal.ToDouble((decimal)value));

                case TypeCode.DateTime:
                    var datetime = (DateTime)value;

                    var size = datetime.Kind == DateTimeKind.Unspecified ? 7 : 8;

                    var dateTimeArgs = datetime.Kind == DateTimeKind.Unspecified ? pyTupleNoKind : pyTupleKind;
                    Runtime.PyTuple_SetItem(dateTimeArgs, 0, Runtime.PyInt_FromInt32(datetime.Year).Steal());
                    Runtime.PyTuple_SetItem(dateTimeArgs, 1, Runtime.PyInt_FromInt32(datetime.Month).Steal());
                    Runtime.PyTuple_SetItem(dateTimeArgs, 2, Runtime.PyInt_FromInt32(datetime.Day).Steal());
                    Runtime.PyTuple_SetItem(dateTimeArgs, 3, Runtime.PyInt_FromInt32(datetime.Hour).Steal());
                    Runtime.PyTuple_SetItem(dateTimeArgs, 4, Runtime.PyInt_FromInt32(datetime.Minute).Steal());
                    Runtime.PyTuple_SetItem(dateTimeArgs, 5, Runtime.PyInt_FromInt32(datetime.Second).Steal());

                    // datetime.datetime 6th argument represents micro seconds
                    var totalSeconds = datetime.TimeOfDay.TotalSeconds;
                    var microSeconds = Convert.ToInt32((totalSeconds - Math.Truncate(totalSeconds)) * 1000000);
                    if (microSeconds == 1000000) microSeconds = 999999;
                    Runtime.PyTuple_SetItem(dateTimeArgs, 6, Runtime.PyInt_FromInt32(microSeconds).Steal());

                    if (size == 8)
                    {
                        Runtime.PyTuple_SetItem(dateTimeArgs, 7, TzInfo(datetime.Kind).Steal());
                    }

                    var returnDateTime = Runtime.PyObject_CallObject(dateTimeCtor, dateTimeArgs);
                    return returnDateTime;


                default:
                    if (value is IEnumerable)
                    {
                        using var resultlist = new PyList();
                        foreach (object o in (IEnumerable)value)
                        {
                            using var p = o.ToPython();
                            resultlist.Append(p);
                        }
                        return resultlist.NewReferenceOrNull();
                    }
                    return CLRObject.GetReference(value, type);
            }
        }

        private static NewReference TzInfo(DateTimeKind kind)
        {
            if (kind == DateTimeKind.Unspecified) return new NewReference(Runtime.PyNone);
            var offset = kind == DateTimeKind.Local ? DateTimeOffset.Now.Offset : TimeSpan.Zero;
            using var tzInfoArgs = Runtime.PyTuple_New(2);
            Runtime.PyTuple_SetItem(tzInfoArgs.Borrow(), 0, Runtime.PyLong_FromLongLong(offset.Hours).Steal());
            Runtime.PyTuple_SetItem(tzInfoArgs.Borrow(), 1, Runtime.PyLong_FromLongLong(offset.Minutes).Steal());
            var returnValue = Runtime.PyObject_CallObject(tzInfoCtor.Value, tzInfoArgs.Borrow());
            return returnValue;
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


        internal static bool ToManaged(BorrowedReference value, Type type,
            out object? result, bool setError)
        {
            var usedImplicit = false;
            return ToManaged(value, type, out result, setError, out usedImplicit);
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
            out object? result, bool setError, out bool usedImplicit)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            return Converter.ToManagedValue(value, type, out result, setError, out usedImplicit);
        }

        internal static bool ToManagedValue(BorrowedReference value, Type obType,
            out object result, bool setError)
        {
            var usedImplicit = false;
            return ToManagedValue(value, obType, out result, setError, out usedImplicit);
        }

        internal static bool ToManagedValue(BorrowedReference value, Type obType,
            out object? result, bool setError, out bool usedImplicit)
        {
            usedImplicit = false;
            if (obType == typeof(PyObject))
            {
                result = new PyObject(value);
                return true;
            }

            if (obType.IsGenericType && Runtime.PyObject_TYPE(value) == Runtime.PyListType)
            {
                var typeDefinition = obType.GetGenericTypeDefinition();
                if (typeDefinition == typeof(List<>)
                    || typeDefinition == typeof(IList<>)
                    || typeDefinition == typeof(IEnumerable<>)
                    || typeDefinition == typeof(IReadOnlyCollection<>))
                {
                    return ToList(value, obType, out result, setError);
                }
            }

            // Common case: if the Python value is a wrapped managed object
            // instance, just return the wrapped object.
            var mt = ManagedType.GetManagedObject(value);
            result = null;

            if (mt != null)
            {
                if (mt is CLRObject co)
                {
                    object tmp = co.inst;
                    var type = tmp.GetType();

                    if (obType.IsInstanceOfType(tmp) || IsSubclassOfRawGeneric(obType, type))
                    {
                        result = tmp;
                        return true;
                    }
                    else
                    {
                        // check implicit conversions that receive tmp type and return obType
                        var conversionMethod = type.GetMethod("op_Implicit", new[] { type });
                        if (conversionMethod != null && conversionMethod.ReturnType == obType)
                        {
                            try
                            {
                                result = conversionMethod.Invoke(null, new[] { tmp });
                                usedImplicit = true;
                                return true;
                            }
                            catch
                            {
                                // Failed to convert using implicit conversion,  must catch the error to stop program from exploding on Linux
                                Exceptions.RaiseTypeError($"Failed to implicitly convert {type} to {obType}");
                                return false;
                            }
                        }
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
                if (value == Runtime.PyNone)
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
                return ToEnum(value, obType, out result, setError, out usedImplicit);
            }

            // Conversion to 'Object' is done based on some reasonable default
            // conversions (Python string -> managed string, Python int -> Int32 etc.).
            if (obType == objectType)
            {
                if (Runtime.IsStringType(value))
                {
                    return ToPrimitive(value, stringType, out result, setError, out usedImplicit);
                }

                if (Runtime.PyBool_Check(value))
                {
                    return ToPrimitive(value, boolType, out result, setError, out usedImplicit);
                }

                if (Runtime.PyInt_Check(value))
                {
                    return ToPrimitive(value, int32Type, out result, setError, out usedImplicit);
                }

                if (Runtime.PyLong_Check(value))
                {
                    return ToPrimitive(value, int64Type, out result, setError, out usedImplicit);
                }

                if (Runtime.PyFloat_Check(value))
                {
                    return ToPrimitive(value, doubleType, out result, setError, out usedImplicit);
                }

                // give custom codecs a chance to take over conversion of sequences
                var pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }

                if (Runtime.PySequence_Check(value))
                {
                    return ToArray(value, typeof(object[]), out result, setError);
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

            var underlyingType = Nullable.GetUnderlyingType(obType);
            if (underlyingType != null)
            {
                return ToManagedValue(value, underlyingType, out result, setError, out usedImplicit);
            }

            TypeCode typeCode = Type.GetTypeCode(obType);
            if (typeCode == TypeCode.Object)
            {
                var pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }
            }

            if (ToPrimitive(value, obType, out result, setError, out usedImplicit))
            {
                return true;
            }

            var opImplicit = obType.GetMethod("op_Implicit", new[] { obType });
            if (opImplicit != null)
            {
                if (ToManagedValue(value, opImplicit.ReturnType, out result, setError, out usedImplicit))
                {
                    opImplicit = obType.GetMethod("op_Implicit", new[] { result.GetType() });
                    if (opImplicit != null)
                    {
                        try
                        {
                            result = opImplicit.Invoke(null, new[] { result });
                        }
                        catch
                        {
                            // Failed to convert using implicit conversion,  must catch the error to stop program from exploding on Linux
                            Exceptions.RaiseTypeError($"Failed to implicitly convert {result.GetType()} to {obType}");
                            return false;
                        }
                    }
                    return opImplicit != null;
                }
            }

            return false;
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
            return ToPrimitive(explicitlyCoerced.Borrow(), obType, out result, false, out var _);
        }

        /// Determine if the comparing class is a subclass of a generic type
        private static bool IsSubclassOfRawGeneric(Type generic, Type comparingClass)
        {

            // Check this is a raw generic type first
            if (!generic.IsGenericType || !generic.ContainsGenericParameters)
            {
                return false;
            }

            // Ensure we have the full generic type definition or it won't match
            generic = generic.GetGenericTypeDefinition();

            // Loop for searching for generic match in inheritance tree of comparing class
            // If we have reach null we don't have a match
            while (comparingClass != null)
            {

                // Check the input for generic type definition, if doesn't exist just use the class
                var comparingClassGeneric = comparingClass.IsGenericType ? comparingClass.GetGenericTypeDefinition() : null;

                // If the same as generic, this is a subclass return true
                if (generic == comparingClassGeneric)
                {
                    return true;
                }

                // Step up the inheritance tree
                comparingClass = comparingClass.BaseType;
            }

            // The comparing class is not based on the generic
            return false;
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
        internal static bool ToPrimitive(BorrowedReference value, Type obType, out object result, bool setError, out bool usedImplicit)
        {
            result = null;
            NewReference op = default;
            usedImplicit = false;

            TypeCode tc = Type.GetTypeCode(obType);

            switch (tc)
            {
                case TypeCode.Object:
                    if (obType == typeof(TimeSpan))
                    {
                        op = Runtime.PyObject_Str(value);
                        TimeSpan ts;
                        var arr = Runtime.GetManagedString(op.Borrow()).Split(',');
                        op.Dispose();
                        string sts = arr.Length == 1 ? arr[0] : arr[1];
                        if (!TimeSpan.TryParse(sts, out ts))
                        {
                            goto type_error;
                        }

                        int days = 0;
                        if (arr.Length > 1)
                        {
                            if (!int.TryParse(arr[0].Split(' ')[0].Trim(), out days))
                            {
                                goto type_error;
                            }
                        }
                        result = ts.Add(TimeSpan.FromDays(days));
                        return true;
                    }
                    else if (obType.IsGenericType && obType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        if (Runtime.PyDict_Check(value))
                        {
                            var typeArguments = obType.GenericTypeArguments;
                            if (typeArguments.Length != 2)
                            {
                                goto type_error;
                            }
                            BorrowedReference key, dicValue, pos;
                            // references returned through key, dicValue are borrowed.
                            if (Runtime.PyDict_Next(value, out pos, out key, out dicValue) != 0)
                            {
                                if (!ToManaged(key, typeArguments[0], out var convertedKey, setError, out usedImplicit))
                                {
                                    goto type_error;
                                }
                                if (!ToManaged(dicValue, typeArguments[1], out var convertedValue, setError, out usedImplicit))
                                {
                                    goto type_error;
                                }

                                result = Activator.CreateInstance(obType, convertedKey, convertedValue);
                                return true;
                            }
                            // and empty dictionary we can't create a key value pair from it
                            goto type_error;
                        }
                    }
                    break;

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
                        op = Runtime.PyNumber_Long(value);
                        if (op.IsNull() && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op.Borrow());
                        op.Dispose();
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
                        op = Runtime.PyNumber_Long(value);
                        if ((op.IsNone() || op.IsNull()) && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op.Borrow());
                        op.Dispose();
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
                            long? num = Runtime.PyLong_AsLongLong(value);
                            if (num == -1 && Exceptions.ErrorOccurred())
                            {
                                goto convert_error;
                            }
                            result = num;
                            return true;
                        }
                        else
                        {
                            op = Runtime.PyNumber_Long(value);
                            if ((op.IsNull() || op.IsNone()) && Exceptions.ErrorOccurred())
                            {
                                goto convert_error;
                            }
                            nint num = Runtime.PyLong_AsSignedSize_t(op.Borrow());
                            op.Dispose();
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
                        op = Runtime.PyNumber_Long(value);
                        if ((op.IsNull() || op.IsNone()) && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op.Borrow());
                        op.Dispose();
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
                        op = Runtime.PyNumber_Long(value);
                        if ((op.IsNull() || op.IsNone()) && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nuint num = Runtime.PyLong_AsUnsignedSize_t(op.Borrow());
                        op.Dispose();
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
                        op = Runtime.PyNumber_Long(value);
                        if ((op.IsNull() || op.IsNone()) && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        ulong? num = Runtime.PyLong_AsUnsignedLongLong(op.Borrow());
                        op.Dispose();
                        if (!num.HasValue || num == ulong.MaxValue && Exceptions.ErrorOccurred())
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
                case TypeCode.Decimal:
                    op = Runtime.PyObject_Str(value);
                    decimal m;
                    var sm = Runtime.GetManagedSpan(op.Borrow(), out var newReference);
                    if (!Decimal.TryParse(sm, NumberStyles.Number | NumberStyles.AllowExponent, nfi, out m))
                    {
                        newReference.Dispose();
                        op.Dispose();
                        goto type_error;
                    }
                    newReference.Dispose();
                    op.Dispose();
                    result = m;
                    return true;
                case TypeCode.DateTime:
                    var year = Runtime.PyObject_GetAttrString(value, yearPtr);
                    if (year.IsNull() || year.IsNone())
                    {
                        year.Dispose();
                        Exceptions.Clear();

                        // fallback to string parsing for types such as numpy
                        op = Runtime.PyObject_Str(value);
                        var sdt = Runtime.GetManagedSpan(op.Borrow(), out var reference);
                        if (!DateTime.TryParse(sdt, out var dt))
                        {
                            reference.Dispose();
                            op.Dispose();
                            Exceptions.Clear();
                            goto type_error;
                        }
                        result = sdt.EndsWith("+00:00") ? dt.ToUniversalTime() : dt;
                        reference.Dispose();
                        op.Dispose();

                        Exceptions.Clear();
                        return true;
                    }
                    var month = Runtime.PyObject_GetAttrString(value, monthPtr);
                    var day = Runtime.PyObject_GetAttrString(value, dayPtr);
                    var hour = Runtime.PyObject_GetAttrString(value, hourPtr);
                    var minute = Runtime.PyObject_GetAttrString(value, minutePtr);
                    var second = Runtime.PyObject_GetAttrString(value, secondPtr);
                    var microsecond = Runtime.PyObject_GetAttrString(value, microsecondPtr);
                    var timeKind = DateTimeKind.Unspecified;
                    var tzinfo = Runtime.PyObject_GetAttrString(value, tzinfoPtr);

                    NewReference hours = default;
                    NewReference minutes = default;
                    if (!ReferenceNullOrNone(tzinfo))
                    {
                        // We set the datetime kind to UTC if the tzinfo was set to UTC by the ToPthon method
                        // using it's custom GMT Python tzinfo class
                        hours = Runtime.PyObject_GetAttrString(tzinfo.Borrow(), hoursPtr);
                        minutes = Runtime.PyObject_GetAttrString(tzinfo.Borrow(), minutesPtr);
                        if (!ReferenceNullOrNone(hours) &&
                            !ReferenceNullOrNone(minutes) &&
                            Runtime.PyLong_AsLong(hours.Borrow()) == 0 && Runtime.PyLong_AsLong(minutes.Borrow()) == 0)
                        {
                            timeKind = DateTimeKind.Utc;
                        }
                    }

                    var convertedHour = 0L;
                    var convertedMinute = 0L;
                    var convertedSecond = 0L;
                    var milliseconds = 0L;
                    // could be python date type
                    if (!ReferenceNullOrNone(hour))
                    {
                        convertedHour = Runtime.PyLong_AsLong(hour.Borrow());
                        convertedMinute = Runtime.PyLong_AsLong(minute.Borrow());
                        convertedSecond = Runtime.PyLong_AsLong(second.Borrow());
                        milliseconds = Runtime.PyLong_AsLong(microsecond.Borrow()) / 1000;
                    }

                    result = new DateTime((int)Runtime.PyLong_AsLong(year.Borrow()),
                        (int)Runtime.PyLong_AsLong(month.Borrow()),
                        (int)Runtime.PyLong_AsLong(day.Borrow()),
                        (int)convertedHour,
                        (int)convertedMinute,
                        (int)convertedSecond,
                        (int)milliseconds,
                        timeKind);

                    year.Dispose();
                    month.Dispose();
                    day.Dispose();
                    hour.Dispose();
                    minute.Dispose();
                    second.Dispose();
                    microsecond.Dispose();

                    if (!tzinfo.IsNull())
                    {
                        tzinfo.Dispose();
                        if (!tzinfo.IsNone())
                        {
                            hours.Dispose();
                            minutes.Dispose();
                        }
                    }

                    Exceptions.Clear();
                    return true;
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

        private static bool ReferenceNullOrNone(NewReference reference)
        {
            return reference.IsNull() || reference.IsNone();
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
        private static bool ToArray(BorrowedReference value, Type obType, out object result, bool setError)
        {
            Type elementType = obType.GetElementType();
            result = null;

            using var IterObject = Runtime.PyObject_GetIter(value);
            if (IterObject.IsNull() || elementType.IsGenericType)
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

            var list = MakeList(value, IterObject, obType, elementType, setError);
            if (list == null)
            {
                return false;
            }

            Array items = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(items, 0);

            result = items;
            return true;
        }

        /// <summary>
        /// Convert a Python value to a correctly typed managed list instance.
        /// The Python value must support the Python sequence protocol and the
        /// items in the sequence must be convertible to the target list type.
        /// </summary>
        private static bool ToList(BorrowedReference value, Type obType, out object result, bool setError)
        {
            var elementType = obType.GetGenericArguments()[0];
            var IterObject = Runtime.PyObject_GetIter(value);
            result = MakeList(value, IterObject, obType, elementType, setError);
            return result != null;
        }

        /// <summary>
        /// Helper function for ToArray and ToList that creates a IList out of iterable objects
        /// </summary>
        /// <param name="value"></param>
        /// <param name="IterObject"></param>
        /// <param name="obType"></param>
        /// <param name="elementType"></param>
        /// <param name="setError"></param>
        /// <returns></returns>
        private static IList MakeList(BorrowedReference value, NewReference IterObject, Type obType, Type elementType, bool setError)
        {
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

                return null;
            }

            while (true)
            {
                using var item = Runtime.PyIter_Next(IterObject.Borrow());
                if (item.IsNull()) break;

                if (!Converter.ToManaged(item.Borrow(), elementType, out var obj, setError))
                {
                    return null;
                }

                list.Add(obj);
            }

            return list;
        }

        internal static bool IsFloatingNumber(Type type) => type == typeof(float) || type == typeof(double);
        internal static bool IsInteger(Type type)
            => type == typeof(Byte) || type == typeof(SByte)
            || type == typeof(Int16) || type == typeof(UInt16)
            || type == typeof(Int32) || type == typeof(UInt32)
            || type == typeof(Int64) || type == typeof(UInt64);

        /// <summary>
        /// Convert a Python value to a correctly typed managed enum instance.
        /// </summary>
        private static bool ToEnum(BorrowedReference value, Type obType, out object result, bool setError, out bool usedImplicit)
        {
            Type etype = Enum.GetUnderlyingType(obType);
            result = null;

            if (!ToPrimitive(value, etype, out result, setError, out usedImplicit))
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
        public static PyObject ToPython(this object? o)
        {
            if (o is null) return Runtime.None;
            return Converter.ToPython(o, o.GetType()).MoveToPyObject();
        }
    }
}
