namespace Python.Runtime.Native;

enum PyMemberType: int
{
    Short = 0,
    Int = 1,
    Long = 2,
    Float = 3,
    Double = 4,
    String = 5,
    Object = 6,
    /// <summary>1-character string</summary>
    Char = 7,
    /// <summary>8-bit signed int</summary>
    Byte = 8,

    UByte = 9,
    UShort = 10,
    UInt = 11,
    ULong = 12,

    StringInPlace = 13,

    /// <summary>bools contained in the structure (assumed char)</summary>
    Bool = 14,

    /// <summary>
    /// Like <see cref="Object"/>but raises AttributeError
    /// when the value is NULL, instead of converting to None
    /// </summary>
    ObjectEx = 16,

    LongLong = 17,
    ULongLong = 18,

    PySignedSizeT = 19,
    AlwaysNone = 20,
}
