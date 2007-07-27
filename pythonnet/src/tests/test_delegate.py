# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

from Python.Test import DelegateTest, PublicDelegate
from Python.Test import StringDelegate, ObjectDelegate
from Python.Test import BoolDelegate
import sys, os, string, unittest, types
import Python.Test as Test
import System


class DelegateTests(unittest.TestCase):
    """Test CLR delegate support."""

    def testDelegateStandardAttrs(self):
        """Test standard delegate attributes."""
        self.failUnless(PublicDelegate.__name__ == 'PublicDelegate')
        self.failUnless(PublicDelegate.__module__ == 'Python.Test')
        self.failUnless(type(PublicDelegate.__dict__) == types.DictProxyType)
        self.failUnless(PublicDelegate.__doc__ == None)


    def testGlobalDelegateVisibility(self):
        """Test visibility of module-level delegates."""
        from Python.Test import PublicDelegate

        self.failUnless(PublicDelegate.__name__ == 'PublicDelegate')
        self.failUnless(Test.PublicDelegate.__name__ == 'PublicDelegate')

        def test():
            from Python.Test import InternalDelegate

        self.failUnlessRaises(ImportError, test)

        def test():
            i = Test.InternalDelegate

        self.failUnlessRaises(AttributeError, test)


    def testNestedDelegateVisibility(self):
        """Test visibility of nested delegates."""
        ob = DelegateTest.PublicDelegate
        self.failUnless(ob.__name__ == 'PublicDelegate')

        ob = DelegateTest.ProtectedDelegate
        self.failUnless(ob.__name__ == 'ProtectedDelegate')

        def test():
            ob = DelegateTest.InternalDelegate

        self.failUnlessRaises(AttributeError, test)

        def test():
            ob = DelegateTest.PrivateDelegate

        self.failUnlessRaises(AttributeError, test)


    def testDelegateFromFunction(self):
        """Test delegate implemented with a Python function."""

        def sayhello():
            return "hello"

        d = StringDelegate(sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromMethod(self):
        """Test delegate implemented with a Python instance method."""

        class Hello:
            def sayhello(self):
                return "hello"

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromUnboundMethod(self):
        """Test failure mode for unbound methods."""

        class Hello:
            def sayhello(self):
                return "hello"

        def test():
            d = StringDelegate(Hello.sayhello)
            d()

        self.failUnlessRaises(TypeError, test)


    def testDelegateFromStaticMethod(self):
        """Test delegate implemented with a Python static method."""

        class Hello:
            def sayhello():
                return "hello"
            sayhello = staticmethod(sayhello)

        d = StringDelegate(Hello.sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromClassMethod(self):
        """Test delegate implemented with a Python class method."""

        class Hello:
            def sayhello(self):
                return "hello"
            sayhello = classmethod(sayhello)

        d = StringDelegate(Hello.sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")

        inst = Hello()
        d = StringDelegate(inst.sayhello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromCallable(self):
        """Test delegate implemented with a Python callable object."""

        class Hello:
            def __call__(self):
                return "hello"

        inst = Hello()
        d = StringDelegate(inst)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromManagedInstanceMethod(self):
        """Test delegate implemented with a managed instance method."""
        ob = DelegateTest()
        d = StringDelegate(ob.SayHello)

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromManagedStaticMethod(self):
        """Test delegate implemented with a managed static method."""
        d = StringDelegate(DelegateTest.StaticSayHello)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d) == "hello")
        self.failUnless(d() == "hello")

        ob.stringDelegate = d
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")


    def testDelegateFromDelegate(self):
        """Test delegate implemented with another delegate."""

        def sayhello():
            return "hello"

        d1 = StringDelegate(sayhello)
        d2 = StringDelegate(d1)
        ob = DelegateTest()

        self.failUnless(ob.CallStringDelegate(d2) == "hello")
        self.failUnless(d2() == "hello")

        ob.stringDelegate = d2
        self.failUnless(ob.CallStringDelegate(ob.stringDelegate) == "hello")
        self.failUnless(ob.stringDelegate() == "hello")

    
    def testDelegateWithInvalidArgs(self):
        """Test delegate instantiation with invalid (non-callable) args."""
        def test():
            d = StringDelegate(None)

        self.failUnlessRaises(TypeError, test)

        def test():
            d = StringDelegate("spam")

        self.failUnlessRaises(TypeError, test)

        def test():
            d = StringDelegate(1)

        self.failUnlessRaises(TypeError, test)


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

        self.failUnless(ob.CallStringDelegate(md) == "ok")
        self.failUnless(inst.value == 2)
        
        self.failUnless(md() == "ok")
        self.failUnless(inst.value == 4)


    def testSubclassDelegateFails(self):
        """Test that subclassing of a delegate type fails."""
        def test():
            class Boom(PublicDelegate):
                pass

        self.failUnlessRaises(TypeError, test)


    def testDelegateEquality(self):
        """Test delegate equality."""

        def sayhello():
            return "hello"

        d = StringDelegate(sayhello)
        ob = DelegateTest()
        ob.stringDelegate = d
        self.failUnless(ob.stringDelegate == d)


    def testBoolDelegate(self):
        """Test boolean delegate."""

        def always_so_negative():
            return 0

        d = BoolDelegate(always_so_negative)
        ob = DelegateTest()
        ob.CallBoolDelegate(d)
        

        self.failUnless(not d())

        self.failUnless(not ob.CallBoolDelegate(d))

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

