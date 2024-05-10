"""Test CLR <-> Python type conversions."""

import operator
import pytest

import System
from Python.Test import ConversionTest, MethodResolutionInt, UnicodeString, CodecResetter
from Python.Runtime import PyObjectConversions
from Python.Runtime.Codecs import RawProxyEncoder


def test_bool_conversion():
    """Test bool conversion."""
    ob = ConversionTest()
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = True
    assert ob.BooleanField is True
    assert ob.BooleanField == 1

    ob.BooleanField = False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    with pytest.raises(TypeError):
        ob.BooleanField = 1

    with pytest.raises(TypeError):
        ob.BooleanField = 0

    with pytest.raises(TypeError):
        ob.BooleanField = None

    with pytest.raises(TypeError):
        ob.BooleanField = ''

    with pytest.raises(TypeError):
        ob.BooleanField = System.Boolean(0)

    with pytest.raises(TypeError):
        ob.BooleanField = System.Boolean(1)

    with pytest.raises(TypeError):
        ob.BooleanField = System.Boolean('a')


def test_sbyte_conversion():
    """Test sbyte conversion."""
    assert System.SByte.MaxValue == 127
    assert System.SByte.MinValue == -128

    ob = ConversionTest()
    assert ob.SByteField == 0

    ob.SByteField = 127
    assert ob.SByteField == 127

    ob.SByteField = -128
    assert ob.SByteField == -128

    ob.SByteField = System.SByte(127)
    assert ob.SByteField == 127

    ob.SByteField = System.SByte(-128)
    assert ob.SByteField == -128

    with pytest.raises(TypeError):
        ConversionTest().SByteField = "spam"

    with pytest.raises(TypeError):
        ConversionTest().SByteField = None

    with pytest.raises(OverflowError):
        ConversionTest().SByteField = 128

    with pytest.raises(OverflowError):
        ConversionTest().SByteField = -129

    with pytest.raises(OverflowError):
        _ = System.SByte(128)

    with pytest.raises(OverflowError):
        _ = System.SByte(-129)


def test_byte_conversion():
    """Test byte conversion."""
    assert System.Byte.MaxValue == 255
    assert System.Byte.MinValue == 0

    ob = ConversionTest()
    assert ob.ByteField == 0

    ob.ByteField = 255
    assert ob.ByteField == 255

    ob.ByteField = 0
    assert ob.ByteField == 0

    ob.ByteField = System.Byte(255)
    assert ob.ByteField == 255

    ob.ByteField = System.Byte(0)
    assert ob.ByteField == 0

    with pytest.raises(TypeError):
        ConversionTest().ByteField = "spam"

    with pytest.raises(TypeError):
        ConversionTest().ByteField = None

    with pytest.raises(OverflowError):
        ConversionTest().ByteField = 256

    with pytest.raises(OverflowError):
        ConversionTest().ByteField = -1

    with pytest.raises(OverflowError):
        _ = System.Byte(256)

    with pytest.raises(OverflowError):
        _ = System.Byte(-1)


def test_char_conversion():
    """Test char conversion."""
    assert System.Char.MaxValue == chr(65535)
    assert System.Char.MinValue == chr(0)

    ob = ConversionTest()
    assert ob.CharField == u'A'

    ob.CharField = 'B'
    assert ob.CharField == u'B'

    ob.CharField = u'B'
    assert ob.CharField == u'B'

    ob.CharField = 67
    assert ob.CharField == u'C'

    with pytest.raises(OverflowError):
        ConversionTest().CharField = 65536

    with pytest.raises(OverflowError):
        ConversionTest().CharField = -1

    with pytest.raises(TypeError):
        ConversionTest().CharField = None


def test_int16_conversion():
    """Test int16 conversion."""
    assert System.Int16.MaxValue == 32767
    assert System.Int16.MinValue == -32768

    ob = ConversionTest()
    assert ob.Int16Field == 0

    ob.Int16Field = 32767
    assert ob.Int16Field == 32767

    ob.Int16Field = -32768
    assert ob.Int16Field == -32768

    ob.Int16Field = System.Int16(32767)
    assert ob.Int16Field == 32767

    ob.Int16Field = System.Int16(-32768)
    assert ob.Int16Field == -32768

    with pytest.raises(TypeError):
        ConversionTest().Int16Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().Int16Field = None

    with pytest.raises(OverflowError):
        ConversionTest().Int16Field = 32768

    with pytest.raises(OverflowError):
        ConversionTest().Int16Field = -32769

    with pytest.raises(OverflowError):
        _ = System.Int16(32768)

    with pytest.raises(OverflowError):
        _ = System.Int16(-32769)


