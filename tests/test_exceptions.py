# -*- coding: utf-8 -*-

"""Test exception support."""

import sys

import System
import pytest
import pickle

# begin code from https://utcc.utoronto.ca/~cks/space/blog/python/GetAllObjects
import gc
# Recursively expand slist's objects
# into olist, using seen to track
# already processed objects.

def _getr(slist, olist, seen):
    for e in slist:
      if id(e) in seen:
        continue
      seen[id(e)] = None
      olist.append(e)
      tl = gc.get_referents(e)
      if tl:
        _getr(tl, olist, seen)

# The public function.
def get_all_objects():
    gcl = gc.get_objects()
    olist = []
    seen = {}
    # Just in case:
    seen[id(gcl)] = None
    seen[id(olist)] = None
    seen[id(seen)] = None
    # _getr does the real work.
    _getr(gcl, olist, seen)
    return olist
# end code from https://utcc.utoronto.ca/~cks/space/blog/python/GetAllObjects

def leak_check(func):
    def do_leak_check():
        func()
        gc.collect()
        exc = {x for x in get_all_objects() if isinstance(x, Exception) and not isinstance(x, pytest.PytestDeprecationWarning)}
        print(len(exc))
        if len(exc):
            for x in exc:
                print('-------')
                print(repr(x))
                print(gc.get_referrers(x))
                print(len(gc.get_referrers(x)))
            assert False
    gc.collect()
    return do_leak_check

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


def test_python_compat_of_managed_exceptions():
    """Test managed exceptions compatible with Python's implementation"""
    from System import OverflowException
    msg = "Simple message"

    e = OverflowException(msg)
    assert str(e) == msg

    assert e.args == (msg,)
    assert isinstance(e.args, tuple)
    strexp = "OverflowException('Simple message"
    assert repr(e)[:len(strexp)] == strexp


def test_exception_is_instance_of_system_object():
    """Test behavior of isinstance(<managed exception>, System.Object)."""
    # This is an anti-test, in that this is a caveat of the current
    # implementation. Because exceptions are not allowed to be new-style
    # classes, we wrap managed exceptions in a general-purpose old-style
    # class that delegates to the wrapped object. This makes _almost_
    # everything work as expected, except that an isinstance check against
    # System.Object will fail for a managed exception (because a new
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

def test_iteration_exception():
    from Python.Test import ExceptionTest
    from System import OverflowException

    exception = OverflowException("error")

    val = ExceptionTest.ThrowExceptionInIterator(exception).__iter__()
    assert next(val) == 1
    assert next(val) == 2
    with pytest.raises(OverflowException) as cm:
        next(val)

    exc = cm.value

    assert exc == exception

    # after exception is thrown iterator is no longer valid
    with pytest.raises(StopIteration):
        next(val)


def test_iteration_innerexception():
    from Python.Test import ExceptionTest
    from System import OverflowException

    exception = System.Exception("message", OverflowException("error"))

    val = ExceptionTest.ThrowExceptionInIterator(exception).__iter__()
    assert next(val) == 1
    assert next(val) == 2
    with pytest.raises(OverflowException) as cm:
        next(val)

    exc = cm.value

    assert exc == exception.InnerException

    # after exception is thrown iterator is no longer valid
    with pytest.raises(StopIteration):
        next(val)

def leak_test(func):
    def do_test_leak():
        # PyTest leaks things, gather the current state
        orig_exc = {x for x in get_all_objects() if isinstance(x, Exception)}
        func()
        exc = {x for x in get_all_objects() if isinstance(x, Exception)}
        possibly_leaked = exc - orig_exc
        assert not possibly_leaked

    return do_test_leak

@leak_test
def test_dont_leak_exceptions_simple():
    from Python.Test import ExceptionTest

    try:
        ExceptionTest.DoThrowSimple()
    except System.ArgumentException:
        print('type error, as expected')

@leak_test
def test_dont_leak_exceptions_inner():
    from Python.Test import ExceptionTest
    try:
        ExceptionTest.DoThrowWithInner()
    except TypeError:
        print('type error, as expected')
    except System.ArgumentException:
        print('type error, also expected')