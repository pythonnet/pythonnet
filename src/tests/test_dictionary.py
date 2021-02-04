# -*- coding: utf-8 -*-

"""Test support for managed dictionaries."""

import Python.Test as Test
import System
import pytest


def test_public_dict():
    """Test public dict."""
    ob = Test.PublicDictionaryTest()
    items = ob.items

    assert len(items) == 5

    assert items['0'] == 0
    assert items['4'] == 4

    items['0'] = 8
    assert items['0'] == 8

    items['4'] = 9
    assert items['4'] == 9

    items['-4'] = 0
    assert items['-4'] == 0

    items['-1'] = 4
    assert items['-1'] == 4

def test_protected_dict():
    """Test protected dict."""
    ob = Test.ProtectedDictionaryTest()
    items = ob.items

    assert len(items) == 5

    assert items['0'] == 0
    assert items['4'] == 4

    items['0'] = 8
    assert items['0'] == 8

    items['4'] = 9
    assert items['4'] == 9

    items['-4'] = 0
    assert items['-4'] == 0

    items['-1'] = 4
    assert items['-1'] == 4

def test_internal_dict():
    """Test internal dict."""

    with pytest.raises(AttributeError):
        ob = Test.InternalDictionaryTest()
        _ = ob.items

def test_private_dict():
    """Test private dict."""

    with pytest.raises(AttributeError):
        ob = Test.PrivateDictionaryTest()
        _ = ob.items

def test_dict_contains():
    """Test dict support for __contains__."""

    ob = Test.PublicDictionaryTest()
    items = ob.items

    assert '0' in items
    assert '1' in items
    assert '2' in items
    assert '3' in items
    assert '4' in items

    assert not ('5' in items)
    assert not ('-1' in items)

def test_dict_abuse():
    """Test dict abuse."""
    _class = Test.PublicDictionaryTest
    ob = Test.PublicDictionaryTest()

    with pytest.raises(AttributeError):
        del _class.__getitem__

    with pytest.raises(AttributeError):
        del ob.__getitem__

    with pytest.raises(AttributeError):
        del _class.__setitem__

    with pytest.raises(AttributeError):
        del ob.__setitem__

    with pytest.raises(TypeError):
        Test.PublicArrayTest.__getitem__(0, 0)

    with pytest.raises(TypeError):
        Test.PublicArrayTest.__setitem__(0, 0, 0)

    with pytest.raises(TypeError):
        desc = Test.PublicArrayTest.__dict__['__getitem__']
        desc(0, 0)

    with pytest.raises(TypeError):
        desc = Test.PublicArrayTest.__dict__['__setitem__']
        desc(0, 0, 0)

def test_InheritedDictionary():
    """Test class that inherited from IDictionary."""
    items = Test.InheritedDictionaryTest()

    assert len(items) == 5

    assert items['0'] == 0
    assert items['4'] == 4

    items['0'] = 8
    assert items['0'] == 8

    items['4'] = 9
    assert items['4'] == 9

    items['-4'] = 0
    assert items['-4'] == 0

    items['-1'] = 4
    assert items['-1'] == 4

def test_InheritedDictionary_contains():
    """Test dict support for __contains__ in class that inherited from IDictionary"""
    items = Test.InheritedDictionaryTest()

    assert '0' in items
    assert '1' in items
    assert '2' in items
    assert '3' in items
    assert '4' in items

    assert not ('5' in items)
    assert not ('-1' in items) 