def test_int32_conversion():
    """Test int32 conversion."""
    assert System.Int32.MaxValue == 2147483647
    assert System.Int32.MinValue == -2147483648

    ob = ConversionTest()
    assert ob.Int32Field == 0

    ob.Int32Field = 2147483647
    assert ob.Int32Field == 2147483647

    ob.Int32Field = -2147483648
    assert ob.Int32Field == -2147483648

    ob.Int32Field = System.Int32(2147483647)
    assert ob.Int32Field == 2147483647

    ob.Int32Field = System.Int32(-2147483648)
    assert ob.Int32Field == -2147483648

    with pytest.raises(TypeError):
        ConversionTest().Int32Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().Int32Field = None

    with pytest.raises(OverflowError):
        ConversionTest().Int32Field = 2147483648

    with pytest.raises(OverflowError):
        ConversionTest().Int32Field = -2147483649

    with pytest.raises(OverflowError):
        _ = System.Int32(2147483648)

    with pytest.raises(OverflowError):
        _ = System.Int32(-2147483649)


def test_int64_conversion():
    """Test int64 conversion."""
    assert System.Int64.MaxValue == 9223372036854775807
    assert System.Int64.MinValue == -9223372036854775808

    ob = ConversionTest()
    assert ob.Int64Field == 0

    ob.Int64Field = 9223372036854775807
    assert ob.Int64Field == 9223372036854775807

    ob.Int64Field = -9223372036854775808
    assert ob.Int64Field == -9223372036854775808

    ob.Int64Field = System.Int64(9223372036854775807)
    assert ob.Int64Field == 9223372036854775807

    ob.Int64Field = System.Int64(-9223372036854775808)
    assert ob.Int64Field == -9223372036854775808

    with pytest.raises(TypeError):
        ConversionTest().Int64Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().Int64Field = None

    with pytest.raises(OverflowError):
        ConversionTest().Int64Field = 9223372036854775808

    with pytest.raises(OverflowError):
        ConversionTest().Int64Field = -9223372036854775809

    with pytest.raises(OverflowError):
        _ = System.Int64(9223372036854775808)

    with pytest.raises(OverflowError):
        _ = System.Int64(-9223372036854775809)


def test_uint16_conversion():
    """Test uint16 conversion."""
    assert System.UInt16.MaxValue == 65535
    assert System.UInt16.MinValue == 0

    ob = ConversionTest()
    assert ob.UInt16Field == 0

    ob.UInt16Field = 65535
    assert ob.UInt16Field == 65535

    ob.UInt16Field = -0
    assert ob.UInt16Field == 0

    ob.UInt16Field = System.UInt16(65535)
    assert ob.UInt16Field == 65535

    ob.UInt16Field = System.UInt16(0)
    assert ob.UInt16Field == 0

    with pytest.raises(TypeError):
        ConversionTest().UInt16Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().UInt16Field = None

    with pytest.raises(OverflowError):
        ConversionTest().UInt16Field = 65536

    with pytest.raises(OverflowError):
        ConversionTest().UInt16Field = -1

    with pytest.raises(OverflowError):
        _ = System.UInt16(65536)

    with pytest.raises(OverflowError):
        _ = System.UInt16(-1)


def test_uint32_conversion():
    """Test uint32 conversion."""
    assert System.UInt32.MaxValue == 4294967295
    assert System.UInt32.MinValue == 0

    ob = ConversionTest()
    assert ob.UInt32Field == 0

    ob.UInt32Field = 4294967295
    assert ob.UInt32Field == 4294967295

    ob.UInt32Field = -0
    assert ob.UInt32Field == 0

    ob.UInt32Field = System.UInt32(4294967295)
    assert ob.UInt32Field == 4294967295

    ob.UInt32Field = System.UInt32(0)
    assert ob.UInt32Field == 0

    with pytest.raises(TypeError):
        ConversionTest().UInt32Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().UInt32Field = None

    with pytest.raises(OverflowError):
        ConversionTest().UInt32Field = 4294967296

    with pytest.raises(OverflowError):
        ConversionTest().UInt32Field = -1

    with pytest.raises(OverflowError):
        _ = System.UInt32(4294967296)

    with pytest.raises(OverflowError):
        _ = System.UInt32(-1)


