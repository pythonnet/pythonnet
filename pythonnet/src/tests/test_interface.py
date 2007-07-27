# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

from Python.Test import InterfaceTest
import sys, os, string, unittest, types
import Python.Test as Test
import System

class InterfaceTests(unittest.TestCase):
    """Test CLR interface support."""

    def testInterfaceStandardAttrs(self):
        """Test standard class attributes."""
        from Python.Test import IPublicInterface as ip
        self.failUnless(ip.__name__ == 'IPublicInterface')
        self.failUnless(ip.__module__ == 'Python.Test')
        self.failUnless(type(ip.__dict__) == types.DictProxyType)


    def testGlobalInterfaceVisibility(self):
        """Test visibility of module-level interfaces."""
        from Python.Test import IPublicInterface
        self.failUnless(IPublicInterface.__name__ == 'IPublicInterface')
        
        def test():
            from Python.Test import IInternalInterface

        self.failUnlessRaises(ImportError, test)

        def test():
            i = Test.IInternalInterface

        self.failUnlessRaises(AttributeError, test)


    def testNestedInterfaceVisibility(self):
        """Test visibility of nested interfaces."""
        ob = InterfaceTest.IPublic
        self.failUnless(ob.__name__ == 'IPublic')

        ob = InterfaceTest.IProtected
        self.failUnless(ob.__name__ == 'IProtected')

        def test():
            ob = InterfaceTest.IInternal

        self.failUnlessRaises(AttributeError, test)

        def test():
            ob = InterfaceTest.IPrivate

        self.failUnlessRaises(AttributeError, test)


    def testExplicitCastToInterface(self):
        """Test explicit cast to an interface."""
        ob = InterfaceTest()
        self.failUnless(type(ob).__name__ == 'InterfaceTest')
        self.failUnless(hasattr(ob, 'HelloProperty'))

        i1 = Test.ISayHello1(ob)
        self.failUnless(type(i1).__name__ == 'ISayHello1')
        self.failUnless(hasattr(i1, 'SayHello'))
        self.failUnless(i1.SayHello() == 'hello 1')
        self.failIf(hasattr(i1, 'HelloProperty'))

        i2 = Test.ISayHello2(ob)
        self.failUnless(type(i2).__name__ == 'ISayHello2')
        self.failUnless(i2.SayHello() == 'hello 2')
        self.failUnless(hasattr(i2, 'SayHello'))
        self.failIf(hasattr(i2, 'HelloProperty'))



def test_suite():
    return unittest.makeSuite(InterfaceTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

