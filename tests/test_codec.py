# -*- coding: utf-8 -*-

"""Test conversions using codecs from client python code"""

import pytest
import Python.Runtime
import Python.Test as Test
from Python.Test import ListConversionTester, ListMember, CodecResetter


@pytest.fixture(autouse=True)
def reset():
    CodecResetter.Reset()
    yield
    CodecResetter.Reset()


class int_iterable:
    def __init__(self):
        self.counter = 0

    def __iter__(self):
        return self

    def __next__(self):
        if self.counter == 3:
            raise StopIteration
        self.counter = self.counter + 1
        return self.counter


class obj_iterable:
    def __init__(self):
        self.counter = 0

    def __iter__(self):
        return self

    def __next__(self):
        if self.counter == 3:
            raise StopIteration
        self.counter = self.counter + 1
        return ListMember(self.counter, "Number " + str(self.counter))


def test_iterable():
    """Test that a python iterable can be passed into a function that takes an
    IEnumerable<object>"""

    # Python.Runtime.Codecs.ListDecoder.Register()
    # Python.Runtime.Codecs.SequenceDecoder.Register()
    Python.Runtime.Codecs.IterableDecoder.Register()
    ob = ListConversionTester()

    iterable = int_iterable()
    assert 3 == ob.GetLength(iterable)

    iterable2 = obj_iterable()
    assert 3 == ob.GetLength2(iterable2)


def test_sequence():
    Python.Runtime.Codecs.SequenceDecoder.Register()
    ob = ListConversionTester()

    tup = (1, 2, 3)
    assert 3 == ob.GetLength(tup)

    tup2 = (ListMember(1, "one"), ListMember(2, "two"), ListMember(3, "three"))
    assert 3 == ob.GetLength(tup2)


def test_list():
    Python.Runtime.Codecs.SequenceDecoder.Register()
    ob = ListConversionTester()

    l = [1, 2, 3]
    assert 3 == ob.GetLength(l)

    l2 = [ListMember(1, "one"), ListMember(2, "two"), ListMember(3, "three")]
    assert 3 == ob.GetLength(l2)


def test_enum():
    Python.Runtime.PyObjectConversions.RegisterEncoder(
        Python.Runtime.Codecs.EnumPyIntCodec.Instance
    )

    assert Test.ByteEnum.Zero == 0
    assert Test.ByteEnum.One == 1
    assert Test.ByteEnum.Two == 2
    assert Test.SByteEnum.Zero == 0
    assert Test.SByteEnum.One == 1
    assert Test.SByteEnum.Two == 2
    assert Test.ShortEnum.Zero == 0
    assert Test.ShortEnum.One == 1
    assert Test.ShortEnum.Two == 2
    assert Test.UShortEnum.Zero == 0
    assert Test.UShortEnum.One == 1
    assert Test.UShortEnum.Two == 2
    assert Test.IntEnum.Zero == 0
    assert Test.IntEnum.One == 1
    assert Test.IntEnum.Two == 2
    assert Test.UIntEnum.Zero == 0
    assert Test.UIntEnum.One == 1
    assert Test.UIntEnum.Two == 2
    assert Test.LongEnum.Zero == 0
    assert Test.LongEnum.One == 1
    assert Test.LongEnum.Two == 2
    assert Test.ULongEnum.Zero == 0
    assert Test.ULongEnum.One == 1
    assert Test.ULongEnum.Two == 2
    assert Test.LongEnum.Max == 9223372036854775807
    assert Test.LongEnum.Min == -9223372036854775808
    assert int(Test.ULongEnum.Max) == 18446744073709551615
