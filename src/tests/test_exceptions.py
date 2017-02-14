# -*- coding: utf-8 -*-

"""Test exception support."""

import sys

import System
import pytest

from ._compat import PY2, PY3, pickle, text_type


def test_unified_exception_semantics():
    """Test unified exception semantics."""
    e = System.Exception('Something bad happened')
    assert isinstance(e, Exception)
    assert isinstance(e, System.Exception)


def test_standard_exception_attributes():
    """Test accessing standard exception attributes."""
    from System import OverflowException
    from Python.Test import ExceptionTest

    e = ExceptionTest.GetExplicitException()
    assert isinstance(e, OverflowException)

    assert e.Message == 'error'

    e.Source = 'Test Suite'
    assert e.Source == 'Test Suite'

    v = e.ToString()
    assert len(v) > 0


def test_extended_exception_attributes():
    """Test accessing extended exception attributes."""
    from Python.Test import ExceptionTest, ExtendedException
    from System import OverflowException

    e = ExceptionTest.GetExtendedException()
    assert isinstance(e, ExtendedException)
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    assert e.Message == 'error'

    e.Source = 'Test Suite'
    assert e.Source == 'Test Suite'

    v = e.ToString()
    assert len(v) > 0

    assert e.ExtraProperty == 'extra'
    e.ExtraProperty = 'changed'
    assert e.ExtraProperty == 'changed'

    assert e.GetExtraInfo() == 'changed'


def test_raise_class_exception():
    """Test class exception propagation."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException

    exc = cm.value
    assert isinstance(exc, NullReferenceException)


def test_exc_info():
    """Test class exception propagation.
    Behavior of exc_info changed in Py3. Refactoring its test"""
    from System import NullReferenceException
    try:
        raise NullReferenceException("message")
    except Exception as exc:
        type_, value, tb = sys.exc_info()
        assert type_ is NullReferenceException
        assert value.Message == "message"
        assert exc.Message == "message"
        # FIXME: Lower-case message isn't implemented
        # self.assertTrue(exc.message == "message")
        assert value is exc


def test_raise_class_exception_with_value():
    """Test class exception propagation with associated value."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException('Aiiieee!')

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert exc.Message == 'Aiiieee!'


