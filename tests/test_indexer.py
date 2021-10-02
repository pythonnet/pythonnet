# -*- coding: utf-8 -*-

"""Test support for indexer properties."""

import Python.Test as Test
import pytest


def test_public_indexer():
    """Test public indexers."""
    ob = Test.PublicIndexerTest()

    ob[0] = "zero"
    assert ob[0] == "zero"

    ob[1] = "one"
    assert ob[1] == "one"

    assert ob[10] is None


def test_protected_indexer():
    """Test protected indexers."""
    ob = Test.ProtectedIndexerTest()

    ob[0] = "zero"
    assert ob[0] == "zero"

    ob[1] = "one"
    assert ob[1] == "one"

    assert ob[10] is None


def test_internal_indexer():
    """Test internal indexers."""
    ob = Test.InternalIndexerTest()

    with pytest.raises(TypeError):
        ob[0] = "zero"

    with pytest.raises(TypeError):
        Test.InternalIndexerTest.__getitem__(ob, 0)

    with pytest.raises(AttributeError):
        ob.__getitem__(0)


def test_private_indexer():
    """Test private indexers."""
    ob = Test.PrivateIndexerTest()

    with pytest.raises(TypeError):
        ob[0] = "zero"

    with pytest.raises(TypeError):
        Test.PrivateIndexerTest.__getitem__(ob, 0)

    with pytest.raises(AttributeError):
        ob.__getitem__(0)


def test_boolean_indexer():
    """Test boolean indexers."""
    ob = Test.BooleanIndexerTest()

    assert ob[True] is None

    with pytest.raises(TypeError):
        ob[1]
    with pytest.raises(TypeError):
        ob[0]
    with pytest.raises(TypeError):
        ob[1] = "true"
    with pytest.raises(TypeError):
        ob[0] = "false"

    ob[False] = "false"
    assert ob[False] == "false"

    ob[True] = "true"
    assert ob[True] == "true"


def test_byte_indexer():
    """Test byte indexers."""
    ob = Test.ByteIndexerTest()
    max_ = 255
    min_ = 0

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.ByteIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.ByteIndexerTest()
        ob["wrong"] = "wrong"


def test_sbyte_indexer():
    """Test sbyte indexers."""
    ob = Test.SByteIndexerTest()
    max_ = 127
    min_ = -128

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.SByteIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.SByteIndexerTest()
        ob["wrong"] = "wrong"


def test_char_indexer():
    """Test char indexers."""
    ob = Test.CharIndexerTest()
    max_ = chr(65535)
    min_ = chr(0)

    assert ob[max_] is None

    ob[max_] = "max_"
    assert ob[max_] == "max_"

    ob[min_] = "min_"
    assert ob[min_] == "min_"

    with pytest.raises(TypeError):
        ob = Test.CharIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.CharIndexerTest()
        ob["wrong"] = "wrong"


def test_int16_indexer():
    """Test Int16 indexers."""
    ob = Test.Int16IndexerTest()
    max_ = 32767
    min_ = -32768

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.Int16IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.Int16IndexerTest()
        ob["wrong"] = "wrong"


def test_int32_indexer():
    """Test Int32 indexers."""
    ob = Test.Int32IndexerTest()
    max_ = 2147483647
    min_ = -2147483648

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.Int32IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.Int32IndexerTest()
        ob["wrong"] = "wrong"


def test_int64_indexer():
    """Test Int64 indexers."""
    ob = Test.Int64IndexerTest()
    max_ = 9223372036854775807
    min_ = -9223372036854775808

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.Int64IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.Int64IndexerTest()
        ob["wrong"] = "wrong"


def test_uint16_indexer():
    """Test UInt16 indexers."""
    ob = Test.UInt16IndexerTest()
    max_ = 65535
    min_ = 0

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.UInt16IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.UInt16IndexerTest()
        ob["wrong"] = "wrong"


def test_uint32_indexer():
    """Test UInt32 indexers."""
    ob = Test.UInt32IndexerTest()
    max_ = 4294967295
    min_ = 0

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.UInt32IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.UInt32IndexerTest()
        ob["wrong"] = "wrong"


def test_uint64_indexer():
    """Test UInt64 indexers."""
    ob = Test.UInt64IndexerTest()
    max_ = 18446744073709551615
    min_ = 0

    assert ob[max_] is None

    ob[max_] = str(max_)
    assert ob[max_] == str(max_)

    ob[min_] = str(min_)
    assert ob[min_] == str(min_)

    with pytest.raises(TypeError):
        ob = Test.UInt64IndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.UInt64IndexerTest()
        ob["wrong"] = "wrong"


