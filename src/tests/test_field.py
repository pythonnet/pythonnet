import sys, os, string, unittest, types
from Python.Test import FieldTest
from Python.Test import ShortEnum
import System
import six

if six.PY3:
    IntType = int
else:
    IntType = types.IntType


class FieldTests(unittest.TestCase):
    """Test CLR field support."""

    def testPublicInstanceField(self):
        """Test public instance fields."""
        object = FieldTest();
        self.assertTrue(object.PublicField == 0)

        object.PublicField = 1
        self.assertTrue(object.PublicField == 1)

        def test():
            del FieldTest().PublicField

        self.assertRaises(TypeError, test)

    def testPublicStaticField(self):
        """Test public static fields."""
        object = FieldTest();
        self.assertTrue(FieldTest.PublicStaticField == 0)

        FieldTest.PublicStaticField = 1
        self.assertTrue(FieldTest.PublicStaticField == 1)

        self.assertTrue(object.PublicStaticField == 1)
        object.PublicStaticField = 0
        self.assertTrue(object.PublicStaticField == 0)

        def test():
            del FieldTest.PublicStaticField

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest().PublicStaticField

        self.assertRaises(TypeError, test)

    def testProtectedInstanceField(self):
        """Test protected instance fields."""
        object = FieldTest();
        self.assertTrue(object.ProtectedField == 0)

        object.ProtectedField = 1
        self.assertTrue(object.ProtectedField == 1)

        def test():
            del FieldTest().ProtectedField

        self.assertRaises(TypeError, test)

    def testProtectedStaticField(self):
        """Test protected static fields."""
        object = FieldTest();
        self.assertTrue(FieldTest.ProtectedStaticField == 0)

        FieldTest.ProtectedStaticField = 1
        self.assertTrue(FieldTest.ProtectedStaticField == 1)

        self.assertTrue(object.ProtectedStaticField == 1)
        object.ProtectedStaticField = 0
        self.assertTrue(object.ProtectedStaticField == 0)

        def test():
            del FieldTest.ProtectedStaticField

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest().ProtectedStaticField

        self.assertRaises(TypeError, test)

    def testReadOnlyInstanceField(self):
        """Test readonly instance fields."""
        self.assertTrue(FieldTest().ReadOnlyField == 0)

        def test():
            FieldTest().ReadOnlyField = 1

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest().ReadOnlyField

        self.assertRaises(TypeError, test)

    def testReadOnlyStaticField(self):
        """Test readonly static fields."""
        object = FieldTest();

        self.assertTrue(FieldTest.ReadOnlyStaticField == 0)
        self.assertTrue(object.ReadOnlyStaticField == 0)

        def test():
            FieldTest.ReadOnlyStaticField = 1

        self.assertRaises(TypeError, test)

        def test():
            FieldTest().ReadOnlyStaticField = 1

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest.ReadOnlyStaticField

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest().ReadOnlyStaticField

        self.assertRaises(TypeError, test)

    def testConstantField(self):
        """Test const fields."""
        object = FieldTest();

        self.assertTrue(FieldTest.ConstField == 0)
        self.assertTrue(object.ConstField == 0)

        def test():
            FieldTest().ConstField = 1

        self.assertRaises(TypeError, test)

        def test():
            FieldTest.ConstField = 1

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest().ConstField

        self.assertRaises(TypeError, test)

        def test():
            del FieldTest.ConstField

        self.assertRaises(TypeError, test)

    def testInternalField(self):
        """Test internal fields."""

        def test():
            f = FieldTest().InternalField

        self.assertRaises(AttributeError, test)

        def test():
            f = FieldTest().InternalStaticField

        self.assertRaises(AttributeError, test)

        def test():
            f = FieldTest.InternalStaticField

        self.assertRaises(AttributeError, test)

    def testPrivateField(self):
        """Test private fields."""

        def test():
            f = FieldTest().PrivateField

        self.assertRaises(AttributeError, test)

        def test():
            f = FieldTest().PrivateStaticField

        self.assertRaises(AttributeError, test)

        def test():
            f = FieldTest.PrivateStaticField

        self.assertRaises(AttributeError, test)

    def testFieldDescriptorGetSet(self):
        """Test field descriptor get / set."""

        # This test ensures that setting an attribute implemented with
        # a descriptor actually goes through the descriptor (rather than
        # silently replacing the descriptor in the instance or type dict.

        object = FieldTest()

        self.assertTrue(FieldTest.PublicStaticField == 0)
        self.assertTrue(object.PublicStaticField == 0)

        descriptor = FieldTest.__dict__['PublicStaticField']
        self.assertTrue(type(descriptor) != IntType)

        object.PublicStaticField = 0
        descriptor = FieldTest.__dict__['PublicStaticField']
        self.assertTrue(type(descriptor) != IntType)

        FieldTest.PublicStaticField = 0
        descriptor = FieldTest.__dict__['PublicStaticField']
        self.assertTrue(type(descriptor) != IntType)

    def testFieldDescriptorWrongType(self):
        """Test setting a field using a value of the wrong type."""

        def test():
            FieldTest().PublicField = "spam"

        self.assertRaises(TypeError, test)

    def testFieldDescriptorAbuse(self):
        """Test field descriptor abuse."""
        desc = FieldTest.__dict__['PublicField']

        def test():
            desc.__get__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.assertRaises(TypeError, test)

    def testBooleanField(self):
        """Test boolean fields."""
        # change this to true / false later for Python 2.3?
        object = FieldTest()
        self.assertTrue(object.BooleanField == False)

        object.BooleanField = True
        self.assertTrue(object.BooleanField == True)

        object.BooleanField = False
        self.assertTrue(object.BooleanField == False)

        object.BooleanField = 1
        self.assertTrue(object.BooleanField == True)

        object.BooleanField = 0
        self.assertTrue(object.BooleanField == False)

    def testSByteField(self):
        """Test sbyte fields."""
        object = FieldTest()
        self.assertTrue(object.SByteField == 0)

        object.SByteField = 1
        self.assertTrue(object.SByteField == 1)

    def testByteField(self):
        """Test byte fields."""
        object = FieldTest()
        self.assertTrue(object.ByteField == 0)

        object.ByteField = 1
        self.assertTrue(object.ByteField == 1)

    def testCharField(self):
        """Test char fields."""
        object = FieldTest()
        self.assertTrue(object.CharField == six.u('A'))
        self.assertTrue(object.CharField == 'A')

        object.CharField = 'B'
        self.assertTrue(object.CharField == six.u('B'))
        self.assertTrue(object.CharField == 'B')

        object.CharField = six.u('C')
        self.assertTrue(object.CharField == six.u('C'))
        self.assertTrue(object.CharField == 'C')

    def testInt16Field(self):
        """Test int16 fields."""
        object = FieldTest()
        self.assertTrue(object.Int16Field == 0)

        object.Int16Field = 1
        self.assertTrue(object.Int16Field == 1)

    def testInt32Field(self):
        """Test int32 fields."""
        object = FieldTest()
        self.assertTrue(object.Int32Field == 0)

        object.Int32Field = 1
        self.assertTrue(object.Int32Field == 1)

    def testInt64Field(self):
        """Test int64 fields."""
        object = FieldTest()
        self.assertTrue(object.Int64Field == 0)

        object.Int64Field = 1
        self.assertTrue(object.Int64Field == 1)

    def testUInt16Field(self):
        """Test uint16 fields."""
        object = FieldTest()
        self.assertTrue(object.UInt16Field == 0)

        object.UInt16Field = 1
        self.assertTrue(object.UInt16Field == 1)

    def testUInt32Field(self):
        """Test uint32 fields."""
        object = FieldTest()
        self.assertTrue(object.UInt32Field == 0)

        object.UInt32Field = 1
        self.assertTrue(object.UInt32Field == 1)

    def testUInt64Field(self):
        """Test uint64 fields."""
        object = FieldTest()
        self.assertTrue(object.UInt64Field == 0)

        object.UInt64Field = 1
        self.assertTrue(object.UInt64Field == 1)

    def testSingleField(self):
        """Test single fields."""
        object = FieldTest()
        self.assertTrue(object.SingleField == 0.0)

        object.SingleField = 1.1
        self.assertTrue(object.SingleField == 1.1)

    def testDoubleField(self):
        """Test double fields."""
        object = FieldTest()
        self.assertTrue(object.DoubleField == 0.0)

        object.DoubleField = 1.1
        self.assertTrue(object.DoubleField == 1.1)

    def testDecimalField(self):
        """Test decimal fields."""
        object = FieldTest()
        self.assertTrue(object.DecimalField == System.Decimal(0))

        object.DecimalField = System.Decimal(1)
        self.assertTrue(object.DecimalField == System.Decimal(1))

    def testStringField(self):
        """Test string fields."""
        object = FieldTest()
        self.assertTrue(object.StringField == "spam")

        object.StringField = "eggs"
        self.assertTrue(object.StringField == "eggs")

    def testInterfaceField(self):
        """Test interface fields."""
        from Python.Test import Spam, ISpam

        object = FieldTest()

        self.assertTrue(ISpam(object.SpamField).GetValue() == "spam")
        self.assertTrue(object.SpamField.GetValue() == "spam")

        object.SpamField = Spam("eggs")
        self.assertTrue(ISpam(object.SpamField).GetValue() == "eggs")
        self.assertTrue(object.SpamField.GetValue() == "eggs")

    def testObjectField(self):
        """Test object fields."""
        object = FieldTest()
        self.assertTrue(object.ObjectField == None)

        object.ObjectField = System.String("spam")
        self.assertTrue(object.ObjectField == "spam")

        object.ObjectField = System.Int32(1)
        self.assertTrue(object.ObjectField == 1)

        object.ObjectField = None
        self.assertTrue(object.ObjectField == None)

    def testEnumField(self):
        """Test enum fields."""
        object = FieldTest()
        self.assertTrue(object.EnumField == ShortEnum.Zero)

        object.EnumField = ShortEnum.One
        self.assertTrue(object.EnumField == ShortEnum.One)

    def testNullableField(self):
        """Test nullable fields."""
        object = FieldTest()

        object.StringField = None
        self.assertTrue(object.StringField == None)

        object.ObjectField = None
        self.assertTrue(object.ObjectField == None)

        object.SpamField = None
        self.assertTrue(object.SpamField == None)

        # Primitive types and enums should not be set to null.

        def test():
            FieldTest().Int32Field = None

        self.assertRaises(TypeError, test)

        def test():
            FieldTest().EnumField = None

        self.assertRaises(TypeError, test)


def test_suite():
    return unittest.makeSuite(FieldTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
