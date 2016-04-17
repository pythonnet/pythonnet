from Python.Test import InterfaceTest
import sys, os, string, unittest, types
import Python.Test as Test
import System
import six

if six.PY3:
    DictProxyType = type(object.__dict__)
else:
    DictProxyType = types.DictProxyType


class InterfaceTests(unittest.TestCase):
    """Test CLR interface support."""

    def testInterfaceStandardAttrs(self):
        """Test standard class attributes."""
        from Python.Test import IPublicInterface as ip
        self.assertTrue(ip.__name__ == 'IPublicInterface')
        self.assertTrue(ip.__module__ == 'Python.Test')
        self.assertTrue(type(ip.__dict__) == DictProxyType)

    def testGlobalInterfaceVisibility(self):
        """Test visibility of module-level interfaces."""
        from Python.Test import IPublicInterface
        self.assertTrue(IPublicInterface.__name__ == 'IPublicInterface')

        def test():
            from Python.Test import IInternalInterface

        self.assertRaises(ImportError, test)

        def test():
            i = Test.IInternalInterface

        self.assertRaises(AttributeError, test)

    def testNestedInterfaceVisibility(self):
        """Test visibility of nested interfaces."""
        ob = InterfaceTest.IPublic
        self.assertTrue(ob.__name__ == 'IPublic')

        ob = InterfaceTest.IProtected
        self.assertTrue(ob.__name__ == 'IProtected')

        def test():
            ob = InterfaceTest.IInternal

        self.assertRaises(AttributeError, test)

        def test():
            ob = InterfaceTest.IPrivate

        self.assertRaises(AttributeError, test)

    def testExplicitCastToInterface(self):
        """Test explicit cast to an interface."""
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


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
