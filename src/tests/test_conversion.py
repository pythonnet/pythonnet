# -*- coding: utf-8 -*-

import unittest

import System
from Python.Test import ConversionTest

from _compat import indexbytes, long, unichr


class ConversionTests(unittest.TestCase):
    """Test CLR <-> Python type conversions."""

    def test_bool_conversion(self):
        """Test bool conversion."""
        ob = ConversionTest()
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = True
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = False
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = 1
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = 0
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(None)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean('')
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(0)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(1)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = System.Boolean('a')
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

    def test_sbyte_conversion(self):
        """Test sbyte conversion."""
        self.assertTrue(System.SByte.MaxValue == 127)
        self.assertTrue(System.SByte.MinValue == -128)

        ob = ConversionTest()
        self.assertTrue(ob.SByteField == 0)

        ob.SByteField = 127
        self.assertTrue(ob.SByteField == 127)

        ob.SByteField = -128
        self.assertTrue(ob.SByteField == -128)

        ob.SByteField = System.SByte(127)
        self.assertTrue(ob.SByteField == 127)

        ob.SByteField = System.SByte(-128)
        self.assertTrue(ob.SByteField == -128)

        with self.assertRaises(TypeError):
            ConversionTest().SByteField = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().SByteField = None

        with self.assertRaises(OverflowError):
            ConversionTest().SByteField = 128

        with self.assertRaises(OverflowError):
            ConversionTest().SByteField = -129

        with self.assertRaises(OverflowError):
            _ = System.SByte(128)

        with self.assertRaises(OverflowError):
            _ = System.SByte(-129)

    def test_byte_conversion(self):
        """Test byte conversion."""
        self.assertTrue(System.Byte.MaxValue == 255)
        self.assertTrue(System.Byte.MinValue == 0)

        ob = ConversionTest()
        self.assertTrue(ob.ByteField == 0)

        ob.ByteField = 255
        self.assertTrue(ob.ByteField == 255)

        ob.ByteField = 0
        self.assertTrue(ob.ByteField == 0)

        ob.ByteField = System.Byte(255)
        self.assertTrue(ob.ByteField == 255)

        ob.ByteField = System.Byte(0)
        self.assertTrue(ob.ByteField == 0)

        with self.assertRaises(TypeError):
            ConversionTest().ByteField = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().ByteField = None

        with self.assertRaises(OverflowError):
            ConversionTest().ByteField = 256

        with self.assertRaises(OverflowError):
            ConversionTest().ByteField = -1

        with self.assertRaises(OverflowError):
            _ = System.Byte(256)

        with self.assertRaises(OverflowError):
            _ = System.Byte(-1)

    def test_char_conversion(self):
        """Test char conversion."""
        self.assertTrue(System.Char.MaxValue == unichr(65535))
        self.assertTrue(System.Char.MinValue == unichr(0))

        ob = ConversionTest()
        self.assertTrue(ob.CharField == u'A')

        ob.CharField = 'B'
        self.assertTrue(ob.CharField == u'B')

        ob.CharField = u'B'
        self.assertTrue(ob.CharField == u'B')

        ob.CharField = 67
        self.assertTrue(ob.CharField == u'C')

        with self.assertRaises(OverflowError):
            ConversionTest().CharField = 65536

        with self.assertRaises(OverflowError):
            ConversionTest().CharField = -1

        with self.assertRaises(TypeError):
            ConversionTest().CharField = None

    def test_int16_conversion(self):
        """Test int16 conversion."""
        self.assertTrue(System.Int16.MaxValue == 32767)
        self.assertTrue(System.Int16.MinValue == -32768)

        ob = ConversionTest()
        self.assertTrue(ob.Int16Field == 0)

        ob.Int16Field = 32767
        self.assertTrue(ob.Int16Field == 32767)

        ob.Int16Field = -32768
        self.assertTrue(ob.Int16Field == -32768)

        ob.Int16Field = System.Int16(32767)
        self.assertTrue(ob.Int16Field == 32767)

        ob.Int16Field = System.Int16(-32768)
        self.assertTrue(ob.Int16Field == -32768)

        with self.assertRaises(TypeError):
            ConversionTest().Int16Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().Int16Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().Int16Field = 32768

        with self.assertRaises(OverflowError):
            ConversionTest().Int16Field = -32769

        with self.assertRaises(OverflowError):
            _ = System.Int16(32768)

        with self.assertRaises(OverflowError):
            _ = System.Int16(-32769)

    def test_int32_conversion(self):
        """Test int32 conversion."""
        self.assertTrue(System.Int32.MaxValue == 2147483647)
        self.assertTrue(System.Int32.MinValue == -2147483648)

        ob = ConversionTest()
        self.assertTrue(ob.Int32Field == 0)

        ob.Int32Field = 2147483647
        self.assertTrue(ob.Int32Field == 2147483647)

        ob.Int32Field = -2147483648
        self.assertTrue(ob.Int32Field == -2147483648)

        ob.Int32Field = System.Int32(2147483647)
        self.assertTrue(ob.Int32Field == 2147483647)

        ob.Int32Field = System.Int32(-2147483648)
        self.assertTrue(ob.Int32Field == -2147483648)

        with self.assertRaises(TypeError):
            ConversionTest().Int32Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().Int32Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().Int32Field = 2147483648

        with self.assertRaises(OverflowError):
            ConversionTest().Int32Field = -2147483649

        with self.assertRaises(OverflowError):
            _ = System.Int32(2147483648)

        with self.assertRaises(OverflowError):
            _ = System.Int32(-2147483649)

    def test_int64_conversion(self):
        """Test int64 conversion."""
        self.assertTrue(System.Int64.MaxValue == long(9223372036854775807))
        self.assertTrue(System.Int64.MinValue == long(-9223372036854775808))

        ob = ConversionTest()
        self.assertTrue(ob.Int64Field == 0)

        ob.Int64Field = long(9223372036854775807)
        self.assertTrue(ob.Int64Field == long(9223372036854775807))

        ob.Int64Field = long(-9223372036854775808)
        self.assertTrue(ob.Int64Field == long(-9223372036854775808))

        ob.Int64Field = System.Int64(long(9223372036854775807))
        self.assertTrue(ob.Int64Field == long(9223372036854775807))

        ob.Int64Field = System.Int64(long(-9223372036854775808))
        self.assertTrue(ob.Int64Field == long(-9223372036854775808))

        with self.assertRaises(TypeError):
            ConversionTest().Int64Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().Int64Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().Int64Field = long(9223372036854775808)

        with self.assertRaises(OverflowError):
            ConversionTest().Int64Field = long(-9223372036854775809)

        with self.assertRaises(OverflowError):
            _ = System.Int64(long(9223372036854775808))

        with self.assertRaises(OverflowError):
            _ = System.Int64(long(-9223372036854775809))

    def test_uint16_conversion(self):
        """Test uint16 conversion."""
        self.assertTrue(System.UInt16.MaxValue == 65535)
        self.assertTrue(System.UInt16.MinValue == 0)

        ob = ConversionTest()
        self.assertTrue(ob.UInt16Field == 0)

        ob.UInt16Field = 65535
        self.assertTrue(ob.UInt16Field == 65535)

        ob.UInt16Field = -0
        self.assertTrue(ob.UInt16Field == 0)

        ob.UInt16Field = System.UInt16(65535)
        self.assertTrue(ob.UInt16Field == 65535)

        ob.UInt16Field = System.UInt16(0)
        self.assertTrue(ob.UInt16Field == 0)

        with self.assertRaises(TypeError):
            ConversionTest().UInt16Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().UInt16Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().UInt16Field = 65536

        with self.assertRaises(OverflowError):
            ConversionTest().UInt16Field = -1

        with self.assertRaises(OverflowError):
            _ = System.UInt16(65536)

        with self.assertRaises(OverflowError):
            _ = System.UInt16(-1)

    def test_uint32_conversion(self):
        """Test uint32 conversion."""
        self.assertTrue(System.UInt32.MaxValue == long(4294967295))
        self.assertTrue(System.UInt32.MinValue == 0)

        ob = ConversionTest()
        self.assertTrue(ob.UInt32Field == 0)

        ob.UInt32Field = long(4294967295)
        self.assertTrue(ob.UInt32Field == long(4294967295))

        ob.UInt32Field = -0
        self.assertTrue(ob.UInt32Field == 0)

        ob.UInt32Field = System.UInt32(long(4294967295))
        self.assertTrue(ob.UInt32Field == long(4294967295))

        ob.UInt32Field = System.UInt32(0)
        self.assertTrue(ob.UInt32Field == 0)

        with self.assertRaises(TypeError):
            ConversionTest().UInt32Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().UInt32Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().UInt32Field = long(4294967296)

        with self.assertRaises(OverflowError):
            ConversionTest().UInt32Field = -1

        with self.assertRaises(OverflowError):
            _ = System.UInt32(long(4294967296))

        with self.assertRaises(OverflowError):
            _ = System.UInt32(-1)

    def test_uint64_conversion(self):
        """Test uint64 conversion."""
        self.assertTrue(System.UInt64.MaxValue == long(18446744073709551615))
        self.assertTrue(System.UInt64.MinValue == 0)

        ob = ConversionTest()
        self.assertTrue(ob.UInt64Field == 0)

        ob.UInt64Field = long(18446744073709551615)
        self.assertTrue(ob.UInt64Field == long(18446744073709551615))

        ob.UInt64Field = -0
        self.assertTrue(ob.UInt64Field == 0)

        ob.UInt64Field = System.UInt64(long(18446744073709551615))
        self.assertTrue(ob.UInt64Field == long(18446744073709551615))

        ob.UInt64Field = System.UInt64(0)
        self.assertTrue(ob.UInt64Field == 0)

        with self.assertRaises(TypeError):
            ConversionTest().UInt64Field = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().UInt64Field = None

        with self.assertRaises(OverflowError):
            ConversionTest().UInt64Field = long(18446744073709551616)

        with self.assertRaises(OverflowError):
            ConversionTest().UInt64Field = -1

        with self.assertRaises(OverflowError):
            _ = System.UInt64(long(18446744073709551616))

        with self.assertRaises(OverflowError):
            _ = System.UInt64(-1)

    def test_single_conversion(self):
        """Test single conversion."""
        self.assertTrue(System.Single.MaxValue == 3.402823e38)
        self.assertTrue(System.Single.MinValue == -3.402823e38)

        ob = ConversionTest()
        self.assertTrue(ob.SingleField == 0.0)

        ob.SingleField = 3.402823e38
        self.assertTrue(ob.SingleField == 3.402823e38)

        ob.SingleField = -3.402823e38
        self.assertTrue(ob.SingleField == -3.402823e38)

        ob.SingleField = System.Single(3.402823e38)
        self.assertTrue(ob.SingleField == 3.402823e38)

        ob.SingleField = System.Single(-3.402823e38)
        self.assertTrue(ob.SingleField == -3.402823e38)

        with self.assertRaises(TypeError):
            ConversionTest().SingleField = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().SingleField = None

        with self.assertRaises(OverflowError):
            ConversionTest().SingleField = 3.402824e38

        with self.assertRaises(OverflowError):
            ConversionTest().SingleField = -3.402824e38

        with self.assertRaises(OverflowError):
            _ = System.Single(3.402824e38)

        with self.assertRaises(OverflowError):
            _ = System.Single(-3.402824e38)

    def test_double_conversion(self):
        """Test double conversion."""
        self.assertTrue(System.Double.MaxValue == 1.7976931348623157e308)
        self.assertTrue(System.Double.MinValue == -1.7976931348623157e308)

        ob = ConversionTest()
        self.assertTrue(ob.DoubleField == 0.0)

        ob.DoubleField = 1.7976931348623157e308
        self.assertTrue(ob.DoubleField == 1.7976931348623157e308)

        ob.DoubleField = -1.7976931348623157e308
        self.assertTrue(ob.DoubleField == -1.7976931348623157e308)

        ob.DoubleField = System.Double(1.7976931348623157e308)
        self.assertTrue(ob.DoubleField == 1.7976931348623157e308)

        ob.DoubleField = System.Double(-1.7976931348623157e308)
        self.assertTrue(ob.DoubleField == -1.7976931348623157e308)

        with self.assertRaises(TypeError):
            ConversionTest().DoubleField = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().DoubleField = None

        with self.assertRaises(OverflowError):
            ConversionTest().DoubleField = 1.7976931348623159e308

        with self.assertRaises(OverflowError):
            ConversionTest().DoubleField = -1.7976931348623159e308

        with self.assertRaises(OverflowError):
            _ = System.Double(1.7976931348623159e308)

        with self.assertRaises(OverflowError):
            _ = System.Double(-1.7976931348623159e308)

    def test_decimal_conversion(self):
        """Test decimal conversion."""
        from System import Decimal

        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.assertTrue(Decimal.ToInt64(Decimal(10)) == long(10))

        ob = ConversionTest()
        self.assertTrue(ob.DecimalField == Decimal(0))

        ob.DecimalField = Decimal(10)
        self.assertTrue(ob.DecimalField == Decimal(10))

        ob.DecimalField = Decimal.One
        self.assertTrue(ob.DecimalField == Decimal.One)

        ob.DecimalField = Decimal.Zero
        self.assertTrue(ob.DecimalField == Decimal.Zero)

        ob.DecimalField = max_d
        self.assertTrue(ob.DecimalField == max_d)

        ob.DecimalField = min_d
        self.assertTrue(ob.DecimalField == min_d)

        with self.assertRaises(TypeError):
            ConversionTest().DecimalField = None

        with self.assertRaises(TypeError):
            ConversionTest().DecimalField = "spam"

        with self.assertRaises(TypeError):
            ConversionTest().DecimalField = 1

    def test_string_conversion(self):
        """Test string / unicode conversion."""
        ob = ConversionTest()

        self.assertTrue(ob.StringField == "spam")
        self.assertTrue(ob.StringField == u"spam")

        ob.StringField = "eggs"
        self.assertTrue(ob.StringField == "eggs")
        self.assertTrue(ob.StringField == u"eggs")

        ob.StringField = u"spam"
        self.assertTrue(ob.StringField == "spam")
        self.assertTrue(ob.StringField == u"spam")

        ob.StringField = u'\uffff\uffff'
        self.assertTrue(ob.StringField == u'\uffff\uffff')

        ob.StringField = System.String("spam")
        self.assertTrue(ob.StringField == "spam")
        self.assertTrue(ob.StringField == u"spam")

        ob.StringField = System.String(u'\uffff\uffff')
        self.assertTrue(ob.StringField == u'\uffff\uffff')

        ob.StringField = None
        self.assertTrue(ob.StringField is None)

        with self.assertRaises(TypeError):
            ConversionTest().StringField = 1

    def test_interface_conversion(self):
        """Test interface conversion."""
        from Python.Test import Spam, ISpam

        ob = ConversionTest()

        self.assertTrue(ISpam(ob.SpamField).GetValue() == "spam")
        self.assertTrue(ob.SpamField.GetValue() == "spam")

        ob.SpamField = Spam("eggs")
        self.assertTrue(ISpam(ob.SpamField).GetValue() == "eggs")
        self.assertTrue(ob.SpamField.GetValue() == "eggs")

        # need to test spam subclass here.

        ob.SpamField = None
        self.assertTrue(ob.SpamField is None)

        with self.assertRaises(TypeError):
            ob = ConversionTest()
            ob.SpamField = System.String("bad")

        with self.assertRaises(TypeError):
            ob = ConversionTest()
            ob.SpamField = System.Int32(1)

    def test_object_conversion(self):
        """Test ob conversion."""
        from Python.Test import Spam

        ob = ConversionTest()
        self.assertTrue(ob.ObjectField is None)

        ob.ObjectField = Spam("eggs")
        self.assertTrue(ob.ObjectField.__class__.__name__ == "Spam")
        self.assertTrue(ob.ObjectField.GetValue() == "eggs")

        ob.ObjectField = None
        self.assertTrue(ob.ObjectField is None)

        ob.ObjectField = System.String("spam")
        self.assertTrue(ob.ObjectField == "spam")

        ob.ObjectField = System.Int32(1)
        self.assertTrue(ob.ObjectField == 1)

        # need to test subclass here

        with self.assertRaises(TypeError):
            ob = ConversionTest()
            ob.ObjectField = self

    def test_enum_conversion(self):
        """Test enum conversion."""
        from Python.Test import ShortEnum

        ob = ConversionTest()
        self.assertTrue(ob.EnumField == ShortEnum.Zero)

        ob.EnumField = ShortEnum.One
        self.assertTrue(ob.EnumField == ShortEnum.One)

        ob.EnumField = 0
        self.assertTrue(ob.EnumField == ShortEnum.Zero)
        self.assertTrue(ob.EnumField == 0)

        ob.EnumField = 1
        self.assertTrue(ob.EnumField == ShortEnum.One)
        self.assertTrue(ob.EnumField == 1)

        with self.assertRaises(ValueError):
            ob = ConversionTest()
            ob.EnumField = 10

        with self.assertRaises(ValueError):
            ob = ConversionTest()
            ob.EnumField = 255

        with self.assertRaises(OverflowError):
            ob = ConversionTest()
            ob.EnumField = 1000000

        with self.assertRaises(TypeError):
            ob = ConversionTest()
            ob.EnumField = "spam"

    def test_null_conversion(self):
        """Test null conversion."""
        ob = ConversionTest()

        ob.StringField = None
        self.assertTrue(ob.StringField is None)

        ob.ObjectField = None
        self.assertTrue(ob.ObjectField is None)

        ob.SpamField = None
        self.assertTrue(ob.SpamField is None)

        # Primitive types and enums should not be set to null.

        with self.assertRaises(TypeError):
            ConversionTest().Int32Field = None

        with self.assertRaises(TypeError):
            ConversionTest().EnumField = None

    def test_byte_array_conversion(self):
        """Test byte array conversion."""
        ob = ConversionTest()

        self.assertTrue(ob.ByteArrayField is None)

        ob.ByteArrayField = [0, 1, 2, 3, 4]
        array = ob.ByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = b"testing"
        ob.ByteArrayField = value
        array = ob.ByteArrayField
        for i, _ in enumerate(value):
            self.assertTrue(array[i] == indexbytes(value, i))

    def test_sbyte_array_conversion(self):
        """Test sbyte array conversion."""
        ob = ConversionTest()

        self.assertTrue(ob.SByteArrayField is None)

        ob.SByteArrayField = [0, 1, 2, 3, 4]
        array = ob.SByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = b"testing"
        ob.SByteArrayField = value
        array = ob.SByteArrayField
        for i, _ in enumerate(value):
            self.assertTrue(array[i] == indexbytes(value, i))


def test_suite():
    return unittest.makeSuite(ConversionTests)
