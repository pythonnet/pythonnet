# -*- coding: utf-8 -*-

"""Test CLR delegate support."""

import Python.Test as Test
import System
import pytest
from Python.Test import DelegateTest, StringDelegate

from .utils import HelloClass, hello_func, MultipleHandler, DictProxyType


def test_delegate_standard_attrs():
    """Test standard delegate attributes."""
    from Python.Test import PublicDelegate

    assert PublicDelegate.__name__ == 'PublicDelegate'
    assert PublicDelegate.__module__ == 'Python.Test'
    assert isinstance(PublicDelegate.__dict__, DictProxyType)
    assert PublicDelegate.__doc__ is None


def test_global_delegate_visibility():
    """Test visibility of module-level delegates."""
    from Python.Test import PublicDelegate

    assert PublicDelegate.__name__ == 'PublicDelegate'
    assert Test.PublicDelegate.__name__ == 'PublicDelegate'

    with pytest.raises(ImportError):
        from Python.Test import InternalDelegate
        _ = InternalDelegate

    with pytest.raises(AttributeError):
        _ = Test.InternalDelegate


def test_nested_delegate_visibility():
    """Test visibility of nested delegates."""
    ob = DelegateTest.PublicDelegate
    assert ob.__name__ == 'PublicDelegate'

    ob = DelegateTest.ProtectedDelegate
    assert ob.__name__ == 'ProtectedDelegate'

    with pytest.raises(AttributeError):
        _ = DelegateTest.InternalDelegate

    with pytest.raises(AttributeError):
        _ = DelegateTest.PrivateDelegate


