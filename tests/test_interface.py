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
    assert i1.__implementation__ == ob
    assert i1.__raw_implementation__ == ob

    i2 = Test.ISayHello2(ob)
    assert type(i2).__name__ == 'ISayHello2'
    assert i2.SayHello() == 'hello 2'
    assert hasattr(i2, 'SayHello')
    assert not hasattr(i2, 'HelloProperty')


def test_interface_object_returned_through_method():
    """Test concrete type is used if method return type is interface"""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    hello1 = ob.GetISayHello1()
    assert type(hello1).__name__ == 'InterfaceTest'

    # This doesn't work yet
    # assert hello1.SayHello() == 'hello 1'


def test_interface_object_returned_through_out_param():
    """Test interface type is used for out parameters of interface types"""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    hello2 = ob.GetISayHello2(None)
    assert type(hello2).__name__ == 'InterfaceTest'

    # This doesn't work yet
    # assert hello2.SayHello() == 'hello 2'

def test_interface_out_param_python_impl():
    from Python.Test import IOutArg, OutArgCaller

    class MyOutImpl(IOutArg):
        __namespace__ = "Python.Test"

        def MyMethod_Out(self, name, index):
            other_index = 101
            return ('MyName', other_index)

    py_impl = MyOutImpl()

    assert 101 == OutArgCaller.CallMyMethod_Out(py_impl)


def test_null_interface_object_returned():
    """Test None is used also for methods with interface return types"""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    hello1, hello2 = ob.GetNoSayHello(None)
    assert hello1 is None
    assert hello2 is None

def test_interface_array_returned():
    """Test concrete type used for methods returning interface arrays"""
    from Python.Test import InterfaceTest

    ob = InterfaceTest()
    hellos = ob.GetISayHello1Array()
    assert type(hellos[0]).__name__ == 'InterfaceTest'

def test_implementation_access():
    """Test the __implementation__ and __raw_implementation__ properties"""
    import System
    clrVal =  System.Int32(100)
    i = System.IComparable(clrVal)
    assert 100 == i.__implementation__
    assert clrVal == i.__raw_implementation__
    assert i.__implementation__ != i.__raw_implementation__


def test_interface_collection_iteration():
    """Test concrete type is used when iterating over interface collection"""
    import System
    from System.Collections.Generic import List
    elem = System.IComparable(System.Int32(100))
    typed_list = List[System.IComparable]()
    typed_list.Add(elem)
    for e in typed_list:
        assert type(e).__name__ == "int"

    untyped_list = System.Collections.ArrayList()
    untyped_list.Add(elem)
    for e in untyped_list:
        assert type(e).__name__ == "int"


def test_methods_of_Object_are_available():
    """Test calling methods inherited from Object"""
    import System
    clrVal =  System.Int32(100)
    i = System.IComparable(clrVal)
    assert i.Equals(clrVal)
    assert clrVal.GetHashCode() == i.GetHashCode()
    assert clrVal.GetType() == i.GetType()
    assert clrVal.ToString() == i.ToString()