def test_single_indexer():
    """Test Single indexers."""
    ob = Test.SingleIndexerTest()
    max_ = 3.402823e38
    min_ = -3.402823e38

    assert ob[max_] is None

    ob[max_] = "max_"
    assert ob[max_] == "max_"

    ob[min_] = "min_"
    assert ob[min_] == "min_"

    with pytest.raises(TypeError):
        ob = Test.SingleIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.SingleIndexerTest()
        ob["wrong"] = "wrong"


def test_double_indexer():
    """Test Double indexers."""
    ob = Test.DoubleIndexerTest()
    max_ = 1.7976931348623157e308
    min_ = -1.7976931348623157e308

    assert ob[max_] is None

    ob[max_] = "max_"
    assert ob[max_] == "max_"

    ob[min_] = "min_"
    assert ob[min_] == "min_"

    with pytest.raises(TypeError):
        ob = Test.DoubleIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.DoubleIndexerTest()
        ob["wrong"] = "wrong"


def test_decimal_indexer():
    """Test Decimal indexers."""
    ob = Test.DecimalIndexerTest()

    from System import Decimal
    max_d = Decimal.Parse("79228162514264337593543950335")
    min_d = Decimal.Parse("-79228162514264337593543950335")

    assert ob[max_d] is None

    ob[max_d] = "max_"
    assert ob[max_d] == "max_"

    ob[min_d] = "min_"
    assert ob[min_d] == "min_"

    with pytest.raises(TypeError):
        ob = Test.DecimalIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.DecimalIndexerTest()
        ob["wrong"] = "wrong"


def test_string_indexer():
    """Test String indexers."""
    ob = Test.StringIndexerTest()

    assert ob["spam"] is None
    assert ob[u"spam"] is None

    ob["spam"] = "spam"
    assert ob["spam"] == "spam"
    assert ob["spam"] == u"spam"
    assert ob[u"spam"] == "spam"
    assert ob[u"spam"] == u"spam"

    ob[u"eggs"] = u"eggs"
    assert ob["eggs"] == "eggs"
    assert ob["eggs"] == u"eggs"
    assert ob[u"eggs"] == "eggs"
    assert ob[u"eggs"] == u"eggs"

    with pytest.raises(TypeError):
        ob = Test.StringIndexerTest()
        ob[1]

    with pytest.raises(TypeError):
        ob = Test.StringIndexerTest()
        ob[1] = "wrong"


def test_enum_indexer():
    """Test enum indexers."""
    ob = Test.EnumIndexerTest()

    key = Test.ShortEnum.One

    assert ob[key] is None

    ob[key] = "spam"
    assert ob[key] == "spam"

    ob[key] = "eggs"
    assert ob[key] == "eggs"

    with pytest.raises(TypeError):
        ob[1] = "spam"
    with pytest.raises(TypeError):
        ob[1]

    with pytest.raises(TypeError):
        ob = Test.EnumIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.EnumIndexerTest()
        ob["wrong"] = "wrong"


def test_object_indexer():
    """Test ob indexers."""
    ob = Test.ObjectIndexerTest()

    from Python.Test import Spam
    spam = Spam("spam")

    assert ob[spam] is None
    assert ob["spam"] is None
    assert ob[1] is None
    assert ob[None] is None

    ob[spam] = "spam"
    assert ob[spam] == "spam"

    ob["spam"] = "eggs"
    assert ob["spam"] == "eggs"

    ob[1] = "one"
    assert ob[1] == "one"

    ob[1] = "long"
    assert ob[1] == "long"

    class Eggs(object):
        pass

    key = Eggs()
    ob = Test.ObjectIndexerTest()
    ob[key] = "eggs_key"
    assert ob[key] == "eggs_key"


def test_interface_indexer():
    """Test interface indexers."""
    ob = Test.InterfaceIndexerTest()

    from Python.Test import Spam
    spam = Spam("spam")

    assert ob[spam] is None

    ob[spam] = "spam"
    assert ob[spam] == "spam"

    ob[spam] = "eggs"
    assert ob[spam] == "eggs"

    with pytest.raises(TypeError):
        ob = Test.InterfaceIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.InterfaceIndexerTest()
        ob["wrong"] = "wrong"


def test_typed_indexer():
    """Test typed indexers."""
    ob = Test.TypedIndexerTest()

    from Python.Test import Spam
    spam = Spam("spam")

    assert ob[spam] is None

    ob[spam] = "spam"
    assert ob[spam] == "spam"

    ob[spam] = "eggs"
    assert ob[spam] == "eggs"

    with pytest.raises(TypeError):
        ob = Test.TypedIndexerTest()
        ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.TypedIndexerTest()
        ob["wrong"] = "wrong"


