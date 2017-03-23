# -*- coding: utf-8 -*-

"""Test CLR <-> Python type conversions."""

import System
import pytest
from Python.Test import ConversionTest

from ._compat import indexbytes, long, unichr


def test_bool_conversion():
    """Test bool conversion."""
    ob = ConversionTest()
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = True
    assert ob.BooleanField is True
    assert ob.BooleanField is True
    assert ob.BooleanField == 1

    ob.BooleanField = False
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = 1
    assert ob.BooleanField is True
    assert ob.BooleanField is True
    assert ob.BooleanField == 1

    ob.BooleanField = 0
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = System.Boolean(None)
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = System.Boolean('')
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = System.Boolean(0)
    assert ob.BooleanField is False
    assert ob.BooleanField is False
    assert ob.BooleanField == 0

    ob.BooleanField = System.Boolean(1)
    assert ob.BooleanField is True
    assert ob.BooleanField is True
    assert ob.BooleanField == 1

    ob.BooleanField = System.Boolean('a')
    assert ob.BooleanField is True
    assert ob.BooleanField is True
    assert ob.BooleanField == 1


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
    assert System.Char.MaxValue == unichr(65535)
    assert System.Char.MinValue == unichr(0)

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
    assert System.Int64.MaxValue == long(9223372036854775807)
    assert System.Int64.MinValue == long(-9223372036854775808)

    ob = ConversionTest()
    assert ob.Int64Field == 0

    ob.Int64Field = long(9223372036854775807)
    assert ob.Int64Field == long(9223372036854775807)

    ob.Int64Field = long(-9223372036854775808)
    assert ob.Int64Field == long(-9223372036854775808)

    ob.Int64Field = System.Int64(long(9223372036854775807))
    assert ob.Int64Field == long(9223372036854775807)

    ob.Int64Field = System.Int64(long(-9223372036854775808))
    assert ob.Int64Field == long(-9223372036854775808)

    with pytest.raises(TypeError):
        ConversionTest().Int64Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().Int64Field = None

    with pytest.raises(OverflowError):
        ConversionTest().Int64Field = long(9223372036854775808)

    with pytest.raises(OverflowError):
        ConversionTest().Int64Field = long(-9223372036854775809)

    with pytest.raises(OverflowError):
        _ = System.Int64(long(9223372036854775808))

    with pytest.raises(OverflowError):
        _ = System.Int64(long(-9223372036854775809))


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
    assert System.UInt32.MaxValue == long(4294967295)
    assert System.UInt32.MinValue == 0

    ob = ConversionTest()
    assert ob.UInt32Field == 0

    ob.UInt32Field = long(4294967295)
    assert ob.UInt32Field == long(4294967295)

    ob.UInt32Field = -0
    assert ob.UInt32Field == 0

    ob.UInt32Field = System.UInt32(long(4294967295))
    assert ob.UInt32Field == long(4294967295)

    ob.UInt32Field = System.UInt32(0)
    assert ob.UInt32Field == 0

    with pytest.raises(TypeError):
        ConversionTest().UInt32Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().UInt32Field = None

    with pytest.raises(OverflowError):
        ConversionTest().UInt32Field = long(4294967296)

    with pytest.raises(OverflowError):
        ConversionTest().UInt32Field = -1

    with pytest.raises(OverflowError):
        _ = System.UInt32(long(4294967296))

    with pytest.raises(OverflowError):
        _ = System.UInt32(-1)


def test_uint64_conversion():
    """Test uint64 conversion."""
    assert System.UInt64.MaxValue == long(18446744073709551615)
    assert System.UInt64.MinValue == 0

    ob = ConversionTest()
    assert ob.UInt64Field == 0

    ob.UInt64Field = long(18446744073709551615)
    assert ob.UInt64Field == long(18446744073709551615)

    ob.UInt64Field = -0
    assert ob.UInt64Field == 0

    ob.UInt64Field = System.UInt64(long(18446744073709551615))
    assert ob.UInt64Field == long(18446744073709551615)

    ob.UInt64Field = System.UInt64(0)
    assert ob.UInt64Field == 0

    with pytest.raises(TypeError):
        ConversionTest().UInt64Field = "spam"

    with pytest.raises(TypeError):
        ConversionTest().UInt64Field = None

    with pytest.raises(OverflowError):
        ConversionTest().UInt64Field = long(18446744073709551616)

    with pytest.raises(OverflowError):
        ConversionTest().UInt64Field = -1

    with pytest.raises(OverflowError):
        _ = System.UInt64(long(18446744073709551616))

    with pytest.raises(OverflowError):
        _ = System.UInt64(-1)


def test_single_conversion():
    """Test single conversion."""
    assert System.Single.MaxValue == 3.402823e38
    assert System.Single.MinValue == -3.402823e38

    ob = ConversionTest()
    assert ob.SingleField == 0.0

    ob.SingleField = 3.402823e38
    assert ob.SingleField == 3.402823e38

    ob.SingleField = -3.402823e38
    assert ob.SingleField == -3.402823e38

    ob.SingleField = System.Single(3.402823e38)
    assert ob.SingleField == 3.402823e38

    ob.SingleField = System.Single(-3.402823e38)
    assert ob.SingleField == -3.402823e38

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

    with pytest.raises(OverflowError):
        ConversionTest().DoubleField = 1.7976931348623159e308

    with pytest.raises(OverflowError):
        ConversionTest().DoubleField = -1.7976931348623159e308

    with pytest.raises(OverflowError):
        _ = System.Double(1.7976931348623159e308)

    with pytest.raises(OverflowError):
        _ = System.Double(-1.7976931348623159e308)


def test_decimal_conversion():
    """Test decimal conversion."""
    from System import Decimal

    max_d = Decimal.Parse("79228162514264337593543950335")
    min_d = Decimal.Parse("-79228162514264337593543950335")

    assert Decimal.ToInt64(Decimal(10)) == long(10)

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

    ob.StringField = None
    assert ob.StringField is None

    with pytest.raises(TypeError):
        ConversionTest().StringField = 1


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

    with pytest.raises(TypeError):
        class Foo(object):
            pass
        ob = ConversionTest()
        ob.ObjectField = Foo


def test_enum_conversion():
    """Test enum conversion."""
    from Python.Test import ShortEnum

    ob = ConversionTest()
    assert ob.EnumField == ShortEnum.Zero

    ob.EnumField = ShortEnum.One
    assert ob.EnumField == ShortEnum.One

    ob.EnumField = 0
    assert ob.EnumField == ShortEnum.Zero
    assert ob.EnumField == 0

    ob.EnumField = 1
    assert ob.EnumField == ShortEnum.One
    assert ob.EnumField == 1

    with pytest.raises(ValueError):
        ob = ConversionTest()
        ob.EnumField = 10

    with pytest.raises(ValueError):
        ob = ConversionTest()
        ob.EnumField = 255

    with pytest.raises(OverflowError):
        ob = ConversionTest()
        ob.EnumField = 1000000

    with pytest.raises(TypeError):
        ob = ConversionTest()
        ob.EnumField = "spam"


def test_null_conversion():
    """Test null conversion."""
    ob = ConversionTest()

    ob.StringField = None
    assert ob.StringField is None

    ob.ObjectField = None
    assert ob.ObjectField is None

    ob.SpamField = None
    assert ob.SpamField is None

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
        assert array[i] == indexbytes(value, i)


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
        assert array[i] == indexbytes(value, i)