def test_delegate_from_function():
    """Test delegate implemented with a Python function."""

    d = StringDelegate(hello_func)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_method():
    """Test delegate implemented with a Python instance method."""

    inst = HelloClass()
    d = StringDelegate(inst.hello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_unbound_method():
    """Test failure mode for unbound methods."""

    with pytest.raises(TypeError):
        d = StringDelegate(HelloClass.hello)
        d()


def test_delegate_from_static_method():
    """Test delegate implemented with a Python static method."""

    d = StringDelegate(HelloClass.s_hello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"

    inst = HelloClass()
    d = StringDelegate(inst.s_hello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_class_method():
    """Test delegate implemented with a Python class method."""

    d = StringDelegate(HelloClass.c_hello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"

    inst = HelloClass()
    d = StringDelegate(inst.c_hello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_callable():
    """Test delegate implemented with a Python callable object."""

    inst = HelloClass()
    d = StringDelegate(inst)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_managed_instance_method():
    """Test delegate implemented with a managed instance method."""
    ob = DelegateTest()
    d = StringDelegate(ob.SayHello)

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_managed_static_method():
    """Test delegate implemented with a managed static method."""
    d = StringDelegate(DelegateTest.StaticSayHello)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d) == "hello"
    assert d() == "hello"

    ob.stringDelegate = d
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_from_delegate():
    """Test delegate implemented with another delegate."""
    d1 = StringDelegate(hello_func)
    d2 = StringDelegate(d1)
    ob = DelegateTest()

    assert ob.CallStringDelegate(d2) == "hello"
    assert d2() == "hello"

    ob.stringDelegate = d2
    assert ob.CallStringDelegate(ob.stringDelegate) == "hello"
    assert ob.stringDelegate() == "hello"


def test_delegate_with_invalid_args():
    """Test delegate instantiation with invalid (non-callable) args."""

    with pytest.raises(TypeError):
        _ = StringDelegate(None)

    with pytest.raises(TypeError):
        _ = StringDelegate("spam")

    with pytest.raises(TypeError):
        _ = StringDelegate(1)


def test_multicast_delegate():
    """Test multicast delegates."""

    inst = MultipleHandler()
    d1 = StringDelegate(inst.count)
    d2 = StringDelegate(inst.count)

    md = System.Delegate.Combine(d1, d2)
    ob = DelegateTest()

    assert ob.CallStringDelegate(md) == "ok"
    assert inst.value == 2

    assert md() == "ok"
    assert inst.value == 4


def test_subclass_delegate_fails():
    """Test that subclassing of a delegate type fails."""
    from Python.Test import PublicDelegate

    with pytest.raises(TypeError):
        class Boom(PublicDelegate):
            pass

        _ = Boom


def test_delegate_equality():
    """Test delegate equality."""

    d = StringDelegate(hello_func)
    ob = DelegateTest()
    ob.stringDelegate = d
    assert ob.stringDelegate == d


def test_bool_delegate():
    """Test boolean delegate."""
    from Python.Test import BoolDelegate

    def always_so_negative():
        return False

    d = BoolDelegate(always_so_negative)
    ob = DelegateTest()
    ob.CallBoolDelegate(d)

    assert not d()
    assert not ob.CallBoolDelegate(d)

    def always_so_positive():
        return 1
    bad = BoolDelegate(always_so_positive)
    with pytest.raises(TypeError):
        ob.CallBoolDelegate(bad)

def test_object_delegate():
    """Test object delegate."""
    from Python.Test import ObjectDelegate

    def create_object():
        return DelegateTest()

    d = ObjectDelegate(create_object)
    ob = DelegateTest()
    ob.CallObjectDelegate(d)

def test_invalid_object_delegate():
    """Test invalid object delegate with mismatched return type."""
    from Python.Test import ObjectDelegate

    d = ObjectDelegate(hello_func)
    ob = DelegateTest()
    with pytest.raises(TypeError):
        ob.CallObjectDelegate(d)

def test_out_int_delegate():
    """Test delegate with an out int parameter."""
    from Python.Test import OutIntDelegate
    value = 7

    def out_hello_func(ignored):
        return 5

    d = OutIntDelegate(out_hello_func)
    result = d(value)
    assert result == 5

    ob = DelegateTest()
    result = ob.CallOutIntDelegate(d, value)
    assert result == 5

    def invalid_handler(ignored):
        return '5'

    d = OutIntDelegate(invalid_handler)
    with pytest.raises(TypeError):
        result = d(value)

def test_out_string_delegate():
    """Test delegate with an out string parameter."""
    from Python.Test import OutStringDelegate
    value = 'goodbye'

    def out_hello_func(ignored):
        return 'hello'

    d = OutStringDelegate(out_hello_func)
    result = d(value)
    assert result == 'hello'

    ob = DelegateTest()
    result = ob.CallOutStringDelegate(d, value)
    assert result == 'hello'

def test_ref_int_delegate():
    """Test delegate with a ref string parameter."""
    from Python.Test import RefIntDelegate
    value = 7

    def ref_hello_func(data):
        assert data == value
        return data + 1

    d = RefIntDelegate(ref_hello_func)
    result = d(value)
    assert result == value + 1

    ob = DelegateTest()
    result = ob.CallRefIntDelegate(d, value)
    assert result == value + 1

def test_ref_string_delegate():
    """Test delegate with a ref string parameter."""
    from Python.Test import RefStringDelegate
    value = 'goodbye'

    def ref_hello_func(data):
        assert data == value
        return 'hello'

    d = RefStringDelegate(ref_hello_func)
    result = d(value)
    assert result == 'hello'

    ob = DelegateTest()
    result = ob.CallRefStringDelegate(d, value)
    assert result == 'hello'

def test_ref_int_ref_string_delegate():
    """Test delegate with a ref int and ref string parameter."""
    from Python.Test import RefIntRefStringDelegate
    intData = 7
    stringData = 'goodbye'

    def ref_hello_func(intValue, stringValue):
        assert intData == intValue
        assert stringData == stringValue
        return (intValue + 1, stringValue + '!')

    d = RefIntRefStringDelegate(ref_hello_func)
    result = d(intData, stringData)
    assert result == (intData + 1, stringData + '!')

    ob = DelegateTest()
    result = ob.CallRefIntRefStringDelegate(d, intData, stringData)
    assert result == (intData + 1, stringData + '!')

    def not_a_tuple(intValue, stringValue):
        return 'a'

    d = RefIntRefStringDelegate(not_a_tuple)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    def short_tuple(intValue, stringValue):
        return (5,)

    d = RefIntRefStringDelegate(short_tuple)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    def long_tuple(intValue, stringValue):
        return (5, 'a', 'b')

    d = RefIntRefStringDelegate(long_tuple)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    def wrong_tuple_item(intValue, stringValue):
        return ('a', 'b')

    d = RefIntRefStringDelegate(wrong_tuple_item)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

def test_int_ref_int_ref_string_delegate():
    """Test delegate with a ref int and ref string parameter."""
    from Python.Test import IntRefIntRefStringDelegate
    intData = 7
    stringData = 'goodbye'

    def ref_hello_func(intValue, stringValue):
        assert intData == intValue
        assert stringData == stringValue
        return (intValue + len(stringValue), intValue + 1, stringValue + '!')

    d = IntRefIntRefStringDelegate(ref_hello_func)
    result = d(intData, stringData)
    assert result == (intData + len(stringData), intData + 1, stringData + '!')

    ob = DelegateTest()
    result = ob.CallIntRefIntRefStringDelegate(d, intData, stringData)
    assert result == (intData + len(stringData), intData + 1, stringData + '!')

    def not_a_tuple(intValue, stringValue):
        return 'a'

    d = IntRefIntRefStringDelegate(not_a_tuple)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    def short_tuple(intValue, stringValue):
        return (5,)

    d = IntRefIntRefStringDelegate(short_tuple)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    def wrong_return_type(intValue, stringValue):
        return ('a', 7, 'b')

    d = IntRefIntRefStringDelegate(wrong_return_type)
    with pytest.raises(TypeError):
        result = d(intData, stringData)

    # test async delegates

    # test multicast delegates

    # test explicit op_

    # test sig mismatch, both on managed and Python side

    # test return wrong type