def test_raise_instance_exception():
    """Test instance exception propagation."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException()

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert len(exc.Message) > 0


def test_raise_instance_exception_with_args():
    """Test instance exception propagation with args."""
    from System import NullReferenceException

    with pytest.raises(NullReferenceException) as cm:
        raise NullReferenceException("Aiiieee!")

    exc = cm.value
    assert isinstance(exc, NullReferenceException)
    assert exc.Message == 'Aiiieee!'


def test_managed_exception_propagation():
    """Test propagation of exceptions raised in managed code."""
    from System import Decimal, OverflowException

    with pytest.raises(OverflowException):
        Decimal.ToInt64(Decimal.MaxValue)


def test_managed_exception_conversion():
    """Test conversion of managed exceptions."""
    from System import OverflowException
    from Python.Test import ExceptionTest

    e = ExceptionTest.GetBaseException()
    assert isinstance(e, System.Exception)

    e = ExceptionTest.GetExplicitException()
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    e = ExceptionTest.GetWidenedException()
    assert isinstance(e, OverflowException)
    assert isinstance(e, System.Exception)

    v = ExceptionTest.SetBaseException(System.Exception('error'))
    assert v

    v = ExceptionTest.SetExplicitException(OverflowException('error'))
    assert v

    v = ExceptionTest.SetWidenedException(OverflowException('error'))
    assert v


def test_catch_exception_from_managed_method():
    """Test catching an exception from a managed method."""
    from Python.Test import ExceptionTest
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        ExceptionTest().ThrowException()

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_from_managed_property():
    """Test catching an exception from a managed property."""
    from Python.Test import ExceptionTest
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        _ = ExceptionTest().ThrowProperty

    e = cm.value
    assert isinstance(e, OverflowException)

    with pytest.raises(OverflowException) as cm:
        ExceptionTest().ThrowProperty = 1

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_managed_class():
    """Test catching the managed class of an exception."""
    from System import OverflowException

    with pytest.raises(OverflowException):
        raise OverflowException('overflow')


def test_catch_exception_python_class():
    """Test catching the python class of an exception."""
    from System import OverflowException

    with pytest.raises(Exception):
        raise OverflowException('overflow')


def test_catch_exception_base_class():
    """Test catching the base of an exception."""
    from System import OverflowException, ArithmeticException

    with pytest.raises(ArithmeticException):
        raise OverflowException('overflow')


def test_catch_exception_nested_base_class():
    """Test catching the nested base of an exception."""
    from System import OverflowException, SystemException

    with pytest.raises(SystemException):
        raise OverflowException('overflow')


def test_catch_exception_with_assignment():
    """Test catching an exception with assignment."""
    from System import OverflowException

    with pytest.raises(OverflowException) as cm:
        raise OverflowException('overflow')

    e = cm.value
    assert isinstance(e, OverflowException)


def test_catch_exception_unqualified():
    """Test catching an unqualified exception."""
    from System import OverflowException

    try:
        raise OverflowException('overflow')
    except:
        pass
    else:
        self.fail("failed to catch unqualified exception")


def test_catch_baseexception():
    """Test catching an unqualified exception with BaseException."""
    from System import OverflowException

    with pytest.raises(BaseException):
        raise OverflowException('overflow')


def test_apparent_module_of_exception():
    """Test the apparent module of an exception."""
    from System import OverflowException

    assert System.Exception.__module__ == 'System'
    assert OverflowException.__module__ == 'System'


def test_str_of_exception():
    """Test the str() representation of an exception."""
    from System import NullReferenceException, Convert, FormatException

    e = NullReferenceException('')
    assert str(e) == ''

    e = NullReferenceException('Something bad happened')
    assert str(e).startswith('Something bad happened')

    with pytest.raises(FormatException) as cm:
        Convert.ToDateTime('this will fail')

    e = cm.value
    # fix for international installation
    msg = text_type(e).encode("utf8")
    fnd = text_type('System.Convert.ToDateTime').encode("utf8")
    assert msg.find(fnd) > -1, msg


def test_python_compat_of_managed_exceptions():
    """Test managed exceptions compatible with Python's implementation"""
    from System import OverflowException
    msg = "Simple message"

    e = OverflowException(msg)
    assert str(e) == msg
    assert text_type(e) == msg

    assert e.args == (msg,)
    assert isinstance(e.args, tuple)
    if PY3:
        assert repr(e) == "OverflowException('Simple message',)"
    elif PY2:
        assert repr(e) == "OverflowException(u'Simple message',)"


def test_exception_is_instance_of_system_object():
    """Test behavior of isinstance(<managed exception>, System.Object)."""
    # This is an anti-test, in that this is a caveat of the current
    # implementation. Because exceptions are not allowed to be new-style
    # classes, we wrap managed exceptions in a general-purpose old-style
    # class that delegates to the wrapped object. This makes _almost_
    # everything work as expected, except that an isinstance check against
    # CLR.System.Object will fail for a managed exception (because a new
    # style class cannot appear in the __bases__ of an old-style class
    # without causing a crash in the CPython interpreter). This test is
    # here mainly to remind me to update the caveat in the documentation
    # one day when when exceptions can be new-style classes.

    # This behavior is now over-shadowed by the implementation of
    # __instancecheck__ (i.e., overloading isinstance), so for all Python
    # version >= 2.6 we expect isinstance(<managed exception>, Object) to
    # be true, even though it does not really subclass Object.
    from System import OverflowException, Object

    o = OverflowException('error')

    if sys.version_info >= (2, 6):
        assert isinstance(o, Object)
    else:
        assert not isinstance(o, Object)


def test_pickling_exceptions():
    exc = System.Exception("test")
    dumped = pickle.dumps(exc)
    loaded = pickle.loads(dumped)

    assert exc.args == loaded.args


@pytest.mark.skipif(PY2, reason="__cause__ isn't implemented in PY2")
def test_chained_exceptions():
    from Python.Test import ExceptionTest

    with pytest.raises(Exception) as cm:
        ExceptionTest.ThrowChainedExceptions()

    exc = cm.value

    msgs = ("Outer exception",
            "Inner exception",
            "Innermost exception",)
    for msg in msgs:
        assert exc.Message == msg
        assert exc.__cause__ == exc.InnerException
        exc = exc.__cause__
