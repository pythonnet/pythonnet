import sys, os, string, unittest, types
import clr

clr.AddReference("Python.Test")
import Python.Test as Test
import six

if six.PY3:
    long = int
    unichr = chr


class IndexerTests(unittest.TestCase):
    """Test support for indexer properties."""

    def testPublicIndexer(self):
        """Test public indexers."""
        object = Test.PublicIndexerTest()

        object[0] = "zero"
        self.assertTrue(object[0] == "zero")

        object[1] = "one"
        self.assertTrue(object[1] == "one")

        self.assertTrue(object[10] == None)

    def testProtectedIndexer(self):
        """Test protected indexers."""
        object = Test.ProtectedIndexerTest()

        object[0] = "zero"
        self.assertTrue(object[0] == "zero")

        object[1] = "one"
        self.assertTrue(object[1] == "one")

        self.assertTrue(object[10] == None)

    def testInternalIndexer(self):
        """Test internal indexers."""
        object = Test.InternalIndexerTest()

        def test():
            object[0] = "zero"

        self.assertRaises(TypeError, test)

        def test():
            Test.InternalIndexerTest.__getitem__(object, 0)

        self.assertRaises(TypeError, test)

        def test():
            object.__getitem__(0)

        self.assertRaises(TypeError, test)

    def testPrivateIndexer(self):
        """Test private indexers."""
        object = Test.PrivateIndexerTest()

        def test():
            object[0] = "zero"

        self.assertRaises(TypeError, test)

        def test():
            Test.PrivateIndexerTest.__getitem__(object, 0)

        self.assertRaises(TypeError, test)

        def test():
            object.__getitem__(0)

        self.assertRaises(TypeError, test)

    def testBooleanIndexer(self):
        """Test boolean indexers."""
        object = Test.BooleanIndexerTest()

        self.assertTrue(object[True] == None)
        self.assertTrue(object[1] == None)

        object[0] = "false"
        self.assertTrue(object[0] == "false")

        object[1] = "true"
        self.assertTrue(object[1] == "true")

        object[False] = "false"
        self.assertTrue(object[False] == "false")

        object[True] = "true"
        self.assertTrue(object[True] == "true")

    def testByteIndexer(self):
        """Test byte indexers."""
        object = Test.ByteIndexerTest()
        max = 255
        min = 0

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.ByteIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.ByteIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testSByteIndexer(self):
        """Test sbyte indexers."""
        object = Test.SByteIndexerTest()
        max = 127
        min = -128

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.SByteIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.SByteIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testCharIndexer(self):
        """Test char indexers."""
        object = Test.CharIndexerTest()
        max = unichr(65535)
        min = unichr(0)

        self.assertTrue(object[max] == None)

        object[max] = "max"
        self.assertTrue(object[max] == "max")

        object[min] = "min"
        self.assertTrue(object[min] == "min")

        def test():
            object = Test.CharIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.CharIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt16Indexer(self):
        """Test Int16 indexers."""
        object = Test.Int16IndexerTest()
        max = 32767
        min = -32768

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.Int16IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int16IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt32Indexer(self):
        """Test Int32 indexers."""
        object = Test.Int32IndexerTest()
        max = 2147483647
        min = -2147483648

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.Int32IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int32IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testInt64Indexer(self):
        """Test Int64 indexers."""
        object = Test.Int64IndexerTest()
        max = long(9223372036854775807)
        min = long(-9223372036854775808)

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.Int64IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.Int64IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt16Indexer(self):
        """Test UInt16 indexers."""
        object = Test.UInt16IndexerTest()
        max = 65535
        min = 0

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.UInt16IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt16IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt32Indexer(self):
        """Test UInt32 indexers."""
        object = Test.UInt32IndexerTest()
        max = long(4294967295)
        min = 0

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.UInt32IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt32IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testUInt64Indexer(self):
        """Test UInt64 indexers."""
        object = Test.UInt64IndexerTest()
        max = long(18446744073709551615)
        min = 0

        self.assertTrue(object[max] == None)

        object[max] = str(max)
        self.assertTrue(object[max] == str(max))

        object[min] = str(min)
        self.assertTrue(object[min] == str(min))

        def test():
            object = Test.UInt64IndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.UInt64IndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testSingleIndexer(self):
        """Test Single indexers."""
        object = Test.SingleIndexerTest()
        max = 3.402823e38
        min = -3.402823e38

        self.assertTrue(object[max] == None)

        object[max] = "max"
        self.assertTrue(object[max] == "max")

        object[min] = "min"
        self.assertTrue(object[min] == "min")

        def test():
            object = Test.SingleIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.SingleIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testDoubleIndexer(self):
        """Test Double indexers."""
        object = Test.DoubleIndexerTest()
        max = 1.7976931348623157e308
        min = -1.7976931348623157e308

        self.assertTrue(object[max] == None)

        object[max] = "max"
        self.assertTrue(object[max] == "max")

        object[min] = "min"
        self.assertTrue(object[min] == "min")

        def test():
            object = Test.DoubleIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.DoubleIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testDecimalIndexer(self):
        """Test Decimal indexers."""
        object = Test.DecimalIndexerTest()

        from System import Decimal
        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.assertTrue(object[max_d] == None)

        object[max_d] = "max"
        self.assertTrue(object[max_d] == "max")

        object[min_d] = "min"
        self.assertTrue(object[min_d] == "min")

        def test():
            object = Test.DecimalIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.DecimalIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testStringIndexer(self):
        """Test String indexers."""
        object = Test.StringIndexerTest()

        self.assertTrue(object["spam"] == None)
        self.assertTrue(object[six.u("spam")] == None)

        object["spam"] = "spam"
        self.assertTrue(object["spam"] == "spam")
        self.assertTrue(object["spam"] == six.u("spam"))
        self.assertTrue(object[six.u("spam")] == "spam")
        self.assertTrue(object[six.u("spam")] == six.u("spam"))

        object[six.u("eggs")] = six.u("eggs")
        self.assertTrue(object["eggs"] == "eggs")
        self.assertTrue(object["eggs"] == six.u("eggs"))
        self.assertTrue(object[six.u("eggs")] == "eggs")
        self.assertTrue(object[six.u("eggs")] == six.u("eggs"))

        def test():
            object = Test.StringIndexerTest()
            object[1]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.StringIndexerTest()
            object[1] = "wrong"

        self.assertRaises(TypeError, test)

    def testEnumIndexer(self):
        """Test enum indexers."""
        object = Test.EnumIndexerTest()

        key = Test.ShortEnum.One

        self.assertTrue(object[key] == None)

        object[key] = "spam"
        self.assertTrue(object[key] == "spam")

        object[key] = "eggs"
        self.assertTrue(object[key] == "eggs")

        object[1] = "spam"
        self.assertTrue(object[1] == "spam")

        def test():
            object = Test.EnumIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.EnumIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testObjectIndexer(self):
        """Test object indexers."""
        object = Test.ObjectIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(object[spam] == None)
        self.assertTrue(object["spam"] == None)
        self.assertTrue(object[1] == None)
        self.assertTrue(object[None] == None)

        object[spam] = "spam"
        self.assertTrue(object[spam] == "spam")

        object["spam"] = "eggs"
        self.assertTrue(object["spam"] == "eggs")

        object[1] = "one"
        self.assertTrue(object[1] == "one")

        object[long(1)] = "long"
        self.assertTrue(object[long(1)] == "long")

        def test():
            class eggs:
                pass

            key = eggs()
            object = Test.ObjectIndexerTest()
            object[key] = "wrong"

        self.assertRaises(TypeError, test)

    def testInterfaceIndexer(self):
        """Test interface indexers."""
        object = Test.InterfaceIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(object[spam] == None)

        object[spam] = "spam"
        self.assertTrue(object[spam] == "spam")

        object[spam] = "eggs"
        self.assertTrue(object[spam] == "eggs")

        def test():
            object = Test.InterfaceIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.InterfaceIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testTypedIndexer(self):
        """Test typed indexers."""
        object = Test.TypedIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(object[spam] == None)

        object[spam] = "spam"
        self.assertTrue(object[spam] == "spam")

        object[spam] = "eggs"
        self.assertTrue(object[spam] == "eggs")

        def test():
            object = Test.TypedIndexerTest()
            object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.TypedIndexerTest()
            object["wrong"] = "wrong"

        self.assertRaises(TypeError, test)

    def testMultiArgIndexer(self):
        """Test indexers that take multiple index arguments."""
        object = Test.MultiArgIndexerTest()

        object[0, 1] = "zero one"
        self.assertTrue(object[0, 1] == "zero one")

        object[1, 9] = "one nine"
        self.assertTrue(object[1, 9] == "one nine")

        self.assertTrue(object[10, 50] == None)

        def test():
            object = Test.MultiArgIndexerTest()
            v = object[0, "one"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.MultiArgIndexerTest()
            object[0, "one"] = "wrong"

        self.assertRaises(TypeError, test)

    def testMultiTypeIndexer(self):
        """Test indexers that take multiple indices of different types."""
        object = Test.MultiTypeIndexerTest()
        spam = Test.Spam("spam")

        object[0, "one", spam] = "zero one spam"
        self.assertTrue(object[0, "one", spam] == "zero one spam")

        object[1, "nine", spam] = "one nine spam"
        self.assertTrue(object[1, "nine", spam] == "one nine spam")

        def test():
            object = Test.MultiTypeIndexerTest()
            v = object[0, 1, spam]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.MultiTypeIndexerTest()
            object[0, 1, spam] = "wrong"

        self.assertRaises(TypeError, test)

    def testMultiDefaultKeyIndexer(self):
        """Test indexers that take multiple indices with a default key arguments."""
        # default argument is 2 in the MultiDefaultKeyIndexerTest object
        object = Test.MultiDefaultKeyIndexerTest()
        object[0, 2] = "zero one spam"
        self.assertTrue(object[0] == "zero one spam")

        object[1] = "one nine spam"
        self.assertTrue(object[1, 2] == "one nine spam")

    def testIndexerWrongKeyType(self):
        """Test calling an indexer using a key of the wrong type."""

        def test():
            object = Test.PublicIndexerTest()
            v = object["wrong"]

        self.assertRaises(TypeError, test)

        def test():
            object = Test.PublicIndexerTest()
            object["wrong"] = "spam"

        self.assertRaises(TypeError, test)

    def testIndexerWrongValueType(self):
        """Test calling an indexer using a value of the wrong type."""

        def test():
            object = Test.PublicIndexerTest()
            object[1] = 9993.9

        self.assertRaises(TypeError, test)

    def testUnboundIndexer(self):
        """Test calling an unbound indexer."""
        object = Test.PublicIndexerTest()

        Test.PublicIndexerTest.__setitem__(object, 0, "zero")
        self.assertTrue(object[0] == "zero")

        Test.PublicIndexerTest.__setitem__(object, 1, "one")
        self.assertTrue(object[1] == "one")

        self.assertTrue(object[10] == None)

    def testIndexerAbuse(self):
        """Test indexer abuse."""
        _class = Test.PublicIndexerTest
        object = Test.PublicIndexerTest()

        def test():
            del _class.__getitem__

        self.assertRaises(AttributeError, test)

        def test():
            del object.__getitem__

        self.assertRaises(AttributeError, test)

        def test():
            del _class.__setitem__

        self.assertRaises(AttributeError, test)

        def test():
            del object.__setitem__

        self.assertRaises(AttributeError, test)


def test_suite():
    return unittest.makeSuite(IndexerTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