def test_uint64_conversion():
    """Test uint64 conversion."""
    assert System.UInt64.MaxValue == 18446744073709551615
    assert System.UInt64.MinValue == 0

    ob = ConversionTest()
    assert ob.UInt64Field == 0

    ob.UInt64Field = 18446744073709551615
    assert ob.UInt64Field == 18446744073709551615

    ob.UInt64Field = -0
    assert ob.UInt64Field == 0

    ob.UInt64Field = System.UInt64(18446744073709551615)
    assert ob.UInt64Field == 18446744073709551615

    ob.UInt64Field = System.UInt64(0)
    assert ob.UInt64Field == 0

    with pytest.raises(TypeError):
        ConversionTest().UInt64Field = 0.5

    with pytest.raises(TypeError):
        ConversionTest().UInt64Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().UInt64Field = None

    with pytest.raises(OverflowError):
        ConversionTest().UInt64Field = 18446744073709551616

    with pytest.raises(OverflowError):
        ConversionTest().UInt64Field = -1

    with pytest.raises(OverflowError):
        _ = System.UInt64((18446744073709551616))

    with pytest.raises(OverflowError):
        _ = System.UInt64(-1)


def test_single_conversion():
    """Test single conversion."""
    assert System.Single.MaxValue == pytest.approx(3.402823e38)
    assert System.Single.MinValue == pytest.approx(-3.402823e38)

    ob = ConversionTest()
    assert ob.SingleField == 0.0

    ob.SingleField = 3.402823e38
    assert ob.SingleField == System.Single(3.402823e38)

    ob.SingleField = -3.402823e38
    assert ob.SingleField == System.Single(-3.402823e38)

    with pytest.raises(TypeError):
        ConversionTest().SingleField = "spam"

    with pytest.raises(TypeError):
        ConversionTest().SingleField = None

    with pytest.raises(OverflowError):
        ConversionTest().SingleField = 3.402824e38

    with pytest.raises(OverflowError):
        ConversionTest().SingleField = -3.402824e38

    with pytest.raises(OverflowError):
        _ = System.Single(3.402824e38)

    with pytest.raises(OverflowError):
        _ = System.Single(-3.402824e38)


def test_double_conversion():
    """Test double conversion."""
    assert System.Double.MaxValue == 1.7976931348623157e308
    assert System.Double.MinValue == -1.7976931348623157e308

    ob = ConversionTest()
    assert ob.DoubleField == 0.0

    ob.DoubleField = 1.7976931348623157e308
    assert ob.DoubleField == 1.7976931348623157e308

    ob.DoubleField = -1.7976931348623157e308
    assert ob.DoubleField == -1.7976931348623157e308

    ob.DoubleField = System.Double(1.7976931348623157e308)
    assert ob.DoubleField == 1.7976931348623157e308

    ob.DoubleField = System.Double(-1.7976931348623157e308)
    assert ob.DoubleField == -1.7976931348623157e308

    with pytest.raises(TypeError):
        ConversionTest().DoubleField = "spam"

    with pytest.raises(TypeError):
        ConversionTest().DoubleField = None



def test_decimal_conversion():
    """Test decimal conversion."""
    from System import Decimal

    max_d = Decimal.Parse("79228162514264337593543950335")
    min_d = Decimal.Parse("-79228162514264337593543950335")

    assert Decimal.ToInt64(Decimal(10)) == 10

    ob = ConversionTest()
    assert ob.DecimalField == Decimal(0)

    ob.DecimalField = Decimal(10)
    assert ob.DecimalField == Decimal(10)

    ob.DecimalField = Decimal.One
    assert ob.DecimalField == Decimal.One

    ob.DecimalField = Decimal.Zero
    assert ob.DecimalField == Decimal.Zero

    ob.DecimalField = max_d
    assert ob.DecimalField == max_d

    ob.DecimalField = min_d
    assert ob.DecimalField == min_d

    with pytest.raises(TypeError):
        ConversionTest().DecimalField = None

    with pytest.raises(TypeError):
        ConversionTest().DecimalField = "spam"

    with pytest.raises(TypeError):
        ConversionTest().DecimalField = 1


