import clr

clr.AddReference('Python.Test')

from Python.Test import DelegateTest, PublicDelegate
from Python.Test import StringDelegate, ObjectDelegate
from Python.Test import BoolDelegate
import sys, os, string, unittest, types
import Python.Test as Test
import System
import six

if six.PY3:
    DictProxyType = type(object.__dict__)
else:
    DictProxyType = types.DictProxyType


class DelegateTests(unittest.TestCase):
    """Test CLR delegate support."""

    def testDelegateStandardAttrs(self):
        """Test standard delegate attributes."""
        self.assertTrue(PublicDelegate.__name__ == 'PublicDelegate')
        self.assertTrue(PublicDelegate.__module__ == 'Python.Test')
        self.assertTrue(type(PublicDelegate.__dict__) == DictProxyType)
        self.assertTrue(PublicDelegate.__doc__ == None)

    def testGlobalDelegateVisibility(self):
        """Test visibility of module-level delegates."""
        from Python.Test import PublicDelegate

        self.assertTrue(PublicDelegate.__name__ == 'PublicDelegate')
        self.assertTrue(Test.PublicDelegate.__name__ == 'PublicDelegate')

        def test():
            from Python.Test import InternalDelegate

        self.assertRaises(ImportError, test)

        def test():
            i = Test.InternalDelegate

        self.assertRaises(AttributeError, test)

    def testNestedDelegateVisibility(self):
        """Test visibility of nested delegates."""
        ob = DelegateTest.PublicDelegate
        self.assertTrue(ob.__name__ == 'PublicDelegate')

        ob = DelegateTest.ProtectedDelegate
        self.assertTrue(ob.__name__ == 'ProtectedDelegate')

        def test():
            ob = DelegateTest.InternalDelegate

        self.assertRaises(AttributeError, test)

        def test():
            ob = DelegateTest.PrivateDelegate

        self.assertRaises(AttributeError, test)

    def testDelegateFromFunction(self):
        """Test delegate implemented with a Python function."""

        def sayhello():
            return "hello"

        d = StringDelegate(sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromMethod(self):
        """Test delegate implemented with a Python instance method."""

        class Hello:
            def sayhello(self):
                return "hello"

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromUnboundMethod(self):
        """Test failure mode for unbound methods."""

        class Hello:
            def sayhello(self):
                return "hello"

        def test():
            d = StringDelegate(Hello.sayhello)
            d()

        self.assertRaises(TypeError, test)

    def testDelegateFromStaticMethod(self):
        """Test delegate implemented with a Python static method."""

        class Hello:
            def sayhello():
                return "hello"

            sayhello = staticmethod(sayhello)

        d = StringDelegate(Hello.sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromClassMethod(self):
        """Test delegate implemented with a Python class method."""

        class Hello:
            def sayhello(self):
                return "hello"

            sayhello = classmethod(sayhello)

        d = StringDelegate(Hello.sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromCallable(self):
        """Test delegate implemented with a Python callable object."""

        class Hello:
            def __call__(self):
                return "hello"

        inst = Hello()
        d = StringDelegate(inst)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromManagedInstanceMethod(self):
        """Test delegate implemented with a managed instance method."""
        ob = DelegateTest()
        d = StringDelegate(ob.SayHello)

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromManagedStaticMethod(self):
        """Test delegate implemented with a managed static method."""
        d = StringDelegate(DelegateTest.StaticSayHello)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d) == "hello")
        self.assertTrue(d() == "hello")

        ob.stringDelegate = d
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateFromDelegate(self):
        """Test delegate implemented with another delegate."""

        def sayhello():
            return "hello"

        d1 = StringDelegate(sayhello)
        d2 = StringDelegate(d1)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(d2) == "hello")
        self.assertTrue(d2() == "hello")

        ob.stringDelegate = d2
        self.assertTrue(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.assertTrue(ob.stringDelegate() == "hello")

    def testDelegateWithInvalidArgs(self):
        """Test delegate instantiation with invalid (non-callable) args."""

        def test():
            d = StringDelegate(None)

        self.assertRaises(TypeError, test)

        def test():
            d = StringDelegate("spam")

        self.assertRaises(TypeError, test)

        def test():
            d = StringDelegate(1)

        self.assertRaises(TypeError, test)

    def testMulticastDelegate(self):
        """Test multicast delegates."""

        class Multi:
            def __init__(self):
                self.value = 0

            def count(self):
                self.value += 1
                return 'ok'

        inst = Multi()
        d1 = StringDelegate(inst.count)
        d2 = StringDelegate(inst.count)

        md = System.Delegate.Combine(d1, d2)
        ob = DelegateTest()

        self.assertTrue(ob.CallStringDelegate(md) == "ok")
        self.assertTrue(inst.value == 2)

        self.assertTrue(md() == "ok")
        self.assertTrue(inst.value == 4)

    def testSubclassDelegateFails(self):
        """Test that subclassing of a delegate type fails."""

        def test():
            class Boom(PublicDelegate):
                pass

        self.assertRaises(TypeError, test)

    def testDelegateEquality(self):
        """Test delegate equality."""

        def sayhello():
            return "hello"

        d = StringDelegate(sayhello)
        ob = DelegateTest()
        ob.stringDelegate = d
        self.assertTrue(ob.stringDelegate == d)

    def testBoolDelegate(self):
        """Test boolean delegate."""

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


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
