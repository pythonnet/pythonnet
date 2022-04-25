using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Linq;

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

        private static Type objectType;
        private static Type stringType;
        private static Type singleType;
        private static Type doubleType;
        private static Type int16Type;
        private static Type int32Type;
        private static Type int64Type;
        private static Type boolType;
        private static Type typeType;
        private static IntPtr dateTimeCtor;
        private static IntPtr timeSpanCtor;
        private static IntPtr tzInfoCtor;
        private static IntPtr pyTupleNoKind;
        private static IntPtr pyTupleKind;

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
            objectType = typeof(Object);
            stringType = typeof(String);
            int16Type = typeof(Int16);
            int32Type = typeof(Int32);
            int64Type = typeof(Int64);
            singleType = typeof(Single);
            doubleType = typeof(Double);
            boolType = typeof(Boolean);
            typeType = typeof(Type);

            IntPtr dateTimeMod = Runtime.PyImport_ImportModule("datetime");
            if (dateTimeMod == null) throw new PythonException();

            dateTimeCtor = Runtime.PyObject_GetAttrString(dateTimeMod, "datetime");
            if (dateTimeCtor == null) throw new PythonException();

            timeSpanCtor = Runtime.PyObject_GetAttrString(dateTimeMod, "timedelta");
            if (timeSpanCtor == null) throw new PythonException();

            IntPtr tzInfoMod = PythonEngine.ModuleFromString("custom_tzinfo", @"
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
        return timedelta(0)").Handle;

            tzInfoCtor = Runtime.PyObject_GetAttrString(tzInfoMod, "GMT");
            if (tzInfoCtor == null) throw new PythonException();

            pyTupleNoKind = Runtime.PyTuple_New(7);
            pyTupleKind = Runtime.PyTuple_New(8);

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

            if (op == Runtime.PyDecimalType)
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
                return Runtime.PyDecimalType;
                
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

            var valueType = value.GetType();
            if (Type.GetTypeCode(type) == TypeCode.Object && valueType != typeof(object)) {
                var encoded = PyObjectConversions.TryEncode(value, type);
                if (encoded != null) {
                    result = encoded.Handle;
                    Runtime.XIncref(result);
                    return result;
                }
            }

            if (valueType.IsGenericType && value is IList && !(value is INotifyPropertyChanged))
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

            // it the type is a python subclass of a managed type then return the
            // underlying python object rather than construct a new wrapper object.
            var pyderived = value as IPythonDerivedType;
            if (null != pyderived)
            {
                if (!IsTransparentProxy(pyderived))
                    return ClassDerivedObject.ToPython(pyderived);
            }

            // ModuleObjects are created in a way that their wrapping them as
            // a CLRObject fails, the ClassObject has no tpHandle. Return the
            // pyHandle as is, do not convert.
            if (value is ModuleObject modobj)
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
                    if (value is TimeSpan)
                    {
                        var timespan = (TimeSpan)value;

                        IntPtr timeSpanArgs = Runtime.PyTuple_New(1);
                        Runtime.PyTuple_SetItem(timeSpanArgs, 0, Runtime.PyFloat_FromDouble(timespan.TotalDays));
                        var returnTimeSpan = Runtime.PyObject_CallObject(timeSpanCtor, timeSpanArgs);
                        // clean up
                        Runtime.XDecref(timeSpanArgs);
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

                case TypeCode.Decimal:
                    // C# decimal to python decimal has a big impact on performance
                    // so we will use C# double and python float
                    return Runtime.PyFloat_FromDouble(decimal.ToDouble((decimal)value));

                case TypeCode.DateTime:
                    var datetime = (DateTime)value;

                    var size = datetime.Kind == DateTimeKind.Unspecified ? 7 : 8;

                    var dateTimeArgs = datetime.Kind == DateTimeKind.Unspecified ? pyTupleNoKind : pyTupleKind;
                    Runtime.PyTuple_SetItem(dateTimeArgs, 0, Runtime.PyInt_FromInt32(datetime.Year));
                    Runtime.PyTuple_SetItem(dateTimeArgs, 1, Runtime.PyInt_FromInt32(datetime.Month));
                    Runtime.PyTuple_SetItem(dateTimeArgs, 2, Runtime.PyInt_FromInt32(datetime.Day));
                    Runtime.PyTuple_SetItem(dateTimeArgs, 3, Runtime.PyInt_FromInt32(datetime.Hour));
                    Runtime.PyTuple_SetItem(dateTimeArgs, 4, Runtime.PyInt_FromInt32(datetime.Minute));
                    Runtime.PyTuple_SetItem(dateTimeArgs, 5, Runtime.PyInt_FromInt32(datetime.Second));

                    // datetime.datetime 6th argument represents micro seconds
                    var totalSeconds = datetime.TimeOfDay.TotalSeconds;
                    var microSeconds = Convert.ToInt32((totalSeconds - Math.Truncate(totalSeconds)) * 1000000);
                    if (microSeconds == 1000000) microSeconds = 999999;
                    Runtime.PyTuple_SetItem(dateTimeArgs, 6, Runtime.PyInt_FromInt32(microSeconds));

                    if (size == 8)
                    {
                        Runtime.PyTuple_SetItem(dateTimeArgs, 7, TzInfo(datetime.Kind));
                    }

                    var returnDateTime = Runtime.PyObject_CallObject(dateTimeCtor, dateTimeArgs);
                    return returnDateTime;


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

        private static IntPtr TzInfo(DateTimeKind kind)
        {
            if (kind == DateTimeKind.Unspecified) return Runtime.PyNone;
            var offset = kind == DateTimeKind.Local ? DateTimeOffset.Now.Offset : TimeSpan.Zero;
            IntPtr tzInfoArgs = Runtime.PyTuple_New(2);
            Runtime.PyTuple_SetItem(tzInfoArgs, 0, Runtime.PyFloat_FromDouble(offset.Hours));
            Runtime.PyTuple_SetItem(tzInfoArgs, 1, Runtime.PyFloat_FromDouble(offset.Minutes));
            var returnValue = Runtime.PyObject_CallObject(tzInfoCtor, tzInfoArgs);
            Runtime.XDecref(tzInfoArgs);
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


        internal static bool ToManaged(IntPtr value, Type type,
            out object result, bool setError)
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
            out object? result, bool setError)
        {
            var usedImplicit = false;
            return ToManagedValue(value.DangerousGetAddress(), obType, out result, setError, out usedImplicit);
        }

        internal static bool ToManagedValue(IntPtr value, Type obType,
            out object result, bool setError, out bool usedImplicit)
        {
            usedImplicit = false;
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
            result = null;
            switch (ManagedType.GetManagedObject(value))
            {
                case CLRObject co:
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
                            try{
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

            if (obType.IsEnum)
            {
                return ToEnum(value, obType, out result, setError, out usedImplicit);
            }

            // Conversion to 'Object' is done based on some reasonable default
            // conversions (Python string -> managed string).
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

                // give custom codecs a chance to take over conversion of ints and sequences
                BorrowedReference pyType = Runtime.PyObject_TYPE(value);
                if (PyObjectConversions.TryDecode(value, pyType, obType, out result))
                {
                    return true;
                }

                if (Runtime.PyInt_Check(value))
                {
                    result = new PyInt(value);
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

            var underlyingType = Nullable.GetUnderlyingType(obType);
            if (underlyingType != null)
            {
                return ToManagedValue(value, underlyingType, out result, setError, out usedImplicit);
            }

            TypeCode typeCode = Type.GetTypeCode(obType);
            if (typeCode == TypeCode.Object)
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

        /// Determine if the comparing class is a subclass of a generic type
        private static bool IsSubclassOfRawGeneric(Type generic, Type comparingClass) {

            // Check this is a raw generic type first
            if(!generic.IsGenericType || !generic.ContainsGenericParameters){
                return false;
            }

            // Ensure we have the full generic type definition or it won't match
            generic = generic.GetGenericTypeDefinition();

            // Loop for searching for generic match in inheritance tree of comparing class
            // If we have reach null we don't have a match
            while (comparingClass != null) {

                // Check the input for generic type definition, if doesn't exist just use the class
                var comparingClassGeneric = comparingClass.IsGenericType ? comparingClass.GetGenericTypeDefinition() : null;

                // If the same as generic, this is a subclass return true
                if (generic == comparingClassGeneric) {
                    return true;
                }

                // Step up the inheritance tree
                comparingClass = comparingClass.BaseType;
            }

            // The comparing class is not based on the generic
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
        internal static bool ToPrimitive(BorrowedReference value, Type obType, out object? result, bool setError, out bool usedImplicit)
        {
            result = null;
            IntPtr op = IntPtr.Zero;
            usedImplicit = false;

            switch (tc)
            {
                case TypeCode.Object:
                    if (obType == typeof(TimeSpan))
                    {
                        op = Runtime.PyObject_Str(value);
                        TimeSpan ts;
                        var arr = Runtime.GetManagedString(op).Split(',');
                        string sts = arr.Length == 1 ? arr[0] : arr[1];
                        if (!TimeSpan.TryParse(sts, out ts))
                        {
                            goto type_error;
                        }
                        Runtime.XDecref(op);

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
                            IntPtr key, dicValue, pos;
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
                        op = Runtime.PyNumber_Long(value);
                        if (op == IntPtr.Zero && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op);
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
                        op = Runtime.PyNumber_Long(value);
                        if (op == IntPtr.Zero && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op);
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
                            if (num is null)
                            {
                                goto convert_error;
                            }
                            result = num.Value;
                            return true;
                        }
                        else
                        {
                            op = Runtime.PyNumber_Long(value);
                            if (op == IntPtr.Zero && Exceptions.ErrorOccurred())
                            {
                                goto convert_error;
                            }
                            nint num = Runtime.PyLong_AsSignedSize_t(op);
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
                        if (op == IntPtr.Zero && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nint num = Runtime.PyLong_AsSignedSize_t(op);
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
                        if (op == IntPtr.Zero && Exceptions.ErrorOccurred())
                        {
                            goto convert_error;
                        }
                        nuint num = Runtime.PyLong_AsUnsignedSize_t(op);
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
                case TypeCode.Decimal:
                    op = Runtime.PyObject_Str(value);
                    decimal m;
                    var sm = Runtime.GetManagedSpan(op, out var newReference);
                    if (!Decimal.TryParse(sm, NumberStyles.Number | NumberStyles.AllowExponent, nfi, out m))
                    {
                        newReference.Dispose();
                        Runtime.XDecref(op);
                        goto type_error;
                    }
                    newReference.Dispose();
                    Runtime.XDecref(op);
                    result = m;
                    return true;
                case TypeCode.DateTime:
                    var year = Runtime.PyObject_GetAttrString(value, yearPtr);
                    if (year == IntPtr.Zero || year == Runtime.PyNone)
                    {
                        Runtime.XDecref(year);

                        // fallback to string parsing for types such as numpy
                        op = Runtime.PyObject_Str(value);
                        var sdt = Runtime.GetManagedSpan(op, out var reference);
                        if (!DateTime.TryParse(sdt, out var dt))
                        {
                            reference.Dispose();
                            Runtime.XDecref(op);
                            Exceptions.Clear();
                            goto type_error;
                        }
                        result = sdt.EndsWith("+00:00") ? dt.ToUniversalTime() : dt;
                        reference.Dispose();
                        Runtime.XDecref(op);

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

                    var hours = IntPtr.MaxValue;
                    var minutes = IntPtr.MaxValue;
                    if (tzinfo != IntPtr.Zero && tzinfo != Runtime.PyNone)
                    {
                        hours = Runtime.PyObject_GetAttrString(tzinfo, hoursPtr);
                        minutes = Runtime.PyObject_GetAttrString(tzinfo, minutesPtr);
                        if (Runtime.PyInt_AsLong(hours) == 0 && Runtime.PyInt_AsLong(minutes) == 0)
                        {
                            timeKind = DateTimeKind.Utc;
                        }
                    }

                    var convertedHour = 0;
                    var convertedMinute = 0;
                    var convertedSecond = 0;
                    var milliseconds = 0;
                    // could be python date type
                    if (hour != IntPtr.Zero && hour != Runtime.PyNone)
                    {
                        convertedHour = Runtime.PyInt_AsLong(hour);
                        convertedMinute = Runtime.PyInt_AsLong(minute);
                        convertedSecond = Runtime.PyInt_AsLong(second);
                        milliseconds = Runtime.PyInt_AsLong(microsecond) / 1000;
                    }

                    result = new DateTime(Runtime.PyInt_AsLong(year),
                        Runtime.PyInt_AsLong(month),
                        Runtime.PyInt_AsLong(day),
                        convertedHour,
                        convertedMinute,
                        convertedSecond,
                        millisecond: milliseconds,
                        timeKind);

                    Runtime.XDecref(year);
                    Runtime.XDecref(month);
                    Runtime.XDecref(day);
                    Runtime.XDecref(hour);
                    Runtime.XDecref(minute);
                    Runtime.XDecref(second);
                    Runtime.XDecref(microsecond);

                    if (tzinfo != IntPtr.Zero)
                    {
                        Runtime.XDecref(tzinfo);
                        if(tzinfo != Runtime.PyNone)
                        {
                            Runtime.XDecref(hours);
                            Runtime.XDecref(minutes);
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
        private static bool ToList(IntPtr value, Type obType, out object result, bool setError)
        {
            var elementType = obType.GetGenericArguments()[0];
            IntPtr IterObject = Runtime.PyObject_GetIter(value);
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
        private static IList MakeList(IntPtr value, IntPtr IterObject, Type obType, Type elementType, bool setError)
        {
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

                return null;
            }

            IntPtr item;
            var usedImplicit = false;
            while ((item = Runtime.PyIter_Next(IterObject)) != IntPtr.Zero)
            {
                using var item = Runtime.PyIter_Next(IterObject.Borrow());
                if (item.IsNull()) break;

                if (!Converter.ToManaged(item.Borrow(), elementType, out var obj, setError))
                {
                    return null;
                }

                list.Add(obj);
            }

            if (Exceptions.ErrorOccurred())
            {
                if (!setError) Exceptions.Clear();
                return null;
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
        private static bool ToEnum(IntPtr value, Type obType, out object result, bool setError, out bool usedImplicit)
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
