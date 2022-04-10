using System;

namespace Python.Runtime;

public partial class PyObject : IConvertible
{
    public virtual TypeCode GetTypeCode() => TypeCode.Object;

    private T DoConvert<T>()
    {
        using var _ = Py.GIL();
        if (Converter.ToPrimitive(Reference, typeof(T), out object? result, setError: false))
        {
            return (T)result!;
        }
        else
        {
            throw new InvalidCastException();
        }
    }

    public bool ToBoolean(IFormatProvider provider) => DoConvert<bool>();
    public byte ToByte(IFormatProvider provider) => DoConvert<byte>();
    public char ToChar(IFormatProvider provider) => DoConvert<char>();
    public short ToInt16(IFormatProvider provider) => DoConvert<short>();
    public int ToInt32(IFormatProvider provider) => DoConvert<int>();
    public long ToInt64(IFormatProvider provider) => DoConvert<long>();
    public sbyte ToSByte(IFormatProvider provider) => DoConvert<sbyte>();
    public ushort ToUInt16(IFormatProvider provider) => DoConvert<ushort>();
    public uint ToUInt32(IFormatProvider provider) => DoConvert<uint>();
    public ulong ToUInt64(IFormatProvider provider) => DoConvert<ulong>();

    public float ToSingle(IFormatProvider provider) => DoConvert<float>();
    public double ToDouble(IFormatProvider provider) => DoConvert<double>();

    public string ToString(IFormatProvider provider) => DoConvert<string>();

    public DateTime ToDateTime(IFormatProvider provider) => throw new InvalidCastException();
    public decimal ToDecimal(IFormatProvider provider) => throw new InvalidCastException();

    public object ToType(Type conversionType, IFormatProvider provider)
    {
        if (Converter.ToManaged(Reference, conversionType, out object? result, setError: false))
        {
            return result!;
        }
        else
        {
            throw new InvalidCastException();
        }
    }

}