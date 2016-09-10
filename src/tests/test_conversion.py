import sys, os, string, unittest, types
from Python.Test import ConversionTest
import System
import six

if six.PY3:
    long = int
    unichr = chr


class ConversionTests(unittest.TestCase):
    """Test CLR <-> Python type conversions."""

    def testBoolConversion(self):
        """Test bool conversion."""
        object = ConversionTest()
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = True
        self.assertTrue(object.BooleanField == True)
        self.assertTrue(object.BooleanField is True)
        self.assertTrue(object.BooleanField == 1)

        object.BooleanField = False
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = 1
        self.assertTrue(object.BooleanField == True)
        self.assertTrue(object.BooleanField is True)
        self.assertTrue(object.BooleanField == 1)

        object.BooleanField = 0
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = System.Boolean(None)
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = System.Boolean('')
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = System.Boolean(0)
        self.assertTrue(object.BooleanField == False)
        self.assertTrue(object.BooleanField is False)
        self.assertTrue(object.BooleanField == 0)

        object.BooleanField = System.Boolean(1)
        self.assertTrue(object.BooleanField == True)
        self.assertTrue(object.BooleanField is True)
        self.assertTrue(object.BooleanField == 1)

        object.BooleanField = System.Boolean('a')
        self.assertTrue(object.BooleanField == True)
        self.assertTrue(object.BooleanField is True)
        self.assertTrue(object.BooleanField == 1)

    def testSByteConversion(self):
        """Test sbyte conversion."""
        self.assertTrue(System.SByte.MaxValue == 127)
        self.assertTrue(System.SByte.MinValue == -128)

        object = ConversionTest()
        self.assertTrue(object.SByteField == 0)

        object.SByteField = 127
        self.assertTrue(object.SByteField == 127)

        object.SByteField = -128
        self.assertTrue(object.SByteField == -128)

        object.SByteField = System.SByte(127)
        self.assertTrue(object.SByteField == 127)

        object.SByteField = System.SByte(-128)
        self.assertTrue(object.SByteField == -128)

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

        object = ConversionTest()
        self.assertTrue(object.ByteField == 0)

        object.ByteField = 255
        self.assertTrue(object.ByteField == 255)

        object.ByteField = 0
        self.assertTrue(object.ByteField == 0)

        object.ByteField = System.Byte(255)
        self.assertTrue(object.ByteField == 255)

        object.ByteField = System.Byte(0)
        self.assertTrue(object.ByteField == 0)

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

        object = ConversionTest()
        self.assertTrue(object.CharField == six.u('A'))

        object.CharField = 'B'
        self.assertTrue(object.CharField == six.u('B'))

        object.CharField = six.u('B')
        self.assertTrue(object.CharField == six.u('B'))

        object.CharField = 67
        self.assertTrue(object.CharField == six.u('C'))

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

        object = ConversionTest()
        self.assertTrue(object.Int16Field == 0)

        object.Int16Field = 32767
        self.assertTrue(object.Int16Field == 32767)

        object.Int16Field = -32768
        self.assertTrue(object.Int16Field == -32768)

        object.Int16Field = System.Int16(32767)
        self.assertTrue(object.Int16Field == 32767)

        object.Int16Field = System.Int16(-32768)
        self.assertTrue(object.Int16Field == -32768)

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

        object = ConversionTest()
        self.assertTrue(object.Int32Field == 0)

        object.Int32Field = 2147483647
        self.assertTrue(object.Int32Field == 2147483647)

        object.Int32Field = -2147483648
        self.assertTrue(object.Int32Field == -2147483648)

        object.Int32Field = System.Int32(2147483647)
        self.assertTrue(object.Int32Field == 2147483647)

        object.Int32Field = System.Int32(-2147483648)
        self.assertTrue(object.Int32Field == -2147483648)

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

        object = ConversionTest()
        self.assertTrue(object.Int64Field == 0)

        object.Int64Field = long(9223372036854775807)
        self.assertTrue(object.Int64Field == long(9223372036854775807))

        object.Int64Field = long(-9223372036854775808)
        self.assertTrue(object.Int64Field == long(-9223372036854775808))

        object.Int64Field = System.Int64(long(9223372036854775807))
        self.assertTrue(object.Int64Field == long(9223372036854775807))

        object.Int64Field = System.Int64(long(-9223372036854775808))
        self.assertTrue(object.Int64Field == long(-9223372036854775808))

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

        object = ConversionTest()
        self.assertTrue(object.UInt16Field == 0)

        object.UInt16Field = 65535
        self.assertTrue(object.UInt16Field == 65535)

        object.UInt16Field = -0
        self.assertTrue(object.UInt16Field == 0)

        object.UInt16Field = System.UInt16(65535)
        self.assertTrue(object.UInt16Field == 65535)

        object.UInt16Field = System.UInt16(0)
        self.assertTrue(object.UInt16Field == 0)

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

        object = ConversionTest()
        self.assertTrue(object.UInt32Field == 0)

        object.UInt32Field = long(4294967295)
        self.assertTrue(object.UInt32Field == long(4294967295))

        object.UInt32Field = -0
        self.assertTrue(object.UInt32Field == 0)

        object.UInt32Field = System.UInt32(long(4294967295))
        self.assertTrue(object.UInt32Field == long(4294967295))

        object.UInt32Field = System.UInt32(0)
        self.assertTrue(object.UInt32Field == 0)

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

        object = ConversionTest()
        self.assertTrue(object.UInt64Field == 0)

        object.UInt64Field = long(18446744073709551615)
        self.assertTrue(object.UInt64Field == long(18446744073709551615))

        object.UInt64Field = -0
        self.assertTrue(object.UInt64Field == 0)

        object.UInt64Field = System.UInt64(long(18446744073709551615))
        self.assertTrue(object.UInt64Field == long(18446744073709551615))

        object.UInt64Field = System.UInt64(0)
        self.assertTrue(object.UInt64Field == 0)

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

        object = ConversionTest()
        self.assertTrue(object.SingleField == 0.0)

        object.SingleField = 3.402823e38
        self.assertTrue(object.SingleField == 3.402823e38)

        object.SingleField = -3.402823e38
        self.assertTrue(object.SingleField == -3.402823e38)

        object.SingleField = System.Single(3.402823e38)
        self.assertTrue(object.SingleField == 3.402823e38)

        object.SingleField = System.Single(-3.402823e38)
        self.assertTrue(object.SingleField == -3.402823e38)

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

        object = ConversionTest()
        self.assertTrue(object.DoubleField == 0.0)

        object.DoubleField = 1.7976931348623157e308
        self.assertTrue(object.DoubleField == 1.7976931348623157e308)

        object.DoubleField = -1.7976931348623157e308
        self.assertTrue(object.DoubleField == -1.7976931348623157e308)

        object.DoubleField = System.Double(1.7976931348623157e308)
        self.assertTrue(object.DoubleField == 1.7976931348623157e308)

        object.DoubleField = System.Double(-1.7976931348623157e308)
        self.assertTrue(object.DoubleField == -1.7976931348623157e308)

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

        object = ConversionTest()
        self.assertTrue(object.DecimalField == Decimal(0))

        object.DecimalField = Decimal(10)
        self.assertTrue(object.DecimalField == Decimal(10))

        object.DecimalField = Decimal.One
        self.assertTrue(object.DecimalField == Decimal.One)

        object.DecimalField = Decimal.Zero
        self.assertTrue(object.DecimalField == Decimal.Zero)

        object.DecimalField = max_d
        self.assertTrue(object.DecimalField == max_d)

        object.DecimalField = min_d
        self.assertTrue(object.DecimalField == min_d)

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
        object = ConversionTest()

        self.assertTrue(object.StringField == "spam")
        self.assertTrue(object.StringField == six.u("spam"))

        object.StringField = "eggs"
        self.assertTrue(object.StringField == "eggs")
        self.assertTrue(object.StringField == six.u("eggs"))

        object.StringField = six.u("spam")
        self.assertTrue(object.StringField == "spam")
        self.assertTrue(object.StringField == six.u("spam"))

        object.StringField = six.u('\uffff\uffff')
        self.assertTrue(object.StringField == six.u('\uffff\uffff'))

        object.StringField = System.String("spam")
        self.assertTrue(object.StringField == "spam")
        self.assertTrue(object.StringField == six.u("spam"))

        object.StringField = System.String(six.u('\uffff\uffff'))
        self.assertTrue(object.StringField == six.u('\uffff\uffff'))

        object.StringField = None
        self.assertTrue(object.StringField == None)

        def test():
            ConversionTest().StringField = 1

        self.assertRaises(TypeError, test)

    def testInterfaceConversion(self):
        """Test interface conversion."""
        from Python.Test import Spam, ISpam

        object = ConversionTest()

        self.assertTrue(ISpam(object.SpamField).GetValue() == "spam")
        self.assertTrue(object.SpamField.GetValue() == "spam")

        object.SpamField = Spam("eggs")
        self.assertTrue(ISpam(object.SpamField).GetValue() == "eggs")
        self.assertTrue(object.SpamField.GetValue() == "eggs")

        # need to test spam subclass here.

        object.SpamField = None
        self.assertTrue(object.SpamField == None)

        def test():
            object = ConversionTest()
            object.SpamField = System.String("bad")

        self.assertRaises(TypeError, test)

        def test():
            object = ConversionTest()
            object.SpamField = System.Int32(1)

        self.assertRaises(TypeError, test)

    def testObjectConversion(self):
        """Test object conversion."""
        from Python.Test import Spam

        object = ConversionTest()
        self.assertTrue(object.ObjectField == None)

        object.ObjectField = Spam("eggs")
        self.assertTrue(object.ObjectField.__class__.__name__ == "Spam")
        self.assertTrue(object.ObjectField.GetValue() == "eggs")

        object.ObjectField = None
        self.assertTrue(object.ObjectField == None)

        object.ObjectField = System.String("spam")
        self.assertTrue(object.ObjectField == "spam")

        object.ObjectField = System.Int32(1)
        self.assertTrue(object.ObjectField == 1)

        # need to test subclass here

        def test():
            object = ConversionTest()
            object.ObjectField = self

        self.assertRaises(TypeError, test)

    def testEnumConversion(self):
        """Test enum conversion."""
        from Python.Test import ShortEnum

        object = ConversionTest()
        self.assertTrue(object.EnumField == ShortEnum.Zero)

        object.EnumField = ShortEnum.One
        self.assertTrue(object.EnumField == ShortEnum.One)

        object.EnumField = 0
        self.assertTrue(object.EnumField == ShortEnum.Zero)
        self.assertTrue(object.EnumField == 0)

        object.EnumField = 1
        self.assertTrue(object.EnumField == ShortEnum.One)
        self.assertTrue(object.EnumField == 1)

        def test():
            object = ConversionTest()
            object.EnumField = 10

        self.assertRaises(ValueError, test)

        def test():
            object = ConversionTest()
            object.EnumField = 255

        self.assertRaises(ValueError, test)

        def test():
            object = ConversionTest()
            object.EnumField = 1000000

        self.assertRaises(OverflowError, test)

        def test():
            object = ConversionTest()
            object.EnumField = "spam"

        self.assertRaises(TypeError, test)

    def testNullConversion(self):
        """Test null conversion."""
        object = ConversionTest()

        object.StringField = None
        self.assertTrue(object.StringField == None)

        object.ObjectField = None
        self.assertTrue(object.ObjectField == None)

        object.SpamField = None
        self.assertTrue(object.SpamField == None)

        # Primitive types and enums should not be set to null.

        def test():
            ConversionTest().Int32Field = None

        self.assertRaises(TypeError, test)

        def test():
            ConversionTest().EnumField = None

        self.assertRaises(TypeError, test)

    def testByteArrayConversion(self):
        """Test byte array conversion."""
        object = ConversionTest()

        self.assertTrue(object.ByteArrayField == None)

        object.ByteArrayField = [0, 1, 2, 3, 4]
        array = object.ByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = six.b("testing")
        object.ByteArrayField = value
        array = object.ByteArrayField
        for i in range(len(value)):
            self.assertTrue(array[i] == six.indexbytes(value, i))

    def testSByteArrayConversion(self):
        """Test sbyte array conversion."""
        object = ConversionTest()

        self.assertTrue(object.SByteArrayField == None)

        object.SByteArrayField = [0, 1, 2, 3, 4]
        array = object.SByteArrayField
        self.assertTrue(len(array) == 5)
        self.assertTrue(array[0] == 0)
        self.assertTrue(array[4] == 4)

        value = six.b("testing")
        object.SByteArrayField = value
        array = object.SByteArrayField
        for i in range(len(value)):
            self.assertTrue(array[i] == six.indexbytes(value, i))


def test_suite():
    return unittest.makeSuite(ConversionTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
