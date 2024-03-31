# -*- coding: utf-8 -*-

"""Test __repr__ output"""

import System
import pytest
from Python.Test import ReprTest


def test_basic():
    """Test Point class which implements both ToString and __repr__ without inheritance"""
    ob = ReprTest.Point(1, 2)
    # point implements ToString() and __repr__()
    assert repr(ob) == "Point(1,2)"
    assert str(ob) == "Python.Test.ReprTest+Point: X=1, Y=2"


def test_system_string():
    """Test system string"""
    ob = System.String("hello")
    assert str(ob) == "hello"
    assert "<System.String object at " in repr(ob)


def test_str_only():
    """Test class implementing ToString() but not __repr__()"""
    ob = ReprTest.Bar()
    assert str(ob) == "I implement ToString() but not __repr__()!"
    assert "<Python.Test.Bar object at " in repr(ob)


def test_hierarchy1():
    """Test inheritance hierarchy with base & middle class implementing ToString"""
    ob1 = ReprTest.BazBase()
    assert str(ob1) == "Base class implementing ToString()!"
    assert "<Python.Test.BazBase object at " in repr(ob1)

    ob2 = ReprTest.BazMiddle()
    assert str(ob2) == "Middle class implementing ToString()!"
    assert "<Python.Test.BazMiddle object at " in repr(ob2)

    ob3 = ReprTest.Baz()
    assert str(ob3) == "Middle class implementing ToString()!"
    assert "<Python.Test.Baz object at " in repr(ob3)


def test_bad_tostring():
    """Test ToString that can't be used by str()"""
    ob = ReprTest.Quux()
    assert str(ob) == "Python.Test.ReprTest+Quux"
    assert "<Python.Test.Quux object at " in repr(ob)


def test_bad_repr():
    """Test incorrect implementation of repr"""
    ob1 = ReprTest.QuuzBase()
    assert str(ob1) == "Python.Test.ReprTest+QuuzBase"
    assert "<Python.Test.QuuzBase object at " in repr(ob1)

    ob2 = ReprTest.Quuz()
    assert str(ob2) == "Python.Test.ReprTest+Quuz"
    assert "<Python.Test.Quuz object at " in repr(ob2)

    ob3 = ReprTest.Corge()
    with pytest.raises(Exception):
        ob3.__repr__()
    repr(ob3)

    ob4 = ReprTest.Grault()
    with pytest.raises(Exception):
        ob4.__repr__()
    repr(ob4)
