# -*- coding: utf-8 -*-

import unittest

import System
from Python.Test import ConversionTest

from _compat import indexbytes, long, range, unichr


class ConversionTests(unittest.TestCase):
    """Test CLR <-> Python type conversions."""

    def testBoolConversion(self):
        """Test bool conversion."""
        ob = ConversionTest()
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = True
        self.assertTrue(ob.BooleanField == True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = False
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = 1
        self.assertTrue(ob.BooleanField == True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = 0
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(None)
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean('')
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(0)
        self.assertTrue(ob.BooleanField == False)
        self.assertTrue(ob.BooleanField is False)
        self.assertTrue(ob.BooleanField == 0)

        ob.BooleanField = System.Boolean(1)
        self.assertTrue(ob.BooleanField == True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

        ob.BooleanField = System.Boolean('a')
        self.assertTrue(ob.BooleanField == True)
        self.assertTrue(ob.BooleanField is True)
        self.assertTrue(ob.BooleanField == 1)

    def testSByteConversion(self):
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

        def test():
            ConversionTest().SByteField = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().SByteField = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().SByteField = 128

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().SByteField = -129

        self.assertRaises(OverflowError, test)

        def test():
            value = System.SByte(128)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.SByte(-129)

        self.assertRaises(OverflowError, test)

    def testByteConversion(self):
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

        def test():
            ConversionTest().ByteField = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().ByteField = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().ByteField = 256

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().ByteField = -1

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Byte(256)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Byte(-1)

        self.assertRaises(OverflowError, test)

    def testCharConversion(self):
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

        def test():
            ConversionTest().CharField = 65536

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().CharField = -1

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().CharField = None

        self.assertRaises(TypeError, test)

    def testInt16Conversion(self):
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

        def test():
            ConversionTest().Int16Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int16Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int16Field = 32768

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().Int16Field = -32769

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int16(32768)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int16(-32769)

        self.assertRaises(OverflowError, test)

    def testInt32Conversion(self):
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

        def test():
            ConversionTest().Int32Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int32Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int32Field = 2147483648

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().Int32Field = -2147483649

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int32(2147483648)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int32(-2147483649)

        self.assertRaises(OverflowError, test)

    def testInt64Conversion(self):
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

        def test():
            ConversionTest().Int64Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int64Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().Int64Field = long(9223372036854775808)

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().Int64Field = long(-9223372036854775809)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int64(long(9223372036854775808))

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Int64(long(-9223372036854775809))

        self.assertRaises(OverflowError, test)

    def testUInt16Conversion(self):
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

        def test():
            ConversionTest().UInt16Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt16Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt16Field = 65536

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().UInt16Field = -1

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt16(65536)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt16(-1)

        self.assertRaises(OverflowError, test)

    def testUInt32Conversion(self):
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

        def test():
            ConversionTest().UInt32Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt32Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt32Field = long(4294967296)

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().UInt32Field = -1

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt32(long(4294967296))

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt32(-1)

        self.assertRaises(OverflowError, test)

    def testUInt64Conversion(self):
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

        def test():
            ConversionTest().UInt64Field = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt64Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().UInt64Field = long(18446744073709551616)

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().UInt64Field = -1

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt64(long(18446744073709551616))

        self.assertRaises(OverflowError, test)

        def test():
            value = System.UInt64(-1)

        self.assertRaises(OverflowError, test)

    def testSingleConversion(self):
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

        def test():
            ConversionTest().SingleField = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().SingleField = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().SingleField = 3.402824e38

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().SingleField = -3.402824e38

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Single(3.402824e38)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Single(-3.402824e38)

        self.assertRaises(OverflowError, test)

    def testDoubleConversion(self):
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

        def test():
            ConversionTest().DoubleField = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().DoubleField = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().DoubleField = 1.7976931348623159e308

        self.assertRaises(OverflowError, test)

        def test():
            ConversionTest().DoubleField = -1.7976931348623159e308

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Double(1.7976931348623159e308)

        self.assertRaises(OverflowError, test)

        def test():
            value = System.Double(-1.7976931348623159e308)

        self.assertRaises(OverflowError, test)

    def testDecimalConversion(self):
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

        def test():
            ConversionTest().DecimalField = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().DecimalField = "spam"

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().DecimalField = 1

        self.assertRaises(TypeError, test)

    def testStringConversion(self):
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
        self.assertTrue(ob.StringField == None)

        def test():
            ConversionTest().StringField = 1

        self.assertRaises(TypeError, test)

    def testInterfaceConversion(self):
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
        self.assertTrue(ob.SpamField == None)

        def test():
            ob = ConversionTest()
            ob.SpamField = System.String("bad")

        self.assertRaises(TypeError, test)

        def test():
            ob = ConversionTest()
            ob.SpamField = System.Int32(1)

        self.assertRaises(TypeError, test)

    def testObjectConversion(self):
        """Test ob conversion."""
        from Python.Test import Spam

        ob = ConversionTest()
        self.assertTrue(ob.ObjectField == None)

        ob.ObjectField = Spam("eggs")
        self.assertTrue(ob.ObjectField.__class__.__name__ == "Spam")
        self.assertTrue(ob.ObjectField.GetValue() == "eggs")

        ob.ObjectField = None
        self.assertTrue(ob.ObjectField == None)

        ob.ObjectField = System.String("spam")
        self.assertTrue(ob.ObjectField == "spam")

        ob.ObjectField = System.Int32(1)
        self.assertTrue(ob.ObjectField == 1)

        # need to test subclass here

        def test():
            ob = ConversionTest()
            ob.ObjectField = self

        self.assertRaises(TypeError, test)

    def testEnumConversion(self):
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

        def test():
            ob = ConversionTest()
            ob.EnumField = 10

        self.assertRaises(ValueError, test)

        def test():
            ob = ConversionTest()
            ob.EnumField = 255

        self.assertRaises(ValueError, test)

        def test():
            ob = ConversionTest()
            ob.EnumField = 1000000

        self.assertRaises(OverflowError, test)

        def test():
            ob = ConversionTest()
            ob.EnumField = "spam"

        self.assertRaises(TypeError, test)

    def testNullConversion(self):
        """Test null conversion."""
        ob = ConversionTest()

        ob.StringField = None
        self.assertTrue(ob.StringField == None)

        ob.ObjectField = None
        self.assertTrue(ob.ObjectField == None)

        ob.SpamField = None
        self.assertTrue(ob.SpamField == None)

        # Primitive types and enums should not be set to null.

        def test():
            ConversionTest().Int32Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().EnumField = None

        self.assertRaises(TypeError, test)

    def testByteArrayConversion(self):
        """Test byte array conversion."""
        ob = ConversionTest()

        self.assertTrue(ob.ByteArrayField == None)

        ob.ByteArrayField = [0, 1, 2, 3, 4]
        array = ob.ByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = b"testing"
        ob.ByteArrayField = value
        array = ob.ByteArrayField
        for i in range(len(value)):
            self.assertTrue(array[i] == indexbytes(value, i))

    def testSByteArrayConversion(self):
        """Test sbyte array conversion."""
        ob = ConversionTest()

        self.assertTrue(ob.SByteArrayField == None)

        ob.SByteArrayField = [0, 1, 2, 3, 4]
        array = ob.SByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = b"testing"
        ob.SByteArrayField = value
        array = ob.SByteArrayField
        for i in range(len(value)):
            self.assertTrue(array[i] == indexbytes(value, i))


def test_suite():
    return unittest.makeSuite(ConversionTests)
