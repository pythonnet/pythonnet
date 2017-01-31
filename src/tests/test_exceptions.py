# -*- coding: utf-8 -*-

import sys
import unittest

import System

from _compat import PY2, PY3, pickle, text_type


class ExceptionTests(unittest.TestCase):
    """Test exception support."""

    def test_unified_exception_semantics(self):
        """Test unified exception semantics."""
        e = System.Exception('Something bad happened')
        self.assertTrue(isinstance(e, Exception))
        self.assertTrue(isinstance(e, System.Exception))

    def test_standard_exception_attributes(self):
        """Test accessing standard exception attributes."""
        from System import OverflowException
        from Python.Test import ExceptionTest

        e = ExceptionTest.GetExplicitException()
        self.assertTrue(isinstance(e, OverflowException))

        self.assertTrue(e.Message == 'error')

        e.Source = 'Test Suite'
        self.assertTrue(e.Source == 'Test Suite')

        v = e.ToString()
        self.assertTrue(len(v) > 0)

    def test_extended_exception_attributes(self):
        """Test accessing extended exception attributes."""
        from Python.Test import ExceptionTest, ExtendedException
        from System import OverflowException

        e = ExceptionTest.GetExtendedException()
        self.assertTrue(isinstance(e, ExtendedException))
        self.assertTrue(isinstance(e, OverflowException))
        self.assertTrue(isinstance(e, System.Exception))

        self.assertTrue(e.Message == 'error')

        e.Source = 'Test Suite'
        self.assertTrue(e.Source == 'Test Suite')

        v = e.ToString()
        self.assertTrue(len(v) > 0)

        self.assertTrue(e.ExtraProperty == 'extra')
        e.ExtraProperty = 'changed'
        self.assertTrue(e.ExtraProperty == 'changed')

        self.assertTrue(e.GetExtraInfo() == 'changed')

    def test_raise_class_exception(self):
        """Test class exception propagation."""
        from System import NullReferenceException

        with self.assertRaises(NullReferenceException) as cm:
            raise NullReferenceException

        exc = cm.exception
        self.assertTrue(isinstance(exc, NullReferenceException))

    def test_exc_info(self):
        """Test class exception propagation.
        Behavior of exc_info changed in Py3. Refactoring its test"""
        from System import NullReferenceException
        try:
            raise NullReferenceException("message")
        except Exception as exc:
            type_, value, tb = sys.exc_info()
            self.assertTrue(type_ is NullReferenceException)
            self.assertTrue(value.Message == "message")
            self.assertTrue(exc.Message == "message")
            # FIXME: Lower-case message isn't implemented
            # self.assertTrue(exc.message == "message")
            self.assertTrue(value is exc)

    def test_raise_class_exception_with_value(self):
        """Test class exception propagation with associated value."""
        from System import NullReferenceException

        with self.assertRaises(NullReferenceException) as cm:
            raise NullReferenceException('Aiiieee!')

        exc = cm.exception
        self.assertTrue(isinstance(exc, NullReferenceException))
        self.assertTrue(exc.Message == 'Aiiieee!')

    def test_raise_instance_exception(self):
        """Test instance exception propagation."""
        from System import NullReferenceException

        with self.assertRaises(NullReferenceException) as cm:
            raise NullReferenceException()

        exc = cm.exception
        self.assertTrue(isinstance(exc, NullReferenceException))
        self.assertTrue(len(exc.Message) > 0)

    def test_raise_instance_exception_with_args(self):
        """Test instance exception propagation with args."""
        from System import NullReferenceException

        with self.assertRaises(NullReferenceException) as cm:
            raise NullReferenceException("Aiiieee!")

        exc = cm.exception
        self.assertTrue(isinstance(exc, NullReferenceException))
        self.assertTrue(exc.Message == 'Aiiieee!')

    def test_managed_exception_propagation(self):
        """Test propagation of exceptions raised in managed code."""
        from System import Decimal, OverflowException

        with self.assertRaises(OverflowException):
            Decimal.ToInt64(Decimal.MaxValue)

    def test_managed_exception_conversion(self):
        """Test conversion of managed exceptions."""
        from System import OverflowException
        from Python.Test import ExceptionTest

        e = ExceptionTest.GetBaseException()
        self.assertTrue(isinstance(e, System.Exception))

        e = ExceptionTest.GetExplicitException()
        self.assertTrue(isinstance(e, OverflowException))
        self.assertTrue(isinstance(e, System.Exception))

        e = ExceptionTest.GetWidenedException()
        self.assertTrue(isinstance(e, OverflowException))
        self.assertTrue(isinstance(e, System.Exception))

        v = ExceptionTest.SetBaseException(System.Exception('error'))
        self.assertTrue(v)

        v = ExceptionTest.SetExplicitException(OverflowException('error'))
        self.assertTrue(v)

        v = ExceptionTest.SetWidenedException(OverflowException('error'))
        self.assertTrue(v)

    def test_catch_exception_from_managed_method(self):
        """Test catching an exception from a managed method."""
        from Python.Test import ExceptionTest
        from System import OverflowException

        with self.assertRaises(OverflowException) as cm:
            ExceptionTest().ThrowException()

        e = cm.exception
        self.assertTrue(isinstance(e, OverflowException))

    def test_catch_exception_from_managed_property(self):
        """Test catching an exception from a managed property."""
        from Python.Test import ExceptionTest
        from System import OverflowException

        with self.assertRaises(OverflowException) as cm:
            _ = ExceptionTest().ThrowProperty

        e = cm.exception
        self.assertTrue(isinstance(e, OverflowException))

        with self.assertRaises(OverflowException) as cm:
            ExceptionTest().ThrowProperty = 1

        e = cm.exception
        self.assertTrue(isinstance(e, OverflowException))

    def test_catch_exception_managed_class(self):
        """Test catching the managed class of an exception."""
        from System import OverflowException

        with self.assertRaises(OverflowException):
            raise OverflowException('overflow')

    def test_catch_exception_python_class(self):
        """Test catching the python class of an exception."""
        from System import OverflowException

        with self.assertRaises(Exception):
            raise OverflowException('overflow')

    def test_catch_exception_base_class(self):
        """Test catching the base of an exception."""
        from System import OverflowException, ArithmeticException

        with self.assertRaises(ArithmeticException):
            raise OverflowException('overflow')

    def test_catch_exception_nested_base_class(self):
        """Test catching the nested base of an exception."""
        from System import OverflowException, SystemException

        with self.assertRaises(SystemException):
            raise OverflowException('overflow')

    def test_catch_exception_with_assignment(self):
        """Test catching an exception with assignment."""
        from System import OverflowException

        with self.assertRaises(OverflowException) as cm:
            raise OverflowException('overflow')

        e = cm.exception
        self.assertTrue(isinstance(e, OverflowException))

    def test_catch_exception_unqualified(self):
        """Test catching an unqualified exception."""
        from System import OverflowException

        try:
            raise OverflowException('overflow')
        except:
            pass
        else:
            self.fail("failed to catch unqualified exception")

    def test_catch_baseexception(self):
        """Test catching an unqualified exception with BaseException."""
        from System import OverflowException

        with self.assertRaises(BaseException):
            raise OverflowException('overflow')

    def test_apparent_module_of_exception(self):
        """Test the apparent module of an exception."""
        from System import OverflowException

        self.assertTrue(System.Exception.__module__ == 'System')
        self.assertTrue(OverflowException.__module__ == 'System')

    def test_str_of_exception(self):
        """Test the str() representation of an exception."""
        from System import NullReferenceException, Convert, FormatException

        e = NullReferenceException('')
        self.assertEqual(str(e), '')

        e = NullReferenceException('Something bad happened')
        self.assertTrue(str(e).startswith('Something bad happened'))

        with self.assertRaises(FormatException) as cm:
            Convert.ToDateTime('this will fail')

        e = cm.exception
        # fix for international installation
        msg = text_type(e).encode("utf8")
        fnd = text_type('System.Convert.ToDateTime').encode("utf8")
        self.assertTrue(msg.find(fnd) > -1, msg)

    def test_python_compat_of_managed_exceptions(self):
        """Test managed exceptions compatible with Python's implementation"""
        from System import OverflowException
        msg = "Simple message"

        e = OverflowException(msg)
        self.assertEqual(str(e), msg)
        self.assertEqual(text_type(e), msg)

        self.assertEqual(e.args, (msg,))
        self.assertTrue(isinstance(e.args, tuple))
        if PY3:
            self.assertEqual(repr(e), "OverflowException('Simple message',)")
        elif PY2:
            self.assertEqual(repr(e), "OverflowException(u'Simple message',)")

    def test_exception_is_instance_of_system_object(self):
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
            self.assertTrue(isinstance(o, Object))
        else:
            self.assertFalse(isinstance(o, Object))

    def test_pickling_exceptions(self):
        exc = System.Exception("test")
        dumped = pickle.dumps(exc)
        loaded = pickle.loads(dumped)

        self.assertEqual(exc.args, loaded.args)

    @unittest.skipIf(PY2, "__cause__ isn't implemented in PY2")
    def test_chained_exceptions(self):
        from Python.Test import ExceptionTest

        with self.assertRaises(Exception) as cm:
            ExceptionTest.ThrowChainedExceptions()

        exc = cm.exception

        msgs = ("Outer exception",
                "Inner exception",
                "Innermost exception",)
        for msg in msgs:
            self.assertEqual(exc.Message, msg)
            self.assertEqual(exc.__cause__, exc.InnerException)
            exc = exc.__cause__


def test_suite():
    return unittest.makeSuite(ExceptionTests)
