# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from System import DayOfWeek
from Python import Test


class EnumTests(unittest.TestCase):
    """Test CLR enum support."""

    def testEnumStandardAttrs(self):
        """Test standard enum attributes."""
        self.failUnless(DayOfWeek.__name__ == 'DayOfWeek')
        self.failUnless(DayOfWeek.__module__ == 'System')
        self.failUnless(type(DayOfWeek.__dict__) == types.DictProxyType)
        self.failUnless(DayOfWeek.__doc__ == '')


    def testEnumGetMember(self):
        """Test access to enum members."""
        self.failUnless(DayOfWeek.Sunday == 0)
        self.failUnless(DayOfWeek.Monday == 1)
        self.failUnless(DayOfWeek.Tuesday == 2)
        self.failUnless(DayOfWeek.Wednesday == 3)
        self.failUnless(DayOfWeek.Thursday == 4)
        self.failUnless(DayOfWeek.Friday == 5)
        self.failUnless(DayOfWeek.Saturday == 6)


    def testByteEnum(self):
        """Test byte enum."""
        self.failUnless(Test.ByteEnum.Zero == 0)
        self.failUnless(Test.ByteEnum.One == 1)
        self.failUnless(Test.ByteEnum.Two == 2)


    def testSByteEnum(self):
        """Test sbyte enum."""
        self.failUnless(Test.SByteEnum.Zero == 0)
        self.failUnless(Test.SByteEnum.One == 1)
        self.failUnless(Test.SByteEnum.Two == 2)


    def testShortEnum(self):
        """Test short enum."""
        self.failUnless(Test.ShortEnum.Zero == 0)
        self.failUnless(Test.ShortEnum.One == 1)
        self.failUnless(Test.ShortEnum.Two == 2)


    def testUShortEnum(self):
        """Test ushort enum."""
        self.failUnless(Test.UShortEnum.Zero == 0)
        self.failUnless(Test.UShortEnum.One == 1)
        self.failUnless(Test.UShortEnum.Two == 2)


    def testIntEnum(self):
        """Test int enum."""
        self.failUnless(Test.IntEnum.Zero == 0)
        self.failUnless(Test.IntEnum.One == 1)
        self.failUnless(Test.IntEnum.Two == 2)


    def testUIntEnum(self):
        """Test uint enum."""
        self.failUnless(Test.UIntEnum.Zero == 0L)
        self.failUnless(Test.UIntEnum.One == 1L)
        self.failUnless(Test.UIntEnum.Two == 2L)


    def testLongEnum(self):
        """Test long enum."""
        self.failUnless(Test.LongEnum.Zero == 0L)
        self.failUnless(Test.LongEnum.One == 1L)
        self.failUnless(Test.LongEnum.Two == 2L)


    def testULongEnum(self):
        """Test ulong enum."""
        self.failUnless(Test.ULongEnum.Zero == 0L)
        self.failUnless(Test.ULongEnum.One == 1L)
        self.failUnless(Test.ULongEnum.Two == 2L)


    def testInstantiateEnumFails(self):
        """Test that instantiation of an enum class fails."""
        def test():
            ob = DayOfWeek()

        self.failUnlessRaises(TypeError, test)


    def testSubclassEnumFails(self):
        """Test that subclassing of an enumeration fails."""
        def test():
            class Boom(DayOfWeek):
                pass

        self.failUnlessRaises(TypeError, test)


    def testEnumSetMemberFails(self):
        """Test that setattr operations on enumerations fail."""
        def test():
            DayOfWeek.Sunday = 13
            
        self.failUnlessRaises(TypeError, test)

        def test():
            del DayOfWeek.Sunday

        self.failUnlessRaises(TypeError, test)


    def testEnumWithFlagsAttrConversion(self):
        """Test enumeration conversion with FlagsAttribute set."""
        from System.Windows.Forms import Label

        # This works because the AnchorStyles enum has FlagsAttribute.
        label = Label()
        label.Anchor = 99

        # This should fail because our test enum doesn't have it.
        def test():
            Test.FieldTest().EnumField = 99
            
        self.failUnlessRaises(ValueError, test)


    def testEnumConversion(self):
        """Test enumeration conversion."""
        object = Test.FieldTest()
        self.failUnless(object.EnumField == 0)

        object.EnumField = Test.ShortEnum.One
        self.failUnless(object.EnumField == 1)

        def test():
            Test.FieldTest().EnumField = 20
            
        self.failUnlessRaises(ValueError, test)

        def test():
            Test.FieldTest().EnumField = 100000
            
        self.failUnlessRaises(OverflowError, test)

        def test():
            Test.FieldTest().EnumField = "str"
            
        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(EnumTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

