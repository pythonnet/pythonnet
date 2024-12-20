from __future__ import annotations

from typing import Any
from collections.abc import Container

import pytest

import clr
clr.AddReference('System.Collections.Immutable')
from System import Array, Nullable, ValueType, Int32, String
import System.Collections.Generic as Gen
import System.Collections.Immutable as Imm

values_1 = (10, 11, 12, 13, 14)
values_2 = values_1 + (None, )
values_3 = tuple(map(str, values_1))
values_4 = values_3 + (None, )


def exactly_equal(val1, val2):
    return type(val1) is type(val2) and val1 == val2, '{0!r} != {1!r}'.format(val1, val2)


def translate_pytype(pytype, *, nullable=False):
    if pytype is int:
        cstype = Int32
    elif pytype is str:
        cstype = String
    else:
        raise NotImplementedError('Unsupported type: {0!r}'.format(pytype))

    if nullable and issubclass(cstype, ValueType):
        cstype = Nullable[cstype]

    return cstype


class SequenceTests:
    vtype: type
    nullable: bool
    cs_vtype: type
    lst: Any
    null_values: Container[int]

    def __init_subclass__(cls, /, values=None, **kwargs):
        if values is not None:
            cls.vtype,cls.nullable,cls.cs_vtype = cls.deduce_types(values)
            cls.null_values = tuple(idx for idx,val in enumerate(values) if val is None)

    @staticmethod
    def deduce_types(values):
        vtypes = set(map(type, values))
        nullable = type(None) in vtypes
        if nullable:
            vtypes.remove(type(None))
        (vtype, ) = vtypes
        cs_vtype = translate_pytype(vtype, nullable=nullable)
        return (vtype, nullable, cs_vtype)

    def test_len(self):
        length = len(self.lst)
        assert exactly_equal(length, 5)

    def test_iter(self):
        for idx,val in enumerate(self.lst):
            exp_val = None if idx in self.null_values else self.vtype(idx + 10)
            assert exactly_equal(val, exp_val)

    def test_reversed(self):
        length = len(self.lst)
        for idx,val in enumerate(reversed(self.lst)):
            exp_val = None if idx in self.null_values else self.vtype(10 + length - idx - 1)
            assert exactly_equal(val, exp_val)

    def test_getitem(self):
        length = len(self.lst)
        for idx in range(length):
            val = self.lst[idx]
            assert exactly_equal(val, self.vtype(10 + idx))

    def test_getitem_negidx(self):
        length = len(self.lst)
        for idx in range(length):
            val = self.lst[idx - length]
            assert exactly_equal(val, self.vtype(10 + idx))

    def test_getitem_raise(self):
        length = len(self.lst)
        with pytest.raises(IndexError):
            self.lst[length]
        with pytest.raises(IndexError):
            self.lst[-1-length]

    def test_contains(self):
        assert self.vtype(10) in self.lst
        assert self.vtype(14) in self.lst
        assert self.vtype(15) not in self.lst

    def test_index(self):
        assert self.lst.index(self.vtype(10)) == 0
        assert self.lst.index(self.vtype(14)) == 4
        with pytest.raises(ValueError):
            self.lst.index(self.vtype(15))


