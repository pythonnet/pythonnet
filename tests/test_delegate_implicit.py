# -*- coding: utf-8 -*-

"""Test CLR delegate support."""

import Python.Test as Test
import System
import pytest
from Python.Test import DelegateTest, StringDelegate

from .utils import HelloClass, hello_func, MultipleHandler, DictProxyType


def test_delegate_implicit_from_function():
    """Test delegate implemented with a Python function."""

    d = hello_func
    assert d() == "hello"

    ob = DelegateTest()
    assert ob.CallStringDelegate(d) == "hello"


def test_delegate_implicit_from_method():
    """Test delegate implemented with a Python instance method."""

    inst = HelloClass()
    d = inst.hello
    assert d() == "hello"

    ob = DelegateTest()
    assert ob.CallStringDelegate(d) == "hello"


def test_delegate_implicit_from_static_method():
    """Test delegate implemented with a Python static method."""

    d = HelloClass.s_hello
    assert d() == "hello"

    ob = DelegateTest()
    assert ob.CallStringDelegate(d) == "hello"


def test_delegate_implicit_from_class_method():
    """Test delegate implemented with a Python class method."""

    d = HelloClass.c_hello
    assert d() == "hello"

    ob = DelegateTest()
    assert ob.CallStringDelegate(d) == "hello"


def test_delegate_from_callable():
    """Test delegate implemented with a Python callable object."""

    inst = HelloClass()
    d = inst
    assert d() == "hello"

    ob = DelegateTest()
    assert ob.CallStringDelegate(d) == "hello"