def test_string_conversion():
    """Test string / unicode conversion."""
    ob = ConversionTest()

    assert ob.StringField == "spam"
    assert ob.StringField == u"spam"

    ob.StringField = "eggs"
    assert ob.StringField == "eggs"
    assert ob.StringField == u"eggs"

    ob.StringField = u"spam"
    assert ob.StringField == "spam"
    assert ob.StringField == u"spam"

    ob.StringField = u'\uffff\uffff'
    assert ob.StringField == u'\uffff\uffff'

    ob.StringField = System.String("spam")
    assert ob.StringField == "spam"
    assert ob.StringField == u"spam"

    ob.StringField = System.String(u'\uffff\uffff')
    assert ob.StringField == u'\uffff\uffff'

    ob.StringField = System.String("\ufeffbom")
    assert ob.StringField == "\ufeffbom"

    ob.StringField = None
    assert ob.StringField is None

    with pytest.raises(TypeError):
        ConversionTest().StringField = 1

    world = UnicodeString()
    test_unicode_str = u"안녕"
    assert test_unicode_str == str(world.value)
    assert test_unicode_str == str(world.GetString())
    assert test_unicode_str == str(world)


def test_interface_conversion():
    """Test interface conversion."""
    from Python.Test import Spam, ISpam

    ob = ConversionTest()

    assert ISpam(ob.SpamField).GetValue() == "spam"
    assert ob.SpamField.GetValue() == "spam"

    ob.SpamField = Spam("eggs")
    assert ISpam(ob.SpamField).GetValue() == "eggs"
    assert ob.SpamField.GetValue() == "eggs"

    # need to test spam subclass here.

    ob.SpamField = None
    assert ob.SpamField is None

    with pytest.raises(TypeError):
        ob = ConversionTest()
        ob.SpamField = System.String("bad")

    with pytest.raises(TypeError):
        ob = ConversionTest()
        ob.SpamField = System.Int32(1)


def test_object_conversion():
    """Test ob conversion."""
    from Python.Test import Spam

    ob = ConversionTest()
    assert ob.ObjectField is None

    ob.ObjectField = Spam("eggs")
    assert ob.ObjectField.__class__.__name__ == "Spam"
    assert ob.ObjectField.GetValue() == "eggs"

    ob.ObjectField = None
    assert ob.ObjectField is None

    ob.ObjectField = System.String("spam")
    assert ob.ObjectField == "spam"

    ob.ObjectField = System.Int32(1)
    assert ob.ObjectField == 1

    # need to test subclass here

    class Foo(object):
        pass
    ob.ObjectField = Foo
    assert ob.ObjectField == Foo

    class PseudoSeq:
        def __getitem__(self, idx):
           return 0

    ob.ObjectField = PseudoSeq()
    assert ob.ObjectField.__class__.__name__ == "PseudoSeq"


def test_null_conversion():
    """Test null conversion."""
    import System

    ob = ConversionTest()

    ob.StringField = None
    assert ob.StringField is None

    ob.ObjectField = None
    assert ob.ObjectField is None

    ob.SpamField = None
    assert ob.SpamField is None

    pi = 22/7
    assert ob.Echo[System.Double](pi) == pi
    assert ob.Echo[System.DateTime](None) is None

    # Primitive types and enums should not be set to null.

    with pytest.raises(TypeError):
        ConversionTest().Int32Field = None

    with pytest.raises(TypeError):
        ConversionTest().EnumField = None


def test_byte_array_conversion():
    """Test byte array conversion."""
    ob = ConversionTest()

    assert ob.ByteArrayField is None

    ob.ByteArrayField = [0, 1, 2, 3, 4]
    array = ob.ByteArrayField
    assert len(array) == 5
    assert array[0] == 0
    assert array[4] == 4

    value = b"testing"
    ob.ByteArrayField = value
    array = ob.ByteArrayField
    for i, _ in enumerate(value):
        assert array[i] == operator.getitem(value, i)


