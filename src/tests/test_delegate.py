# -*- coding: utf-8 -*-
# TODO: Add test for ObjectDelegate

import unittest

import Python.Test as Test
import System
from Python.Test import DelegateTest, StringDelegate

from _compat import DictProxyType
from utils import HelloClass, hello_func, MultipleHandler


class DelegateTests(unittest.TestCase):
    """Test CLR delegate support."""

    def test_delegate_standard_attrs(self):
        """Test standard delegate attributes."""
        from Python.Test import PublicDelegate

        self.assertTrue(PublicDelegate.__name__ == 'PublicDelegate')
        self.assertTrue(PublicDelegate.__module__ == 'Python.Test')
        self.assertTrue(isinstance(PublicDelegate.__dict__, DictProxyType))
        self.assertTrue(PublicDelegate.__doc__ is None)

    def test_global_delegate_visibility(self):
        """Test visibility of module-level delegates."""
        from Python.Test import PublicDelegate

        self.assertTrue(PublicDelegate.__name__ == 'PublicDelegate')
        self.assertTrue(Test.PublicDelegate.__name__ == 'PublicDelegate')

        with self.assertRaises(ImportError):
            from Python.Test import InternalDelegate
            _ = InternalDelegate

        with self.assertRaises(AttributeError):
            _ = Test.InternalDelegate

    def test_nested_delegate_visibility(self):
        """Test visibility of nested delegates."""
        ob = DelegateTest.PublicDelegate
        self.assertTrue(ob.__name__ == 'PublicDelegate')

        ob = DelegateTest.ProtectedDelegate
        self.assertTrue(ob.__name__ == 'ProtectedDelegate')

        with self.assertRaises(AttributeError):
            _ = DelegateTest.InternalDelegate

        with self.assertRaises(AttributeError):
            _ = DelegateTest.PrivateDelegate

    def test_delegate_from_function(self):
        """Test delegate implemented with a Python function."""

        d = StringDelegate(hello_func)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_method(self):
        """Test delegate implemented with a Python instance method."""

        inst = HelloClass()
        d = StringDelegate(inst.hello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_unbound_method(self):
        """Test failure mode for unbound methods."""

        with self.assertRaises(TypeError):
            d = StringDelegate(HelloClass.hello)
            d()

    def test_delegate_from_static_method(self):
        """Test delegate implemented with a Python static method."""

        d = StringDelegate(HelloClass.s_hello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

        inst = HelloClass()
        d = StringDelegate(inst.s_hello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_class_method(self):
        """Test delegate implemented with a Python class method."""

        d = StringDelegate(HelloClass.c_hello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

        inst = HelloClass()
        d = StringDelegate(inst.c_hello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_callable(self):
        """Test delegate implemented with a Python callable object."""

        inst = HelloClass()
        d = StringDelegate(inst)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_managed_instance_method(self):
        """Test delegate implemented with a managed instance method."""
        ob = DelegateTest()
        d = StringDelegate(ob.SayHello)

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_managed_static_method(self):
        """Test delegate implemented with a managed static method."""
        d = StringDelegate(DelegateTest.StaticSayHello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_from_delegate(self):
        """Test delegate implemented with another delegate."""
        d1 = StringDelegate(hello_func)
        d2 = StringDelegate(d1)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d2) == "hello")
        self.assertTrue(d2() == "hello")

        ob.stringDelegate = d2
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def test_delegate_with_invalid_args(self):
        """Test delegate instantiation with invalid (non-callable) args."""

        with self.assertRaises(TypeError):
            _ = StringDelegate(None)

        with self.assertRaises(TypeError):
            _ = StringDelegate("spam")

        with self.assertRaises(TypeError):
            _ = StringDelegate(1)

    def test_multicast_delegate(self):
        """Test multicast delegates."""

        inst = MultipleHandler()
        d1 = StringDelegate(inst.count)
        d2 = StringDelegate(inst.count)

        md = System.Delegate.Combine(d1, d2)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(md) == "ok")
        self.assertTrue(inst.value == 2)

        self.assertTrue(md() == "ok")
        self.assertTrue(inst.value == 4)

    def test_subclass_delegate_fails(self):
        """Test that subclassing of a delegate type fails."""
        from Python.Test import PublicDelegate

        with self.assertRaises(TypeError):
            class Boom(PublicDelegate):
                pass
            _ = Boom

    def test_delegate_equality(self):
        """Test delegate equality."""

        d = StringDelegate(hello_func)
        ob = DelegateTest()
        ob.stringDelegate = d
        self.assertTrue(ob.stringDelegate == d)

    def test_bool_delegate(self):
        """Test boolean delegate."""
        from Python.Test import BoolDelegate

        def always_so_negative():
            return 0

        d = BoolDelegate(always_so_negative)
        ob = DelegateTest()
        ob.CallBoolDelegate(d)

        self.assertTrue(not d())
        self.assertTrue(not ob.CallBoolDelegate(d))

        # test async delegates

        # test multicast delegates

        # test explicit op_

        # test sig mismatch, both on managed and Python side

        # test return wrong type


def test_suite():
    return unittest.makeSuite(DelegateTests)