def test_multi_arg_indexer():
    """Test indexers that take multiple index arguments."""
    ob = Test.MultiArgIndexerTest()

    ob[0, 1] = "zero one"
    assert ob[0, 1] == "zero one"

    ob[1, 9] = "one nine"
    assert ob[1, 9] == "one nine"

    assert ob[10, 50] is None

    with pytest.raises(TypeError):
        ob = Test.MultiArgIndexerTest()
        _ = ob[0, "one"]

    with pytest.raises(TypeError):
        ob = Test.MultiArgIndexerTest()
        ob[0, "one"] = "wrong"


def test_multi_type_indexer():
    """Test indexers that take multiple indices of different types."""
    ob = Test.MultiTypeIndexerTest()
    spam = Test.Spam("spam")

    ob[0, "one", spam] = "zero one spam"
    assert ob[0, "one", spam] == "zero one spam"

    ob[1, "nine", spam] = "one nine spam"
    assert ob[1, "nine", spam] == "one nine spam"

    with pytest.raises(TypeError):
        ob = Test.MultiTypeIndexerTest()
        _ = ob[0, 1, spam]

    with pytest.raises(TypeError):
        ob = Test.MultiTypeIndexerTest()
        ob[0, 1, spam] = "wrong"


def test_multi_default_key_indexer():
    """Test indexers that take multiple indices with a default
    key arguments."""
    # default argument is 2 in the MultiDefaultKeyIndexerTest object
    ob = Test.MultiDefaultKeyIndexerTest()
    ob[0, 2] = "zero one spam"
    assert ob[0] == "zero one spam"

    ob[1] = "one nine spam"
    assert ob[1, 2] == "one nine spam"


def test_indexer_wrong_key_type():
    """Test calling an indexer using a key of the wrong type."""

    with pytest.raises(TypeError):
        ob = Test.PublicIndexerTest()
        _ = ob["wrong"]

    with pytest.raises(TypeError):
        ob = Test.PublicIndexerTest()
        ob["wrong"] = "spam"


def test_indexer_wrong_value_type():
    """Test calling an indexer using a value of the wrong type."""

    with pytest.raises(TypeError):
        ob = Test.PublicIndexerTest()
        ob[1] = 9993.9


def test_unbound_indexer():
    """Test calling an unbound indexer."""
    ob = Test.PublicIndexerTest()

    Test.PublicIndexerTest.__setitem__(ob, 0, "zero")
    assert ob[0] == "zero"

    Test.PublicIndexerTest.__setitem__(ob, 1, "one")
    assert ob[1] == "one"

    assert ob[10] is None


def test_indexer_abuse():
    """Test indexer abuse."""
    _class = Test.PublicIndexerTest
    ob = Test.PublicIndexerTest()

    with pytest.raises(AttributeError):
        del _class.__getitem__

    with pytest.raises(AttributeError):
        del ob.__getitem__

    with pytest.raises(AttributeError):
        del _class.__setitem__

    with pytest.raises(AttributeError):
        del ob.__setitem__


def test_indexer_accessed_through_interface():
    """Test that indexers can be accessed through interfaces"""
    from System.Collections.Generic import Dictionary, IDictionary
    d = IDictionary[str, str](Dictionary[str, str]())
    d["one"] = "1"
    assert d["one"] == "1"


def test_using_indexer_on_object_without_indexer():
    """Test using subscript syntax on an object an without indexer raises"""
    from System import Object
    o = Object()
    with pytest.raises(TypeError):
        o[0]

    with pytest.raises(TypeError):
        o[0] = 1


def test_inherited_indexer():
    """Test that inherited indexers are accessible"""
    from Python.Test import PublicInheritedIndexerTest
    from Python.Test import ProtectedInheritedIndexerTest
    from Python.Test import PrivateInheritedIndexerTest
    from Python.Test import InternalInheritedIndexerTest

    pub = PublicInheritedIndexerTest()
    pub[0] = "zero"
    assert pub[0] == "zero"

    def assert_no_indexer(obj):
        with pytest.raises(TypeError):
            obj[0]
        with pytest.raises(TypeError):
            obj[0] = "zero"

    assert_no_indexer(PrivateInheritedIndexerTest)
    assert_no_indexer(ProtectedInheritedIndexerTest)
    assert_no_indexer(InternalInheritedIndexerTest)


def test_inherited_indexer_interface():
    """Test that indexers inherited from other interfaces are accessible"""
    from Python.Test import InterfaceInheritedIndexerTest, IInheritedIndexer

    impl = InterfaceInheritedIndexerTest()
    ifc = IInheritedIndexer(impl)
    ifc[0] = "zero"
    assert ifc[0] == "zero"

def test_public_inherited_overloaded_indexer():
    """Test public indexers."""
    ob = Test.PublicInheritedOverloadedIndexer()

    ob[0] = "zero"
    assert ob[0] == "zero"

    ob[1] = "one"
    assert ob[1] == "one"

    assert ob[10] is None

    ob["spam"] = "spam"
    assert ob["spam"] == "spam"

    with pytest.raises(TypeError):
        ob[[]]
