using System;

namespace Python.Runtime;

partial class PyInt : IComparable<long>, IComparable<int>, IComparable<sbyte>, IComparable<short>
    , IComparable<ulong>, IComparable<uint>, IComparable<ushort>, IComparable<byte>
    , IEquatable<long>, IEquatable<int>, IEquatable<short>, IEquatable<sbyte>
    , IEquatable<ulong>, IEquatable<uint>, IEquatable<ushort>, IEquatable<byte>
    , IComparable<PyInt?>, IEquatable<PyInt?>
{
    public override bool Equals(object o)
    {
        using var _ = Py.GIL();
        return o switch
        {
            long i64 => this.Equals(i64),
            int i32 => this.Equals(i32),
            short i16 => this.Equals(i16),
            sbyte i8 => this.Equals(i8),

            ulong u64 => this.Equals(u64),
            uint u32 => this.Equals(u32),
            ushort u16 => this.Equals(u16),
            byte u8 => this.Equals(u8),

            _ => base.Equals(o),
        };
    }

    #region Signed
    public int CompareTo(long other)
    {
        using var pyOther = Runtime.PyInt_FromInt64(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(int other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(short other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(sbyte other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public bool Equals(long other)
    {
        using var pyOther = Runtime.PyInt_FromInt64(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(int other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(short other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(sbyte other)
    {
        using var pyOther = Runtime.PyInt_FromInt32(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }
    #endregion Signed

    #region Unsigned
    public int CompareTo(ulong other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(uint other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(ushort other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public int CompareTo(byte other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.CompareTo(pyOther.BorrowOrThrow());
    }

    public bool Equals(ulong other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(uint other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(ushort other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }

    public bool Equals(byte other)
    {
        using var pyOther = Runtime.PyLong_FromUnsignedLongLong(other);
        return this.Equals(pyOther.BorrowOrThrow());
    }
    #endregion Unsigned

    public int CompareTo(PyInt? other)
    {
        return other is null ? 1 : this.CompareTo(other.BorrowNullable());
    }

    public bool Equals(PyInt? other) => base.Equals(other);
}