def test_sbyte_array_conversion():
    """Test sbyte array conversion."""
    ob = ConversionTest()

    assert ob.SByteArrayField is None

    ob.SByteArrayField = [0, 1, 2, 3, 4]
    array = ob.SByteArrayField
    assert len(array) == 5
    assert array[0] == 0
    assert array[4] == 4

    value = b"testing"
    ob.SByteArrayField = value
    array = ob.SByteArrayField
    for i, _ in enumerate(value):
        assert array[i] == operator.getitem(value, i)

def test_codecs():
    """Test codec registration from Python"""
    class ListAsRawEncoder(RawProxyEncoder):
        __namespace__ = "Python.Test"
        def CanEncode(self, clr_type):
            return clr_type.Name == "List`1" and clr_type.Namespace == "System.Collections.Generic"

    list_raw_encoder = ListAsRawEncoder()
    PyObjectConversions.RegisterEncoder(list_raw_encoder)

    ob = ConversionTest()

    l = ob.ListField
    l.Add(42)
    assert ob.ListField.Count == 1

    CodecResetter.Reset()

def test_int_param_resolution_required():
    """Test resolution of `int` parameters when resolution is needed"""

    mri = MethodResolutionInt()
    data = list(mri.MethodA(0x1000, 10))
    assert len(data) == 10
    assert data[0] == 0

    data = list(mri.MethodA(0x100000000, 10))
    assert len(data) == 10
    assert data[0] == 0

def test_iconvertible_conversion():
    change_type = System.Convert.ChangeType

    assert 1024 == change_type(1024, System.Int32)
    assert 1024 == change_type(1024, System.Int64)
    assert 1024 == change_type(1024, System.Int16)

def test_intptr_construction():
    from System import IntPtr, UIntPtr, Int64, UInt64
    from ctypes import sizeof, c_void_p

    ptr_size = sizeof(c_void_p)
    max_intptr = 2 ** (ptr_size * 8 - 1) - 1
    min_intptr = -max_intptr - 1
    max_uintptr = 2 ** (ptr_size * 8) - 1
    min_uintptr = 0

    ob = ConversionTest()

    assert ob.IntPtrField == IntPtr.Zero
    assert ob.UIntPtrField == UIntPtr.Zero

    for v in [0, -1, 1024, max_intptr, min_intptr]:
        ob.IntPtrField = IntPtr(Int64(v))
        assert ob.IntPtrField == IntPtr(v)
        assert ob.IntPtrField.ToInt64() == v

    for v in [min_intptr - 1, max_intptr + 1]:
        with pytest.raises(OverflowError):
            IntPtr(v)

    for v in [0, 1024, min_uintptr, max_uintptr, max_intptr]:
        ob.UIntPtrField = UIntPtr(UInt64(v))
        assert ob.UIntPtrField == UIntPtr(v)
        assert ob.UIntPtrField.ToUInt64() == v

    for v in [min_uintptr - 1, max_uintptr + 1, min_intptr]:
        with pytest.raises(OverflowError):
            UIntPtr(v)

def test_explicit_conversion():
    from operator import index
    from System import (
        Int64, UInt64, Int32, UInt32, Int16, UInt16, Byte, SByte, Boolean
    )
    from System import Double, Single

    assert int(Boolean(False)) == 0
    assert int(Boolean(True)) == 1

    for t in [UInt64, UInt32, UInt16, Byte]:
        assert index(t(127)) == 127
        assert int(t(127)) == 127
        assert float(t(127)) == 127.0

    for t in [Int64, Int32, Int16, SByte]:
        assert index(t(127)) == 127
        assert index(t(-127)) == -127
        assert int(t(127)) == 127
        assert int(t(-127)) == -127
        assert float(t(127)) == 127.0
        assert float(t(-127)) == -127.0

    assert int(Int64(Int64.MaxValue)) == 2**63 - 1
    assert int(Int64(Int64.MinValue)) == -2**63
    assert int(UInt64(UInt64.MaxValue)) == 2**64 - 1

    for t in [Single, Double]:
        assert float(t(0.125)) == 0.125
        assert int(t(123.4)) == 123
        with pytest.raises(TypeError):
            index(t(123.4))
