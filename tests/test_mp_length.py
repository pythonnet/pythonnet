# -*- coding: utf-8 -*-

"""Test __len__ for .NET classes implementing ICollection/ICollection<T>."""

from Python.Test import (
    MpLengthCollectionTest,
    MpLengthExplicitCollectionTest,
    MpLengthGenericCollectionTest,
    MpLengthExplicitGenericCollectionTest,
)


def test_simple___len__():
    """Test __len__ for simple ICollection implementers"""
    import System.Collections.Generic

    lst = System.Collections.Generic.List[int]()
    assert len(lst) == 0
    lst.Add(5)
    lst.Add(6)
    assert len(lst) == 2

    dct = System.Collections.Generic.Dictionary[int, int]()
    assert len(dct) == 0
    dct.Add(4, 5)
    assert len(dct) == 1

    arr = System.Array[int]([0, 1, 2, 3])
    assert len(arr) == 4


def test_custom_collection___len__():
    """Test __len__ for custom collection class"""
    s = MpLengthCollectionTest()
    assert len(s) == 3


def test_custom_collection_explicit___len__():
    """Test __len__ for custom collection class that explicitly implements ICollection"""
    s = MpLengthExplicitCollectionTest()
    assert len(s) == 2


def test_custom_generic_collection___len__():
    """Test __len__ for custom generic collection class"""
    s = MpLengthGenericCollectionTest[int]()
    s.Add(1)
    s.Add(2)
    assert len(s) == 2


def test_custom_generic_collection_explicit___len__():
    """Test __len__ for custom generic collection that explicity implements ICollection<T>"""
    s = MpLengthExplicitGenericCollectionTest[int]()
    s.Add(1)
    s.Add(10)
    assert len(s) == 2


def test_len_through_interface_generic():
    """Test __len__ for ICollection<T>"""
    import System.Collections.Generic

    lst = System.Collections.Generic.List[int]()
    coll = System.Collections.Generic.ICollection[int](lst)
    assert len(coll) == 0


def test_len_through_interface():
    """Test __len__ for ICollection"""
    import System.Collections

    lst = System.Collections.ArrayList()
    coll = System.Collections.ICollection(lst)
    assert len(coll) == 0
