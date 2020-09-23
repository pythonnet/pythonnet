# -*- coding: utf-8 -*-

"""Test CLR interface support."""

import Python.Test as Test
import pytest

from .utils import DictProxyType


def test_interface_standard_attrs():
    """Test standard class attributes."""
    from Python.Test import IPublicInterface

    assert IPublicInterface.__name__ == 'IPublicInterface'
    assert IPublicInterface.__module__ == 'Python.Test'
    assert isinstance(IPublicInterface.__dict__, DictProxyType)


def test_global_interface_visibility():
    """Test visibility of module-level interfaces."""
    from Python.Test import IPublicInterface

    assert IPublicInterface.__name__ == 'IPublicInterface'

    with pytest.raises(ImportError):
        from Python.Test import IInternalInterface
        _ = IInternalInterface

    with pytest.raises(AttributeError):
        _ = Test.IInternalInterface


def test_nested_interface_visibility():
    """Test visibility of nested interfaces."""
    from Python.Test import InterfaceTest

    ob = InterfaceTest.IPublic
    assert ob.__name__ == 'IPublic'

    ob = InterfaceTest.IProtected
    assert ob.__name__ == 'IProtected'

    with pytest.raises(AttributeError):
        _ = InterfaceTest.IInternal

    with pytest.raises(AttributeError):
        _ = InterfaceTest.IPrivate


def test_explicit_cast_to_interface():
    """Test explicit cast to an interface."""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    assert type(ob).__name__ == 'InterfaceTest'
    assert hasattr(ob, 'HelloProperty')

    i1 = Test.ISayHello1(ob)
    assert type(i1).__name__ == 'ISayHello1'
    assert hasattr(i1, 'SayHello')
    assert i1.SayHello() == 'hello 1'
    assert not hasattr(i1, 'HelloProperty')

    i2 = Test.ISayHello2(ob)
    assert type(i2).__name__ == 'ISayHello2'
    assert i2.SayHello() == 'hello 2'
    assert hasattr(i2, 'SayHello')
    assert not hasattr(i2, 'HelloProperty')


def test_interface_method_and_property_lookup():
    """Test methods and properties in interfaces can be accessed"""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    assert hasattr(ob, 'TestMethod1')
    assert ob.TestMethod1() == 'TestMethod1'
    assert hasattr(ob, 'TestMethod2')
    assert ob.TestMethod2() == 'TestMethod2'
    assert hasattr(ob, 'TestProperty1')
    assert ob.TestProperty1 == 'TestProperty1'
    assert hasattr(ob, 'TestProperty2')
    assert ob.TestProperty2 == 'TestProperty2'