class MutableSequenceTests(SequenceTests):
    def get_copy(self) -> Any:
        raise NotImplementedError('must be overridden!')

    def test_setitem(self):
        arr = self.get_copy()
        arr[0] = self.vtype(111)
        assert exactly_equal(arr[0], self.vtype(111))

    def test_setitem_negidx(self):
        arr = self.get_copy()
        arr[-1] = self.vtype(222)
        assert arr[len(arr)-1] == self.vtype(222)

    def test_setitem_raise(self):
        arr = self.get_copy()
        length = len(arr)
        with pytest.raises(IndexError):
            arr[length] = self.vtype(0)
        with pytest.raises(IndexError):
            arr[-1-length] = self.vtype(0)

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_delitem(self):
        arr = self.get_copy()
        exp_lst = list(arr)[:-1]
        del arr[-1]
        assert list(arr) == exp_lst

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_delitem_raise(self):
        arr = self.get_copy()
        with pytest.raises(Exception):
            del arr[len(arr)]
        with pytest.raises(Exception):
            del arr[-1-len(arr)]

    def test_insert(self):
        arr = self.get_copy()
        length = len(arr)
        arr.insert(1, 333)
        assert len(arr) == length + 1
        assert arr[1] == 333

    def test_append(self):
        arr = self.get_copy()
        orig_length = len(arr)
        arr.append(444)
        assert len(arr) == orig_length + 1
        assert arr[orig_length] == 444

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_pop(self):
        arr = self.get_copy()
        length = len(arr)
        val = arr.pop(1)
        assert exactly_equal(val, self.vtype(11))
        assert len(arr) == length - 1

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_pop_last(self):
        arr = self.get_copy()
        length = len(arr)
        val = arr.pop()
        assert exactly_equal(val, None if self.nullable else self.vtype(14))
        assert len(arr) == length - 1

    def test_extend(self):
        arr = self.get_copy()
        orig_length = len(arr)
        arr.extend([self.vtype(555), self.vtype(666)])
        assert exactly_equal(arr[orig_length    ], self.vtype(555))
        assert exactly_equal(arr[orig_length + 1], self.vtype(666))
        assert len(arr) == orig_length + 2

    def test_reverse(self):
        arr = self.get_copy()
        expected = list(arr)[::-1]
        arr.reverse()
        assert list(arr) == expected

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_remove(self):
        arr = self.get_copy()
        expected = [val for val in arr if val != self.vtype(13)]
        arr.remove(self.vtype(13))
        assert list(arr) == expected

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_remove_raise(self):
        arr = self.get_copy()
        with pytest.raises(ValueError):
            arr.remove(self.vtype(15))

    def test_iadd(self):
        arr = self.get_copy()
        orig_length = len(arr)
        arr += [777, 888]
        assert exactly_equal(arr[orig_length    ], self.vtype(777))
        assert exactly_equal(arr[orig_length + 1], self.vtype(888))
        assert len(arr) == orig_length + 2

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_clear(self):
        arr = self.get_copy()
        arr.clear()
        assert len(arr) == 0


class PyListTests(MutableSequenceTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.lst = list(values)

    def get_copy(self):
        return self.lst.copy()

# class TestPyListInt    (PyListTests, values=values_1): pass
# class TestPyListNullInt(PyListTests, values=values_2): pass
# class TestPyListStr    (PyListTests, values=values_3): pass
# class TestPyListNullStr(PyListTests, values=values_4): pass


class ArrayTests(MutableSequenceTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.lst = Array[cls.cs_vtype](values)

    def get_copy(self):
        return Array[self.cs_vtype](self.lst)

class TestArrayInt    (ArrayTests, values=values_1): pass
class TestArrayNullInt(ArrayTests, values=values_2): pass
class TestArrayStr    (ArrayTests, values=values_3): pass
class TestArrayNullStr(ArrayTests, values=values_4): pass


class ImmutableArrayTests(SequenceTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.lst = Imm.ImmutableArray[cls.cs_vtype](values)

class TestImmutableArrayInt    (ImmutableArrayTests, values=values_1): pass
class TestImmutableArrayNullInt(ImmutableArrayTests, values=values_2): pass
class TestImmutableArrayStr    (ImmutableArrayTests, values=values_3): pass
class TestImmutableArrayNullStr(ImmutableArrayTests, values=values_4): pass


class ListTests(MutableSequenceTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.lst = Gen.List[cls.cs_vtype](Array[cls.cs_vtype](values))

    def get_copy(self):
        return Gen.List[self.cs_vtype](self.lst)

class TestListInt    (ListTests, values=values_1): pass
class TestListNullInt(ListTests, values=values_2): pass
class TestListStr    (ListTests, values=values_3): pass
class TestListNullStr(ListTests, values=values_4): pass


class ImmutableListTests(SequenceTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.lst = Imm.ImmutableList.ToImmutableList[cls.cs_vtype](Array[cls.cs_vtype](values))

class TestImmutableListInt    (ImmutableListTests, values=values_1): pass
class TestImmutableListNullInt(ImmutableListTests, values=values_2): pass
class TestImmutableListStr    (ImmutableListTests, values=values_3): pass
class TestImmutableListNullStr(ImmutableListTests, values=values_4): pass
