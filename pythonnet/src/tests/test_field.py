# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from Python.Test import FieldTest
from Python.Test import ShortEnum
import System


class FieldTests(unittest.TestCase):
    """Test CLR field support."""

    def testPublicInstanceField(self):
        """Test public instance fields."""
        object = FieldTest();
        self.failUnless(object.PublicField == 0)

        object.PublicField = 1
        self.failUnless(object.PublicField == 1)

        def test():
            del FieldTest().PublicField

        self.failUnlessRaises(TypeError, test)


    def testPublicStaticField(self):
        """Test public static fields."""
        object = FieldTest();
        self.failUnless(FieldTest.PublicStaticField == 0)

        FieldTest.PublicStaticField = 1
        self.failUnless(FieldTest.PublicStaticField == 1)

        self.failUnless(object.PublicStaticField == 1)
        object.PublicStaticField = 0
        self.failUnless(object.PublicStaticField == 0)

        def test():
            del FieldTest.PublicStaticField

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest().PublicStaticField

        self.failUnlessRaises(TypeError, test)


    def testProtectedInstanceField(self):
        """Test protected instance fields."""
        object = FieldTest();
        self.failUnless(object.ProtectedField == 0)

        object.ProtectedField = 1
        self.failUnless(object.ProtectedField == 1)

        def test():
            del FieldTest().ProtectedField

        self.failUnlessRaises(TypeError, test)


    def testProtectedStaticField(self):
        """Test protected static fields."""
        object = FieldTest();
        self.failUnless(FieldTest.ProtectedStaticField == 0)

        FieldTest.ProtectedStaticField = 1
        self.failUnless(FieldTest.ProtectedStaticField == 1)

        self.failUnless(object.ProtectedStaticField == 1)
        object.ProtectedStaticField = 0
        self.failUnless(object.ProtectedStaticField == 0)

        def test():
            del FieldTest.ProtectedStaticField

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest().ProtectedStaticField

        self.failUnlessRaises(TypeError, test)


    def testReadOnlyInstanceField(self):
        """Test readonly instance fields."""
        self.failUnless(FieldTest().ReadOnlyField == 0)

        def test():
            FieldTest().ReadOnlyField = 1

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest().ReadOnlyField

        self.failUnlessRaises(TypeError, test)


    def testReadOnlyStaticField(self):
        """Test readonly static fields."""
        object = FieldTest();

        self.failUnless(FieldTest.ReadOnlyStaticField == 0)
        self.failUnless(object.ReadOnlyStaticField == 0)

        def test():
            FieldTest.ReadOnlyStaticField = 1

        self.failUnlessRaises(TypeError, test)

        def test():
            FieldTest().ReadOnlyStaticField = 1

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest.ReadOnlyStaticField

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest().ReadOnlyStaticField

        self.failUnlessRaises(TypeError, test)


    def testConstantField(self):
        """Test const fields."""
        object = FieldTest();

        self.failUnless(FieldTest.ConstField == 0)
        self.failUnless(object.ConstField == 0)

        def test():
            FieldTest().ConstField = 1

        self.failUnlessRaises(TypeError, test)

        def test():
            FieldTest.ConstField = 1

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest().ConstField

        self.failUnlessRaises(TypeError, test)

        def test():
            del FieldTest.ConstField

        self.failUnlessRaises(TypeError, test)


    def testInternalField(self):
        """Test internal fields."""

        def test():
            f = FieldTest().InternalField

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = FieldTest().InternalStaticField

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = FieldTest.InternalStaticField

        self.failUnlessRaises(AttributeError, test)


    def testPrivateField(self):
        """Test private fields."""

        def test():
            f = FieldTest().PrivateField

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = FieldTest().PrivateStaticField

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = FieldTest.PrivateStaticField

        self.failUnlessRaises(AttributeError, test)


    def testFieldDescriptorGetSet(self):
        """Test field descriptor get / set."""

        # This test ensures that setting an attribute implemented with
        # a descriptor actually goes through the descriptor (rather than
        # silently replacing the descriptor in the instance or type dict.

        object = FieldTest()

        self.failUnless(FieldTest.PublicStaticField == 0)
        self.failUnless(object.PublicStaticField == 0)

        descriptor = FieldTest.__dict__['PublicStaticField']
        self.failUnless(type(descriptor) != types.IntType)

        object.PublicStaticField = 0
        descriptor = FieldTest.__dict__['PublicStaticField']
        self.failUnless(type(descriptor) != types.IntType)

        FieldTest.PublicStaticField = 0
        descriptor = FieldTest.__dict__['PublicStaticField']
        self.failUnless(type(descriptor) != types.IntType)


    def testFieldDescriptorWrongType(self):
        """Test setting a field using a value of the wrong type."""
        def test():
            FieldTest().PublicField = "spam"

        self.failUnlessRaises(TypeError, test)


    def testFieldDescriptorAbuse(self):
        """Test field descriptor abuse."""
        desc = FieldTest.__dict__['PublicField']

        def test():
            desc.__get__(0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.failUnlessRaises(TypeError, test)


    def testBooleanField(self):
        """Test boolean fields."""
        # change this to true / false later for Python 2.3?
        object = FieldTest()
        self.failUnless(object.BooleanField == False)

        object.BooleanField = True
        self.failUnless(object.BooleanField == True)

        object.BooleanField = False
        self.failUnless(object.BooleanField == False)

        object.BooleanField = 1
        self.failUnless(object.BooleanField == True)

        object.BooleanField = 0
        self.failUnless(object.BooleanField == False)


    def testSByteField(self):
        """Test sbyte fields."""
        object = FieldTest()
        self.failUnless(object.SByteField == 0)

        object.SByteField = 1
        self.failUnless(object.SByteField == 1)


    def testByteField(self):
        """Test byte fields."""
        object = FieldTest()
        self.failUnless(object.ByteField == 0)

        object.ByteField = 1
        self.failUnless(object.ByteField == 1)


    def testCharField(self):
        """Test char fields."""
        object = FieldTest()
        self.failUnless(object.CharField == u'A')
        self.failUnless(object.CharField == 'A')

        object.CharField = 'B'
        self.failUnless(object.CharField == u'B')
        self.failUnless(object.CharField ==  'B')

        object.CharField = u'C'
        self.failUnless(object.CharField == u'C')
        self.failUnless(object.CharField == 'C')


    def testInt16Field(self):
        """Test int16 fields."""
        object = FieldTest()
        self.failUnless(object.Int16Field == 0)

        object.Int16Field = 1
        self.failUnless(object.Int16Field == 1)


    def testInt32Field(self):
        """Test int32 fields."""
        object = FieldTest()
        self.failUnless(object.Int32Field == 0)

        object.Int32Field = 1
        self.failUnless(object.Int32Field == 1)


    def testInt64Field(self):
        """Test int64 fields."""
        object = FieldTest()
        self.failUnless(object.Int64Field == 0)

        object.Int64Field = 1
        self.failUnless(object.Int64Field == 1)


    def testUInt16Field(self):
        """Test uint16 fields."""
        object = FieldTest()
        self.failUnless(object.UInt16Field == 0)

        object.UInt16Field = 1
        self.failUnless(object.UInt16Field == 1)


    def testUInt32Field(self):
        """Test uint32 fields."""
        object = FieldTest()
        self.failUnless(object.UInt32Field == 0)

        object.UInt32Field = 1
        self.failUnless(object.UInt32Field == 1)


    def testUInt64Field(self):
        """Test uint64 fields."""
        object = FieldTest()
        self.failUnless(object.UInt64Field == 0)

        object.UInt64Field = 1
        self.failUnless(object.UInt64Field == 1)


    def testSingleField(self):
        """Test single fields."""
        object = FieldTest()
        self.failUnless(object.SingleField == 0.0)

        object.SingleField = 1.1
        self.failUnless(object.SingleField == 1.1)


    def testDoubleField(self):
        """Test double fields."""
        object = FieldTest()
        self.failUnless(object.DoubleField == 0.0)

        object.DoubleField = 1.1
        self.failUnless(object.DoubleField == 1.1)


    def testDecimalField(self):
        """Test decimal fields."""
        object = FieldTest()
        self.failUnless(object.DecimalField == System.Decimal(0))

        object.DecimalField = System.Decimal(1)
        self.failUnless(object.DecimalField == System.Decimal(1))


    def testStringField(self):
        """Test string fields."""
        object = FieldTest()
        self.failUnless(object.StringField == "spam")

        object.StringField = "eggs"
        self.failUnless(object.StringField == "eggs")        


    def testInterfaceField(self):
        """Test interface fields."""
        from Python.Test import Spam, ISpam

        object = FieldTest()
        
        self.failUnless(ISpam(object.SpamField).GetValue() == "spam")
        self.failUnless(object.SpamField.GetValue() == "spam")

        object.SpamField = Spam("eggs")
        self.failUnless(ISpam(object.SpamField).GetValue() == "eggs")
        self.failUnless(object.SpamField.GetValue() == "eggs")


    def testObjectField(self):
        """Test object fields."""
        object = FieldTest()
        self.failUnless(object.ObjectField == None)

        object.ObjectField = System.String("spam")
        self.failUnless(object.ObjectField == "spam")

        object.ObjectField = System.Int32(1)
        self.failUnless(object.ObjectField == 1)

        object.ObjectField = None
        self.failUnless(object.ObjectField == None)


    def testEnumField(self):
        """Test enum fields."""
        object = FieldTest()
        self.failUnless(object.EnumField == ShortEnum.Zero)

        object.EnumField = ShortEnum.One
        self.failUnless(object.EnumField == ShortEnum.One)


    def testNullableField(self):
        """Test nullable fields."""
        object = FieldTest()

        object.StringField = None
        self.failUnless(object.StringField == None)

        object.ObjectField = None
        self.failUnless(object.ObjectField == None)

        object.SpamField = None
        self.failUnless(object.SpamField == None)

        # Primitive types and enums should not be set to null.

        def test():
            FieldTest().Int32Field = None

        self.failUnlessRaises(TypeError, test)

        def test():
            FieldTest().EnumField = None

        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(FieldTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

