# -*- coding: utf-8 -*-

"""Test CLR <-> Python type conversions."""

from __future__ import unicode_literals
import System
import pytest
from Python.Test import MpLengthCollectionTest, MpLengthExplicitCollectionTest, MpLengthGenericCollectionTest, MpLengthExplicitGenericCollectionTest

from ._compat import indexbytes, long, unichr, text_type, PY2, PY3

def test_simple___len__():
    """Test __len__ for simple ICollection implementers"""
    import System
    import System.Collections.Generic
    l = System.Collections.Generic.List[int]()
    assert len(l) == 0
    l.Add(5)
    l.Add(6)
    assert len(l) == 2

    d = System.Collections.Generic.Dictionary[int, int]()
    assert len(d) == 0
    d.Add(4, 5)
    assert len(d) == 1

    a = System.Array[int]([0,1,2,3])
    assert len(a) == 4

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
