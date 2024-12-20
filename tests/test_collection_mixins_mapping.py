from __future__ import annotations

import operator
from typing import Any
from collections.abc import Container

import pytest

import clr
clr.AddReference('System.Collections.Immutable')
from System import Nullable, Object, ValueType, Int32, String
import System.Collections.Generic as Gen
import System.Collections.Immutable as Imm
import System.Collections.ObjectModel as OM

kv_pairs_1 = ((0, "0"), (10, "10"), (20, "20"), (30, "30"))
kv_pairs_2 = kv_pairs_1 + ((40, None), )
kv_pairs_3 = (("0", 0), ("10", 10), ("20", 20), ("30", 30))
kv_pairs_4 = kv_pairs_3 + (("40", None), )


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


class MappingTests:
    ktype: type
    vtype: type
    nullable: bool
    cs_ktype: type
    cs_vtype: type
    mapped_to_null: Container[Any]
    dct: Any

    def __init_subclass__(cls, /, values=None, **kwargs):
        if values is not None:
            cls.ktype,cls.vtype,cls.nullable,cls.cs_ktype,cls.cs_vtype = cls.deduce_types(values)
            cls.mapped_to_null = tuple(key for key,val in values if val is None)

    @staticmethod
    def deduce_types(values):
        (ktype, ) = {type(key) for key,_ in values}
        vtypes = {type(val) for _,val in values}
        nullable = type(None) in vtypes
        if nullable:
            vtypes.remove(type(None))
        (vtype, ) = vtypes
        cs_ktype = translate_pytype(ktype)
        cs_vtype = translate_pytype(vtype, nullable=nullable)
        return (ktype, vtype, nullable, cs_ktype, cs_vtype)

    def test_len(self):
        assert type(len(self.dct)) is int
        assert len(self.dct) == 4 if not self.nullable else 5

    def test_iter(self):
        for idx,key in enumerate(self.dct):
            assert type(key) is self.ktype and int(key) == idx * 10

    def test_keys(self):
        keys = sorted(self.dct.keys())
#         print(f'### {list(self.dct.keys())}')  # TODO: DELME
        for idx,key in enumerate(keys):
            assert exactly_equal(key, self.ktype(idx * 10))

    def test_values(self):
        values = sorted(self.dct.values(), key=lambda val: val if val is not None else self.vtype(999))
        for idx,val in enumerate(values):
            exp_val = None if self.ktype(10 * idx) in self.mapped_to_null else self.vtype(10 * idx)
            assert exactly_equal(val, exp_val)

    def test_items(self):
        items = sorted(self.dct.items(), key=operator.itemgetter(0))
        for idx,tpl in enumerate(items):
            assert type(tpl) is tuple and len(tpl) == 2
            key,val = tpl
            assert exactly_equal(key, self.ktype(idx * 10))
            exp_val = None if self.ktype(10 * idx) in self.mapped_to_null else self.vtype(10 * idx)
            assert exactly_equal(val, exp_val)

    def test_contains(self):
        assert self.ktype(10) in self.dct
        assert self.ktype(50) not in self.dct
        assert 12.34 not in self.dct

    def test_getitem(self):
        for idx in range(len(self.dct)):
            val = self.dct[self.ktype(10 * idx)]
            assert exactly_equal(val, self.vtype(10 * idx))

    def test_getitem_raise(self):
        with pytest.raises(KeyError):
            self.dct[self.ktype(50)]
        with pytest.raises(KeyError):
            self.dct[12.34]

    def test_get(self):
        val = self.dct.get(self.ktype(10))
        assert exactly_equal(val, self.vtype(10))
        assert self.dct.get(self.ktype(50)) is None
        val = self.dct.get(self.ktype(50), 123.1)
        assert val == 123.1


