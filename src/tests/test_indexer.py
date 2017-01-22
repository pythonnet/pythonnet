# -*- coding: utf-8 -*-

import unittest

import Python.Test as Test

from _compat import long, unichr


class IndexerTests(unittest.TestCase):
    """Test support for indexer properties."""

    def test_public_indexer(self):
        """Test public indexers."""
        ob = Test.PublicIndexerTest()

        ob[0] = "zero"
        self.assertTrue(ob[0] == "zero")

        ob[1] = "one"
        self.assertTrue(ob[1] == "one")

        self.assertTrue(ob[10] is None)

    def test_protected_indexer(self):
        """Test protected indexers."""
        ob = Test.ProtectedIndexerTest()

        ob[0] = "zero"
        self.assertTrue(ob[0] == "zero")

        ob[1] = "one"
        self.assertTrue(ob[1] == "one")

        self.assertTrue(ob[10] is None)

    def test_internal_indexer(self):
        """Test internal indexers."""
        ob = Test.InternalIndexerTest()

        with self.assertRaises(TypeError):
            ob[0] = "zero"

        with self.assertRaises(TypeError):
            Test.InternalIndexerTest.__getitem__(ob, 0)

        with self.assertRaises(TypeError):
            ob.__getitem__(0)

    def test_private_indexer(self):
        """Test private indexers."""
        ob = Test.PrivateIndexerTest()

        with self.assertRaises(TypeError):
            ob[0] = "zero"

        with self.assertRaises(TypeError):
            Test.PrivateIndexerTest.__getitem__(ob, 0)

        with self.assertRaises(TypeError):
            ob.__getitem__(0)

    def test_boolean_indexer(self):
        """Test boolean indexers."""
        ob = Test.BooleanIndexerTest()

        self.assertTrue(ob[True] is None)
        self.assertTrue(ob[1] is None)

        ob[0] = "false"
        self.assertTrue(ob[0] == "false")

        ob[1] = "true"
        self.assertTrue(ob[1] == "true")

        ob[False] = "false"
        self.assertTrue(ob[False] == "false")

        ob[True] = "true"
        self.assertTrue(ob[True] == "true")

    def test_byte_indexer(self):
        """Test byte indexers."""
        ob = Test.ByteIndexerTest()
        max_ = 255
        min_ = 0

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.ByteIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.ByteIndexerTest()
            ob["wrong"] = "wrong"

    def test_sbyte_indexer(self):
        """Test sbyte indexers."""
        ob = Test.SByteIndexerTest()
        max_ = 127
        min_ = -128

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.SByteIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.SByteIndexerTest()
            ob["wrong"] = "wrong"

    def test_char_indexer(self):
        """Test char indexers."""
        ob = Test.CharIndexerTest()
        max_ = unichr(65535)
        min_ = unichr(0)

        self.assertTrue(ob[max_] is None)

        ob[max_] = "max_"
        self.assertTrue(ob[max_] == "max_")

        ob[min_] = "min_"
        self.assertTrue(ob[min_] == "min_")

        with self.assertRaises(TypeError):
            ob = Test.CharIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.CharIndexerTest()
            ob["wrong"] = "wrong"

    def test_int16_indexer(self):
        """Test Int16 indexers."""
        ob = Test.Int16IndexerTest()
        max_ = 32767
        min_ = -32768

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.Int16IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int16IndexerTest()
            ob["wrong"] = "wrong"

    def test_int32_indexer(self):
        """Test Int32 indexers."""
        ob = Test.Int32IndexerTest()
        max_ = 2147483647
        min_ = -2147483648

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.Int32IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int32IndexerTest()
            ob["wrong"] = "wrong"

    def test_int64_indexer(self):
        """Test Int64 indexers."""
        ob = Test.Int64IndexerTest()
        max_ = long(9223372036854775807)
        min_ = long(-9223372036854775808)

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.Int64IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.Int64IndexerTest()
            ob["wrong"] = "wrong"

    def test_uint16_indexer(self):
        """Test UInt16 indexers."""
        ob = Test.UInt16IndexerTest()
        max_ = 65535
        min_ = 0

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.UInt16IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt16IndexerTest()
            ob["wrong"] = "wrong"

    def test_uint32_indexer(self):
        """Test UInt32 indexers."""
        ob = Test.UInt32IndexerTest()
        max_ = long(4294967295)
        min_ = 0

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.UInt32IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt32IndexerTest()
            ob["wrong"] = "wrong"

    def test_uint64_indexer(self):
        """Test UInt64 indexers."""
        ob = Test.UInt64IndexerTest()
        max_ = long(18446744073709551615)
        min_ = 0

        self.assertTrue(ob[max_] is None)

        ob[max_] = str(max_)
        self.assertTrue(ob[max_] == str(max_))

        ob[min_] = str(min_)
        self.assertTrue(ob[min_] == str(min_))

        with self.assertRaises(TypeError):
            ob = Test.UInt64IndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.UInt64IndexerTest()
            ob["wrong"] = "wrong"

    def test_single_indexer(self):
        """Test Single indexers."""
        ob = Test.SingleIndexerTest()
        max_ = 3.402823e38
        min_ = -3.402823e38

        self.assertTrue(ob[max_] is None)

        ob[max_] = "max_"
        self.assertTrue(ob[max_] == "max_")

        ob[min_] = "min_"
        self.assertTrue(ob[min_] == "min_")

        with self.assertRaises(TypeError):
            ob = Test.SingleIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.SingleIndexerTest()
            ob["wrong"] = "wrong"

    def test_double_indexer(self):
        """Test Double indexers."""
        ob = Test.DoubleIndexerTest()
        max_ = 1.7976931348623157e308
        min_ = -1.7976931348623157e308

        self.assertTrue(ob[max_] is None)

        ob[max_] = "max_"
        self.assertTrue(ob[max_] == "max_")

        ob[min_] = "min_"
        self.assertTrue(ob[min_] == "min_")

        with self.assertRaises(TypeError):
            ob = Test.DoubleIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.DoubleIndexerTest()
            ob["wrong"] = "wrong"

    def test_decimal_indexer(self):
        """Test Decimal indexers."""
        ob = Test.DecimalIndexerTest()

        from System import Decimal
        max_d = Decimal.Parse("79228162514264337593543950335")
        min_d = Decimal.Parse("-79228162514264337593543950335")

        self.assertTrue(ob[max_d] is None)

        ob[max_d] = "max_"
        self.assertTrue(ob[max_d] == "max_")

        ob[min_d] = "min_"
        self.assertTrue(ob[min_d] == "min_")

        with self.assertRaises(TypeError):
            ob = Test.DecimalIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.DecimalIndexerTest()
            ob["wrong"] = "wrong"

    def test_string_indexer(self):
        """Test String indexers."""
        ob = Test.StringIndexerTest()

        self.assertTrue(ob["spam"] is None)
        self.assertTrue(ob[u"spam"] is None)

        ob["spam"] = "spam"
        self.assertTrue(ob["spam"] == "spam")
        self.assertTrue(ob["spam"] == u"spam")
        self.assertTrue(ob[u"spam"] == "spam")
        self.assertTrue(ob[u"spam"] == u"spam")

        ob[u"eggs"] = u"eggs"
        self.assertTrue(ob["eggs"] == "eggs")
        self.assertTrue(ob["eggs"] == u"eggs")
        self.assertTrue(ob[u"eggs"] == "eggs")
        self.assertTrue(ob[u"eggs"] == u"eggs")

        with self.assertRaises(TypeError):
            ob = Test.StringIndexerTest()
            ob[1]

        with self.assertRaises(TypeError):
            ob = Test.StringIndexerTest()
            ob[1] = "wrong"

    def test_enum_indexer(self):
        """Test enum indexers."""
        ob = Test.EnumIndexerTest()

        key = Test.ShortEnum.One

        self.assertTrue(ob[key] is None)

        ob[key] = "spam"
        self.assertTrue(ob[key] == "spam")

        ob[key] = "eggs"
        self.assertTrue(ob[key] == "eggs")

        ob[1] = "spam"
        self.assertTrue(ob[1] == "spam")

        with self.assertRaises(TypeError):
            ob = Test.EnumIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.EnumIndexerTest()
            ob["wrong"] = "wrong"

    def test_object_indexer(self):
        """Test ob indexers."""
        ob = Test.ObjectIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(ob[spam] is None)
        self.assertTrue(ob["spam"] is None)
        self.assertTrue(ob[1] is None)
        self.assertTrue(ob[None] is None)

        ob[spam] = "spam"
        self.assertTrue(ob[spam] == "spam")

        ob["spam"] = "eggs"
        self.assertTrue(ob["spam"] == "eggs")

        ob[1] = "one"
        self.assertTrue(ob[1] == "one")

        ob[long(1)] = "long"
        self.assertTrue(ob[long(1)] == "long")

        with self.assertRaises(TypeError):
            class Eggs(object):
                pass

            key = Eggs()
            ob = Test.ObjectIndexerTest()
            ob[key] = "wrong"

    def test_interface_indexer(self):
        """Test interface indexers."""
        ob = Test.InterfaceIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(ob[spam] is None)

        ob[spam] = "spam"
        self.assertTrue(ob[spam] == "spam")

        ob[spam] = "eggs"
        self.assertTrue(ob[spam] == "eggs")

        with self.assertRaises(TypeError):
            ob = Test.InterfaceIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.InterfaceIndexerTest()
            ob["wrong"] = "wrong"

    def test_typed_indexer(self):
        """Test typed indexers."""
        ob = Test.TypedIndexerTest()

        from Python.Test import Spam
        spam = Spam("spam")

        self.assertTrue(ob[spam] is None)

        ob[spam] = "spam"
        self.assertTrue(ob[spam] == "spam")

        ob[spam] = "eggs"
        self.assertTrue(ob[spam] == "eggs")

        with self.assertRaises(TypeError):
            ob = Test.TypedIndexerTest()
            ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.TypedIndexerTest()
            ob["wrong"] = "wrong"

    def test_multi_arg_indexer(self):
        """Test indexers that take multiple index arguments."""
        ob = Test.MultiArgIndexerTest()

        ob[0, 1] = "zero one"
        self.assertTrue(ob[0, 1] == "zero one")

        ob[1, 9] = "one nine"
        self.assertTrue(ob[1, 9] == "one nine")

        self.assertTrue(ob[10, 50] is None)

        with self.assertRaises(TypeError):
            ob = Test.MultiArgIndexerTest()
            _ = ob[0, "one"]

        with self.assertRaises(TypeError):
            ob = Test.MultiArgIndexerTest()
            ob[0, "one"] = "wrong"

    def test_multi_type_indexer(self):
        """Test indexers that take multiple indices of different types."""
        ob = Test.MultiTypeIndexerTest()
        spam = Test.Spam("spam")

        ob[0, "one", spam] = "zero one spam"
        self.assertTrue(ob[0, "one", spam] == "zero one spam")

        ob[1, "nine", spam] = "one nine spam"
        self.assertTrue(ob[1, "nine", spam] == "one nine spam")

        with self.assertRaises(TypeError):
            ob = Test.MultiTypeIndexerTest()
            _ = ob[0, 1, spam]

        with self.assertRaises(TypeError):
            ob = Test.MultiTypeIndexerTest()
            ob[0, 1, spam] = "wrong"

    def test_multi_default_key_indexer(self):
        """Test indexers that take multiple indices with a default
        key arguments."""
        # default argument is 2 in the MultiDefaultKeyIndexerTest object
        ob = Test.MultiDefaultKeyIndexerTest()
        ob[0, 2] = "zero one spam"
        self.assertTrue(ob[0] == "zero one spam")

        ob[1] = "one nine spam"
        self.assertTrue(ob[1, 2] == "one nine spam")

    def test_indexer_wrong_key_type(self):
        """Test calling an indexer using a key of the wrong type."""

        with self.assertRaises(TypeError):
            ob = Test.PublicIndexerTest()
            _ = ob["wrong"]

        with self.assertRaises(TypeError):
            ob = Test.PublicIndexerTest()
            ob["wrong"] = "spam"

    def test_indexer_wrong_value_type(self):
        """Test calling an indexer using a value of the wrong type."""

        with self.assertRaises(TypeError):
            ob = Test.PublicIndexerTest()
            ob[1] = 9993.9

    def test_unbound_indexer(self):
        """Test calling an unbound indexer."""
        ob = Test.PublicIndexerTest()

        Test.PublicIndexerTest.__setitem__(ob, 0, "zero")
        self.assertTrue(ob[0] == "zero")

        Test.PublicIndexerTest.__setitem__(ob, 1, "one")
        self.assertTrue(ob[1] == "one")

        self.assertTrue(ob[10] is None)

    def test_indexer_abuse(self):
        """Test indexer abuse."""
        _class = Test.PublicIndexerTest
        ob = Test.PublicIndexerTest()

        with self.assertRaises(AttributeError):
            del _class.__getitem__

        with self.assertRaises(AttributeError):
            del ob.__getitem__

        with self.assertRaises(AttributeError):
            del _class.__setitem__

        with self.assertRaises(AttributeError):
            del ob.__setitem__


def test_suite():
    return unittest.makeSuite(IndexerTests)
