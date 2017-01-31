# -*- coding: utf-8 -*-

import unittest

import Python.Test as Test

from _compat import DictProxyType


class InterfaceTests(unittest.TestCase):
    """Test CLR interface support."""

    def test_interface_standard_attrs(self):
        """Test standard class attributes."""
        from Python.Test import IPublicInterface

        self.assertTrue(IPublicInterface.__name__ == 'IPublicInterface')
        self.assertTrue(IPublicInterface.__module__ == 'Python.Test')
        self.assertTrue(isinstance(IPublicInterface.__dict__, DictProxyType))

    def test_global_interface_visibility(self):
        """Test visibility of module-level interfaces."""
        from Python.Test import IPublicInterface

        self.assertTrue(IPublicInterface.__name__ == 'IPublicInterface')

        with self.assertRaises(ImportError):
            from Python.Test import IInternalInterface
            _ = IInternalInterface

        with self.assertRaises(AttributeError):
            _ = Test.IInternalInterface

    def test_nested_interface_visibility(self):
        """Test visibility of nested interfaces."""
        from Python.Test import InterfaceTest

        ob = InterfaceTest.IPublic
        self.assertTrue(ob.__name__ == 'IPublic')

        ob = InterfaceTest.IProtected
        self.assertTrue(ob.__name__ == 'IProtected')

        with self.assertRaises(AttributeError):
            _ = InterfaceTest.IInternal

        with self.assertRaises(AttributeError):
            _ = InterfaceTest.IPrivate

    def test_explicit_cast_to_interface(self):
        """Test explicit cast to an interface."""
        from Python.Test import InterfaceTest

        ob = InterfaceTest()
        self.assertTrue(type(ob).__name__ == 'InterfaceTest')
        self.assertTrue(hasattr(ob, 'HelloProperty'))

        i1 = Test.ISayHello1(ob)
        self.assertTrue(type(i1).__name__ == 'ISayHello1')
        self.assertTrue(hasattr(i1, 'SayHello'))
        self.assertTrue(i1.SayHello() == 'hello 1')
        self.assertFalse(hasattr(i1, 'HelloProperty'))

        i2 = Test.ISayHello2(ob)
        self.assertTrue(type(i2).__name__ == 'ISayHello2')
        self.assertTrue(i2.SayHello() == 'hello 2')
        self.assertTrue(hasattr(i2, 'SayHello'))
        self.assertFalse(hasattr(i2, 'HelloProperty'))


def test_suite():
    return unittest.makeSuite(InterfaceTests)
