# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
import Python.Test as Test


class IndexerTests(unittest.TestCase):
    """Test support for indexer properties."""

    def testPublicIndexer(self):
        """Test public indexers."""
        object = Test.PublicIndexerTest()

        object[0] = "zero"
        self.failUnless(object[0] == "zero")
        
        object[1] = "one"
        self.failUnless(object[1] == "one")

        self.failUnless(object[10] == None)


    def testProtectedIndexer(self):
        """Test protected indexers."""
        object = Test.ProtectedIndexerTest()

        object[0] = "zero"
        self.failUnless(object[0] == "zero")
        
        object[1] = "one"
        self.failUnless(object[1] == "one")

        self.failUnless(object[10] == None)


    def testInternalIndexer(self):
        """Test internal indexers."""
        object = Test.InternalIndexerTest()

        def test():
            object[0] = "zero"

        self.failUnlessRaises(TypeError, test)

        def test():
            Test.InternalIndexerTest.__getitem__(object, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            object.__getitem__(0)

        self.failUnlessRaises(TypeError, test)


    def testPrivateIndexer(self):
        """Test private indexers."""
        object = Test.PrivateIndexerTest()

        def test():
            object[0] = "zero"

        self.failUnlessRaises(TypeError, test)

        def test():
            Test.PrivateIndexerTest.__getitem__(object, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            object.__getitem__(0)

        self.failUnlessRaises(TypeError, test)


    def testBooleanIndexer(self):
        """Test boolean indexers."""
        object = Test.BooleanIndexerTest()

        self.failUnless(object[True] == None)
        self.failUnless(object[1] == None)

        object[0] = "false"
        self.failUnless(object[0] == "false")
        
        object[1] = "true"
        self.failUnless(object[1] == "true")

        object[False] = "false"
        self.failUnless(object[False] == "false")
        
        object[True] = "true"
        self.failUnless(object[True] == "true")


    def testByteIndexer(self):
        """Test byte indexers."""
        object = Test.ByteIndexerTest()
        max = 255
        min = 0

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.ByteIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.ByteIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testSByteIndexer(self):
        """Test sbyte indexers."""
        object = Test.SByteIndexerTest()
        max = 127
        min = -128

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.SByteIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.SByteIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testCharIndexer(self):
        """Test char indexers."""
        object = Test.CharIndexerTest()
        max = unichr(65535)
        min = unichr(0)
    
        self.failUnless(object[max] == None)

        object[max] = "max"
        self.failUnless(object[max] == "max")
        
        object[min] = "min"
        self.failUnless(object[min] == "min")

        def test():
            object = Test.CharIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.CharIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt16Indexer(self):
        """Test Int16 indexers."""
        object = Test.Int16IndexerTest()
        max = 32767
        min = -32768

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.Int16IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int16IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt32Indexer(self):
        """Test Int32 indexers."""
        object = Test.Int32IndexerTest()
        max = 2147483647
        min = -2147483648

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.Int32IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int32IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInt64Indexer(self):
        """Test Int64 indexers."""
        object = Test.Int64IndexerTest()
        max = 9223372036854775807L
        min = -9223372036854775808L

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.Int64IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.Int64IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testUInt16Indexer(self):
        """Test UInt16 indexers."""
        object = Test.UInt16IndexerTest()
        max = 65535
        min = 0

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.UInt16IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt16IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testUInt32Indexer(self):
        """Test UInt32 indexers."""
        object = Test.UInt32IndexerTest()
        max = 4294967295L
        min = 0

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.UInt32IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt32IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testUInt64Indexer(self):
        """Test UInt64 indexers."""
        object = Test.UInt64IndexerTest()
        max = 18446744073709551615L
        min = 0

        self.failUnless(object[max] == None)

        object[max] = str(max)
        self.failUnless(object[max] == str(max))
        
        object[min] = str(min)
        self.failUnless(object[min] == str(min))

        def test():
            object = Test.UInt64IndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.UInt64IndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testSingleIndexer(self):
        """Test Single indexers."""
        object = Test.SingleIndexerTest()
        max = 3.402823e38
        min = -3.402823e38

        self.failUnless(object[max] == None)

        object[max] = "max"
        self.failUnless(object[max] == "max")
        
        object[min] = "min"
        self.failUnless(object[min] == "min")

        def test():
            object = Test.SingleIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.SingleIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testDoubleIndexer(self):
        """Test Double indexers."""
        object = Test.DoubleIndexerTest()
        max = 1.7976931348623157e308
        min = -1.7976931348623157e308

        self.failUnless(object[max] == None)

        object[max] = "max"
        self.failUnless(object[max] == "max")
        
        object[min] = "min"
        self.failUnless(object[min] == "min")

        def test():
            object = Test.DoubleIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.DoubleIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testDecimalIndexer(self):
        """Test Decimal indexers."""
        object = Test.DecimalIndexerTest()

        from System import Decimal
        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.failUnless(object[max_d] == None)

        object[max_d] = "max"
        self.failUnless(object[max_d] == "max")
        
        object[min_d] = "min"
        self.failUnless(object[min_d] == "min")

        def test():
            object = Test.DecimalIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.DecimalIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testStringIndexer(self):
        """Test String indexers."""
        object = Test.StringIndexerTest()

        self.failUnless(object["spam"] == None)
        self.failUnless(object[u"spam"] == None)

        object["spam"] = "spam"
        self.failUnless(object["spam"] == "spam")
        self.failUnless(object["spam"] == u"spam")
        self.failUnless(object[u"spam"] == "spam")
        self.failUnless(object[u"spam"] == u"spam")
        
        object[u"eggs"] = u"eggs"
        self.failUnless(object["eggs"] == "eggs")
        self.failUnless(object["eggs"] == u"eggs")
        self.failUnless(object[u"eggs"] == "eggs")
        self.failUnless(object[u"eggs"] == u"eggs")

        def test():
            object = Test.StringIndexerTest()
            object[1]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.StringIndexerTest()
            object[1] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testEnumIndexer(self):
        """Test enum indexers."""
        object = Test.EnumIndexerTest()

        key = Test.ShortEnum.One

        self.failUnless(object[key] == None)

        object[key] = "spam"
        self.failUnless(object[key] == "spam")
        
        object[key] = "eggs"
        self.failUnless(object[key] == "eggs")

        object[1] = "spam"
        self.failUnless(object[1] == "spam")

        def test():
            object = Test.EnumIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.EnumIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testObjectIndexer(self):
        """Test object indexers."""
        object = Test.ObjectIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.failUnless(object[spam] == None)
        self.failUnless(object["spam"] == None)
        self.failUnless(object[1] == None)
        self.failUnless(object[None] == None)

        object[spam] = "spam"
        self.failUnless(object[spam] == "spam")
        
        object["spam"] = "eggs"
        self.failUnless(object["spam"] == "eggs")

        object[1] = "one"
        self.failUnless(object[1] == "one")

        object[1L] = "long"
        self.failUnless(object[1L] == "long")

        def test():
            class eggs:
                pass
            key = eggs()
            object = Test.ObjectIndexerTest()
            object[key] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testInterfaceIndexer(self):
        """Test interface indexers."""
        object = Test.InterfaceIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.failUnless(object[spam] == None)

        object[spam] = "spam"
        self.failUnless(object[spam] == "spam")
        
        object[spam] = "eggs"
        self.failUnless(object[spam] == "eggs")

        def test():
            object = Test.InterfaceIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.InterfaceIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testTypedIndexer(self):
        """Test typed indexers."""
        object = Test.TypedIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.failUnless(object[spam] == None)

        object[spam] = "spam"
        self.failUnless(object[spam] == "spam")
        
        object[spam] = "eggs"
        self.failUnless(object[spam] == "eggs")

        def test():
            object = Test.TypedIndexerTest()
            object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.TypedIndexerTest()
            object["wrong"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testMultiArgIndexer(self):
        """Test indexers that take multiple index arguments."""
        object = Test.MultiArgIndexerTest()

        object[0, 1] = "zero one"
        self.failUnless(object[0, 1] == "zero one")
        
        object[1, 9] = "one nine"
        self.failUnless(object[1, 9] == "one nine")

        self.failUnless(object[10, 50] == None)

        def test():
            object = Test.MultiArgIndexerTest()
            v = object[0, "one"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.MultiArgIndexerTest()
            object[0, "one"] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testMultiTypeIndexer(self):
        """Test indexers that take multiple indices of different types."""
        object = Test.MultiTypeIndexerTest()
        spam = Test.Spam("spam")

        object[0, "one", spam] = "zero one spam"
        self.failUnless(object[0, "one", spam] == "zero one spam")
        
        object[1, "nine", spam] = "one nine spam"
        self.failUnless(object[1, "nine", spam] == "one nine spam")

        def test():
            object = Test.MultiTypeIndexerTest()
            v = object[0, 1, spam]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.MultiTypeIndexerTest()
            object[0, 1, spam] = "wrong"

        self.failUnlessRaises(TypeError, test)


    def testIndexerWrongKeyType(self):
        """Test calling an indexer using a key of the wrong type."""

        def test():
            object = Test.PublicIndexerTest()
            v = object["wrong"]

        self.failUnlessRaises(TypeError, test)

        def test():
            object = Test.PublicIndexerTest()
            object["wrong"] = "spam"

        self.failUnlessRaises(TypeError, test)


    def testIndexerWrongValueType(self):
        """Test calling an indexer using a value of the wrong type."""

        def test():
            object = Test.PublicIndexerTest()
            object[1] = 9993.9

        self.failUnlessRaises(TypeError, test)


    def testUnboundIndexer(self):
        """Test calling an unbound indexer."""
        object = Test.PublicIndexerTest()

        Test.PublicIndexerTest.__setitem__(object, 0, "zero")
        self.failUnless(object[0] == "zero")
        
        Test.PublicIndexerTest.__setitem__(object, 1, "one")
        self.failUnless(object[1] == "one")

        self.failUnless(object[10] == None)


    def testIndexerAbuse(self):
        """Test indexer abuse."""
        _class = Test.PublicIndexerTest
        object = Test.PublicIndexerTest()

        def test():
            del _class.__getitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del object.__getitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del _class.__setitem__

        self.failUnlessRaises(AttributeError, test)

        def test():
            del object.__setitem__

        self.failUnlessRaises(AttributeError, test)



def test_suite():
    return unittest.makeSuite(IndexerTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

