# -*- coding: utf-8 -*-

"""Test clrmethod and clrproperty support for calling methods and getting/setting python properties from CLR."""

import Python.Test as Test
import System
import pytest
import clr

class ExampleClrClass(System.Object):
    __namespace__ = "PyTest"
    def __init__(self):
        self._x = 3
    @clr.clrmethod(int, [int])
    def test(self, x):
        return x*2
    
    def get_X(self):
        return self._x
    def set_X(self, value):
        self._x = value
    X = clr.clrproperty(int, get_X, set_X)

    @clr.clrproperty(int)
    def Y(self):
        return self._x * 2

def test_set_and_get_property_from_py():
    """Test setting and getting clr-accessible properties from python."""
    t = ExampleClrClass()
    assert t.X == 3
    assert t.Y == 3 * 2
    t.X = 4
    assert t.X == 4
    assert t.Y == 4 * 2

def test_set_and_get_property_from_clr():
    """Test setting and getting clr-accessible properties from the clr."""
    t = ExampleClrClass()
    assert t.GetType().GetProperty("X").GetValue(t) == 3
    assert t.GetType().GetProperty("Y").GetValue(t) == 3 * 2
    t.GetType().GetProperty("X").SetValue(t, 4)
    assert t.GetType().GetProperty("X").GetValue(t) == 4
    assert t.GetType().GetProperty("Y").GetValue(t) == 4 * 2


def test_set_and_get_property_from_clr_and_py():
    """Test setting and getting clr-accessible properties alternatingly from the clr and from python."""
    t = ExampleClrClass()
    assert t.GetType().GetProperty("X").GetValue(t) == 3
    assert t.GetType().GetProperty("Y").GetValue(t) == 3 * 2
    assert t.X == 3
    assert t.Y == 3 * 2
    t.GetType().GetProperty("X").SetValue(t, 4)
    assert t.GetType().GetProperty("X").GetValue(t) == 4
    assert t.GetType().GetProperty("Y").GetValue(t) == 4 * 2
    assert t.X == 4
    assert t.Y == 4 * 2
    t.X = 5
    assert t.GetType().GetProperty("X").GetValue(t) == 5
    assert t.GetType().GetProperty("Y").GetValue(t) == 5 * 2
    assert t.X == 5
    assert t.Y == 5 * 2

def test_method_invocation_from_py():
    """Test calling a clr-accessible method from python."""
    t = ExampleClrClass()
    assert t.test(41) == 41*2

def test_method_invocation_from_clr():
    """Test calling a clr-accessible method from the clr."""
    t = ExampleClrClass()
    assert t.GetType().GetMethod("test").Invoke(t, [37]) == 37*2