class MutableMappingTests(MappingTests):
    def get_copy(self) -> Any:
        raise NotImplementedError('must be overridden!')

    def test_setitem(self):
        dct = self.get_copy()
        key,val = (self.ktype(10), self.vtype(11))
        dct[key] = val
        assert exactly_equal(dct[key], val)

    def test_setitem_raise(self):
        if isinstance(self.dct, Object):  # this is only relevant for CLR types
            dct = self.get_copy()
            with pytest.raises(Exception):
                dct[12.34] = self.vtype(0)

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_delitem(self):
        dct = self.get_copy()
        del dct[self.ktype(10)]
        assert self.ktype(10) not in dct

    @pytest.mark.xfail(reason='Known to crash', run=False)
    def test_delitem_raise(self):
        dct = self.get_copy()
        with pytest.raises(KeyError):
            del dct[12.34]

    def test_pop(self):
        dct = self.get_copy()
        length = len(dct)
        val = dct.pop(self.ktype(10))
        assert exactly_equal(val, self.vtype(10))
        val = dct.pop(self.ktype(10), self.vtype(11))
        assert exactly_equal(val, self.vtype(11))
        assert len(dct) == length - 1

    def test_popitem(self):
        dct = self.get_copy()
        while len(dct) != 0:
            tpl = dct.popitem()
            assert type(tpl) is tuple and len(tpl) == 2
            key,val = tpl
            assert type(key) is self.ktype
            assert type(val) is self.vtype or (self.nullable and val is None)
            if val is not None:
                assert int(key) == int(val)

    def test_clear(self):
        dct = self.get_copy()
        dct.clear()
        assert len(dct) == 0
        assert dict(dct) == {}

    def test_setdefault(self):
        dct = self.get_copy()
        dct.setdefault(self.ktype(50), self.vtype(50))
        assert exactly_equal(dct[self.ktype(50)], self.vtype(50))

    def test_update(self):
        dct = self.get_copy()
        pydict = {self.ktype(num): self.vtype(num) for num in (30, 40)}
        if self.nullable:
            pydict[self.ktype(50)] = None
        dct.update(pydict)
        pydict.update({self.ktype(num): self.vtype(num) for num in (0, 10, 20)})  # put in the items we expect to be set already
        assert dict(dct) == pydict
        extra_vals = tuple((self.ktype(num), self.vtype(num)) for num in (60, 70))
        dct.update(extra_vals)
        pydict.update(extra_vals)
        assert dict(dct) == pydict
        if self.ktype is str:
            dct.update(aaa=80, bbb=90)
            pydict.update(aaa=80, bbb=90)
            assert dict(dct) == pydict


class PyDictTests(MutableMappingTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.dct = dict(values)

    def get_copy(self):
        return self.dct.copy()

# class TestPyDictIntStr    (PyDictTests, values=kv_pairs_1): pass
# class TestPyDictIntNullStr(PyDictTests, values=kv_pairs_2): pass
# class TestPyDictStrInt    (PyDictTests, values=kv_pairs_3): pass
# class TestPyDictStrNullInt(PyDictTests, values=kv_pairs_4): pass


def make_cs_dictionary(cs_ktype, cs_vtype, values):
    dct = Gen.Dictionary[cs_ktype, cs_vtype]()
    for key,val in values:
        dct[key] = None if val is None else val
    return dct


class DictionaryTests(MutableMappingTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        cls.dct = make_cs_dictionary(cls.cs_ktype, cls.cs_vtype, values)

    def get_copy(self):
        return Gen.Dictionary[self.cs_ktype, self.cs_vtype](self.dct)

class TestDictionaryIntStr    (DictionaryTests, values=kv_pairs_1): pass
class TestDictionaryIntNullStr(DictionaryTests, values=kv_pairs_2): pass
class TestDictionaryStrInt    (DictionaryTests, values=kv_pairs_3): pass
class TestDictionaryStrNullInt(DictionaryTests, values=kv_pairs_4): pass


class ReadOnlyDictionaryTests(MappingTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        dct = make_cs_dictionary(cls.cs_ktype, cls.cs_vtype, values)
        cls.dct = OM.ReadOnlyDictionary[cls.cs_ktype, cls.cs_vtype](dct)

class ReadOnlyDictionaryIntStr    (ReadOnlyDictionaryTests, values=kv_pairs_1): pass
class ReadOnlyDictionaryIntNullStr(ReadOnlyDictionaryTests, values=kv_pairs_2): pass
class ReadOnlyDictionaryStrInt    (ReadOnlyDictionaryTests, values=kv_pairs_3): pass
class ReadOnlyDictionaryStrNullInt(ReadOnlyDictionaryTests, values=kv_pairs_4): pass


class ImmutableDictionaryTests(MappingTests):
    def __init_subclass__(cls, /, values, **kwargs):
        super().__init_subclass__(values=values, **kwargs)
        dct = make_cs_dictionary(cls.cs_ktype, cls.cs_vtype, values)
        cls.dct = Imm.ImmutableDictionary.ToImmutableDictionary[cls.cs_ktype, cls.cs_vtype](dct)

class TestImmutableDictionaryIntStr    (ImmutableDictionaryTests, values=kv_pairs_1): pass
class TestImmutableDictionaryIntNullStr(ImmutableDictionaryTests, values=kv_pairs_2): pass
class TestImmutableDictionaryStrInt    (ImmutableDictionaryTests, values=kv_pairs_3): pass
class TestImmutableDictionaryStrNullInt(ImmutableDictionaryTests, values=kv_pairs_4): pass
