# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from Python.Test import ConversionTest
import System


class ConversionTests(unittest.TestCase):
    """Test CLR <-> Python type conversions."""

    def testBoolConversion(self):
        """Test bool conversion."""
        object = ConversionTest()
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)
        
        object.BooleanField = True
        self.failUnless(object.BooleanField == True)
        self.failUnless(object.BooleanField is True)
        self.failUnless(object.BooleanField == 1)
        
        object.BooleanField = False
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)

        object.BooleanField = 1
        self.failUnless(object.BooleanField == True)
        self.failUnless(object.BooleanField is True)
        self.failUnless(object.BooleanField == 1)

        object.BooleanField = 0
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)

        object.BooleanField = System.Boolean(None)
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)

        object.BooleanField = System.Boolean('')
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)

        object.BooleanField = System.Boolean(0)
        self.failUnless(object.BooleanField == False)
        self.failUnless(object.BooleanField is False)
        self.failUnless(object.BooleanField == 0)

        object.BooleanField = System.Boolean(1)
        self.failUnless(object.BooleanField == True)
        self.failUnless(object.BooleanField is True)
        self.failUnless(object.BooleanField == 1)

        object.BooleanField = System.Boolean('a')
        self.failUnless(object.BooleanField == True)
        self.failUnless(object.BooleanField is True)
        self.failUnless(object.BooleanField == 1)


    def testSByteConversion(self):
        """Test sbyte conversion."""
        self.failUnless(System.SByte.MaxValue == 127)
        self.failUnless(System.SByte.MinValue == -128)

        object = ConversionTest()
        self.failUnless(object.SByteField == 0)

        object.SByteField = 127
        self.failUnless(object.SByteField == 127)

        object.SByteField = -128
        self.failUnless(object.SByteField == -128)

        object.SByteField = System.SByte(127)
        self.failUnless(object.SByteField == 127)

        object.SByteField = System.SByte(-128)
        self.failUnless(object.SByteField == -128)

        def test():
            ConversionTest().SByteField = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().SByteField = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().SByteField = 128
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().SByteField = -129
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.SByte(128)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.SByte(-129)
            
        self.failUnlessRaises(OverflowError, test)


    def testByteConversion(self):
        """Test byte conversion."""
        self.failUnless(System.Byte.MaxValue == 255)
        self.failUnless(System.Byte.MinValue == 0)

        object = ConversionTest()
        self.failUnless(object.ByteField == 0)

        object.ByteField = 255
        self.failUnless(object.ByteField == 255)

        object.ByteField = 0
        self.failUnless(object.ByteField == 0)

        object.ByteField = System.Byte(255)
        self.failUnless(object.ByteField == 255)

        object.ByteField = System.Byte(0)
        self.failUnless(object.ByteField == 0)

        def test():
            ConversionTest().ByteField = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().ByteField = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().ByteField = 256
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().ByteField = -1
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Byte(256)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Byte(-1)
            
        self.failUnlessRaises(OverflowError, test)


    def testCharConversion(self):
        """Test char conversion."""
        self.failUnless(System.Char.MaxValue == unichr(65535))
        self.failUnless(System.Char.MinValue == unichr(0))

        object = ConversionTest()
        self.failUnless(object.CharField == u'A')

        object.CharField = 'B'
        self.failUnless(object.CharField == u'B')

        object.CharField = u'B'
        self.failUnless(object.CharField == u'B')

        object.CharField = 67
        self.failUnless(object.CharField == u'C')

        def test():
            ConversionTest().CharField = 65536
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().CharField = -1
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().CharField = None

        self.failUnlessRaises(TypeError, test)


    def testInt16Conversion(self):
        """Test int16 conversion."""
        self.failUnless(System.Int16.MaxValue == 32767)
        self.failUnless(System.Int16.MinValue == -32768)

        object = ConversionTest()
        self.failUnless(object.Int16Field == 0)

        object.Int16Field = 32767
        self.failUnless(object.Int16Field == 32767)

        object.Int16Field = -32768
        self.failUnless(object.Int16Field == -32768)

        object.Int16Field = System.Int16(32767)
        self.failUnless(object.Int16Field == 32767)

        object.Int16Field = System.Int16(-32768)
        self.failUnless(object.Int16Field == -32768)

        def test():
            ConversionTest().Int16Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int16Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int16Field = 32768
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().Int16Field = -32769
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int16(32768)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int16(-32769)
            
        self.failUnlessRaises(OverflowError, test)


    def testInt32Conversion(self):
        """Test int32 conversion."""
        self.failUnless(System.Int32.MaxValue == 2147483647)
        self.failUnless(System.Int32.MinValue == -2147483648)

        object = ConversionTest()
        self.failUnless(object.Int32Field == 0)

        object.Int32Field = 2147483647
        self.failUnless(object.Int32Field == 2147483647)

        object.Int32Field = -2147483648
        self.failUnless(object.Int32Field == -2147483648)

        object.Int32Field = System.Int32(2147483647)
        self.failUnless(object.Int32Field == 2147483647)

        object.Int32Field = System.Int32(-2147483648)
        self.failUnless(object.Int32Field == -2147483648)

        def test():
            ConversionTest().Int32Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int32Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int32Field = 2147483648
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().Int32Field = -2147483649
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int32(2147483648)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int32(-2147483649)
            
        self.failUnlessRaises(OverflowError, test)


    def testInt64Conversion(self):
        """Test int64 conversion."""
        self.failUnless(System.Int64.MaxValue == 9223372036854775807L)
        self.failUnless(System.Int64.MinValue == -9223372036854775808L)

        object = ConversionTest()
        self.failUnless(object.Int64Field == 0)

        object.Int64Field = 9223372036854775807L
        self.failUnless(object.Int64Field == 9223372036854775807L)

        object.Int64Field = -9223372036854775808L
        self.failUnless(object.Int64Field == -9223372036854775808L)

        object.Int64Field = System.Int64(9223372036854775807L)
        self.failUnless(object.Int64Field == 9223372036854775807L)

        object.Int64Field = System.Int64(-9223372036854775808L)
        self.failUnless(object.Int64Field == -9223372036854775808L)

        def test():
            ConversionTest().Int64Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int64Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().Int64Field = 9223372036854775808L
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().Int64Field = -9223372036854775809L
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int64(9223372036854775808L)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Int64(-9223372036854775809L)
            
        self.failUnlessRaises(OverflowError, test)


    def testUInt16Conversion(self):
        """Test uint16 conversion."""
        self.failUnless(System.UInt16.MaxValue == 65535)
        self.failUnless(System.UInt16.MinValue == 0)

        object = ConversionTest()
        self.failUnless(object.UInt16Field == 0)

        object.UInt16Field = 65535
        self.failUnless(object.UInt16Field == 65535)

        object.UInt16Field = -0
        self.failUnless(object.UInt16Field == 0)

        object.UInt16Field = System.UInt16(65535)
        self.failUnless(object.UInt16Field == 65535)

        object.UInt16Field = System.UInt16(0)
        self.failUnless(object.UInt16Field == 0)

        def test():
            ConversionTest().UInt16Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt16Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt16Field = 65536
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().UInt16Field = -1
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt16(65536)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt16(-1)
            
        self.failUnlessRaises(OverflowError, test)


    def testUInt32Conversion(self):
        """Test uint32 conversion."""
        self.failUnless(System.UInt32.MaxValue == 4294967295L)
        self.failUnless(System.UInt32.MinValue == 0)

        object = ConversionTest()
        self.failUnless(object.UInt32Field == 0)

        object.UInt32Field = 4294967295L
        self.failUnless(object.UInt32Field == 4294967295L)

        object.UInt32Field = -0
        self.failUnless(object.UInt32Field == 0)

        object.UInt32Field = System.UInt32(4294967295L)
        self.failUnless(object.UInt32Field == 4294967295L)

        object.UInt32Field = System.UInt32(0)
        self.failUnless(object.UInt32Field == 0)

        def test():
            ConversionTest().UInt32Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt32Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt32Field = 4294967296L
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().UInt32Field = -1
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt32(4294967296L)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt32(-1)
            
        self.failUnlessRaises(OverflowError, test)


    def testUInt64Conversion(self):
        """Test uint64 conversion."""
        self.failUnless(System.UInt64.MaxValue == 18446744073709551615L)
        self.failUnless(System.UInt64.MinValue == 0)

        object = ConversionTest()
        self.failUnless(object.UInt64Field == 0)

        object.UInt64Field = 18446744073709551615L
        self.failUnless(object.UInt64Field == 18446744073709551615L)

        object.UInt64Field = -0
        self.failUnless(object.UInt64Field == 0)

        object.UInt64Field = System.UInt64(18446744073709551615L)
        self.failUnless(object.UInt64Field == 18446744073709551615L)

        object.UInt64Field = System.UInt64(0)
        self.failUnless(object.UInt64Field == 0)

        def test():
            ConversionTest().UInt64Field = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt64Field = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().UInt64Field = 18446744073709551616L
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().UInt64Field = -1
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt64(18446744073709551616L)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.UInt64(-1)
            
        self.failUnlessRaises(OverflowError, test)


    def testSingleConversion(self):
        """Test single conversion."""
        self.failUnless(System.Single.MaxValue == 3.402823e38)
        self.failUnless(System.Single.MinValue == -3.402823e38)

        object = ConversionTest()
        self.failUnless(object.SingleField == 0.0)

        object.SingleField = 3.402823e38
        self.failUnless(object.SingleField == 3.402823e38)

        object.SingleField = -3.402823e38
        self.failUnless(object.SingleField == -3.402823e38)

        object.SingleField = System.Single(3.402823e38)
        self.failUnless(object.SingleField == 3.402823e38)

        object.SingleField = System.Single(-3.402823e38)
        self.failUnless(object.SingleField == -3.402823e38)

        def test():
            ConversionTest().SingleField = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().SingleField = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().SingleField = 3.402824e38
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().SingleField = -3.402824e38
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Single(3.402824e38)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Single(-3.402824e38)
            
        self.failUnlessRaises(OverflowError, test)


    def testDoubleConversion(self):
        """Test double conversion."""
        self.failUnless(System.Double.MaxValue == 1.7976931348623157e308)
        self.failUnless(System.Double.MinValue == -1.7976931348623157e308)

        object = ConversionTest()
        self.failUnless(object.DoubleField == 0.0)

        object.DoubleField = 1.7976931348623157e308
        self.failUnless(object.DoubleField == 1.7976931348623157e308)

        object.DoubleField = -1.7976931348623157e308
        self.failUnless(object.DoubleField == -1.7976931348623157e308)

        object.DoubleField = System.Double(1.7976931348623157e308)
        self.failUnless(object.DoubleField == 1.7976931348623157e308)

        object.DoubleField = System.Double(-1.7976931348623157e308)
        self.failUnless(object.DoubleField == -1.7976931348623157e308)

        def test():
            ConversionTest().DoubleField = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().DoubleField = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().DoubleField = 1.7976931348623159e308
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            ConversionTest().DoubleField = -1.7976931348623159e308
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Double(1.7976931348623159e308)
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            value = System.Double(-1.7976931348623159e308)
            
        self.failUnlessRaises(OverflowError, test)


    def testDecimalConversion(self):
        """Test decimal conversion."""
        from System import Decimal

        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")
        
        self.failUnless(Decimal.ToInt64(Decimal(10)) == 10L)

        object = ConversionTest()
        self.failUnless(object.DecimalField == Decimal(0))

        object.DecimalField = Decimal(10)
        self.failUnless(object.DecimalField == Decimal(10))

        object.DecimalField = Decimal.One
        self.failUnless(object.DecimalField == Decimal.One)

        object.DecimalField = Decimal.Zero
        self.failUnless(object.DecimalField == Decimal.Zero)

        object.DecimalField = max_d
        self.failUnless(object.DecimalField == max_d)

        object.DecimalField = min_d
        self.failUnless(object.DecimalField == min_d)

        def test():
            ConversionTest().DecimalField = None
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().DecimalField = "spam"
            
        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().DecimalField = 1
            
        self.failUnlessRaises(TypeError, test)


    def testStringConversion(self):
        """Test string / unicode conversion."""
        object = ConversionTest()

        self.failUnless(object.StringField == "spam")
        self.failUnless(object.StringField == u"spam")

        object.StringField = "eggs"
        self.failUnless(object.StringField == "eggs")
        self.failUnless(object.StringField == u"eggs")

        object.StringField = u"spam"
        self.failUnless(object.StringField == "spam")
        self.failUnless(object.StringField == u"spam")

        object.StringField = u'\uffff\uffff'
        self.failUnless(object.StringField == u'\uffff\uffff')

        object.StringField = System.String("spam")
        self.failUnless(object.StringField == "spam")
        self.failUnless(object.StringField == u"spam")

        object.StringField = System.String(u'\uffff\uffff')
        self.failUnless(object.StringField == u'\uffff\uffff')

        object.StringField = None
        self.failUnless(object.StringField == None)

        def test():
            ConversionTest().StringField = 1
            
        self.failUnlessRaises(TypeError, test)


    def testInterfaceConversion(self):
        """Test interface conversion."""
        from Python.Test import Spam, ISpam

        object = ConversionTest()

        self.failUnless(ISpam(object.SpamField).GetValue() == "spam")
        self.failUnless(object.SpamField.GetValue() == "spam")
        
        object.SpamField = Spam("eggs")
        self.failUnless(ISpam(object.SpamField).GetValue() == "eggs")
        self.failUnless(object.SpamField.GetValue() == "eggs")

        # need to test spam subclass here.

        object.SpamField = None
        self.failUnless(object.SpamField == None)

        def test():
            object = ConversionTest()
            object.SpamField = System.String("bad")

        self.failUnlessRaises(TypeError, test)

        def test():
            object = ConversionTest()
            object.SpamField = System.Int32(1)

        self.failUnlessRaises(TypeError, test)


    def testObjectConversion(self):
        """Test object conversion."""
        from Python.Test import Spam

        object = ConversionTest()
        self.failUnless(object.ObjectField == None)

        object.ObjectField = Spam("eggs")
        self.failUnless(object.ObjectField.__class__.__name__ == "Spam")
        self.failUnless(object.ObjectField.GetValue() == "eggs")

        object.ObjectField = None
        self.failUnless(object.ObjectField == None)

        object.ObjectField = System.String("spam")
        self.failUnless(object.ObjectField == "spam")

        object.ObjectField = System.Int32(1)
        self.failUnless(object.ObjectField == 1)

        # need to test subclass here

        def test():
            object = ConversionTest()
            object.ObjectField = self

        self.failUnlessRaises(TypeError, test)


    def testEnumConversion(self):
        """Test enum conversion."""
        from Python.Test import ShortEnum

        object = ConversionTest()
        self.failUnless(object.EnumField == ShortEnum.Zero)

        object.EnumField = ShortEnum.One
        self.failUnless(object.EnumField == ShortEnum.One)

        object.EnumField = 0
        self.failUnless(object.EnumField == ShortEnum.Zero)
        self.failUnless(object.EnumField == 0)

        object.EnumField = 1
        self.failUnless(object.EnumField == ShortEnum.One)
        self.failUnless(object.EnumField == 1)

        def test():
            object = ConversionTest()
            object.EnumField = 10

        self.failUnlessRaises(ValueError, test)

        def test():
            object = ConversionTest()
            object.EnumField = 255

        self.failUnlessRaises(ValueError, test)

        def test():
            object = ConversionTest()
            object.EnumField = 1000000

        self.failUnlessRaises(OverflowError, test)

        def test():
            object = ConversionTest()
            object.EnumField = "spam"

        self.failUnlessRaises(TypeError, test)


    def testNullConversion(self):
        """Test null conversion."""
        object = ConversionTest()

        object.StringField = None
        self.failUnless(object.StringField == None)

        object.ObjectField = None
        self.failUnless(object.ObjectField == None)

        object.SpamField = None
        self.failUnless(object.SpamField == None)

        # Primitive types and enums should not be set to null.

        def test():
            ConversionTest().Int32Field = None

        self.failUnlessRaises(TypeError, test)

        def test():
            ConversionTest().EnumField = None

        self.failUnlessRaises(TypeError, test)


    def testByteArrayConversion(self):
        """Test byte array conversion."""
        object = ConversionTest()

        self.failUnless(object.ByteArrayField == None)

        object.ByteArrayField = [0, 1, 2 , 3, 4]
        array = object.ByteArrayField
        self.failUnless(len(array) == 5)
        self.failUnless(array[0] == 0)
        self.failUnless(array[4] == 4)

        value = "testing"
        object.ByteArrayField = value
        array = object.ByteArrayField
        for i in range(len(value)):
            self.failUnless(array[i] == ord(value[i]))


    def testSByteArrayConversion(self):
        """Test sbyte array conversion."""
        object = ConversionTest()

        self.failUnless(object.SByteArrayField == None)

        object.SByteArrayField = [0, 1, 2 , 3, 4]
        array = object.SByteArrayField
        self.failUnless(len(array) == 5)
        self.failUnless(array[0] == 0)
        self.failUnless(array[4] == 4)

        value = "testing"
        object.SByteArrayField = value
        array = object.SByteArrayField
        for i in range(len(value)):
            self.failUnless(array[i] == ord(value[i]))









def test_suite():
    return unittest.makeSuite(ConversionTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

