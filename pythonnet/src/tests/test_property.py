# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from Python.Test import PropertyTest


class PropertyTests(unittest.TestCase):
    """Test CLR property support."""

    def testPublicInstanceProperty(self):
        """Test public instance properties."""
        object = PropertyTest();

        self.failUnless(object.PublicProperty == 0)
        object.PublicProperty = 1
        self.failUnless(object.PublicProperty == 1)

        def test():
            del PropertyTest().PublicProperty

        self.failUnlessRaises(TypeError, test)


    def testPublicStaticProperty(self):
        """Test public static properties."""
        object = PropertyTest();

        self.failUnless(PropertyTest.PublicStaticProperty == 0)
        PropertyTest.PublicStaticProperty = 1
        self.failUnless(PropertyTest.PublicStaticProperty == 1)

        self.failUnless(object.PublicStaticProperty == 1)
        object.PublicStaticProperty = 0
        self.failUnless(object.PublicStaticProperty == 0)

        def test():
            del PropertyTest.PublicStaticProperty

        self.failUnlessRaises(TypeError, test)

        def test():
            del PropertyTest().PublicStaticProperty

        self.failUnlessRaises(TypeError, test)


    def testProtectedInstanceProperty(self):
        """Test protected instance properties."""
        object = PropertyTest();

        self.failUnless(object.ProtectedProperty == 0)
        object.ProtectedProperty = 1
        self.failUnless(object.ProtectedProperty == 1)

        def test():
            del PropertyTest().ProtectedProperty

        self.failUnlessRaises(TypeError, test)


    def testProtectedStaticProperty(self):
        """Test protected static properties."""
        object = PropertyTest();

        self.failUnless(PropertyTest.ProtectedStaticProperty == 0)
        PropertyTest.ProtectedStaticProperty = 1
        self.failUnless(PropertyTest.ProtectedStaticProperty == 1)

        self.failUnless(object.ProtectedStaticProperty == 1)
        object.ProtectedStaticProperty = 0
        self.failUnless(object.ProtectedStaticProperty == 0)

        def test():
            del PropertyTest.ProtectedStaticProperty

        self.failUnlessRaises(TypeError, test)

        def test():
            del PropertyTest().ProtectedStaticProperty

        self.failUnlessRaises(TypeError, test)


    def testInternalProperty(self):
        """Test internal properties."""

        def test():
            f = PropertyTest().InternalProperty

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = PropertyTest().InternalStaticProperty

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = PropertyTest.InternalStaticProperty

        self.failUnlessRaises(AttributeError, test)


    def testPrivateProperty(self):
        """Test private properties."""

        def test():
            f = PropertyTest().PrivateProperty

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = PropertyTest().PrivateStaticProperty

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = PropertyTest.PrivateStaticProperty

        self.failUnlessRaises(AttributeError, test)


    def testPropertyDescriptorGetSet(self):
        """Test property descriptor get / set."""

        # This test ensures that setting an attribute implemented with
        # a descriptor actually goes through the descriptor (rather than
        # silently replacing the descriptor in the instance or type dict.

        object = PropertyTest()

        self.failUnless(PropertyTest.PublicStaticProperty == 0)
        self.failUnless(object.PublicStaticProperty == 0)

        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.failUnless(type(descriptor) != types.IntType)

        object.PublicStaticProperty = 0
        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.failUnless(type(descriptor) != types.IntType)

        PropertyTest.PublicStaticProperty = 0
        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.failUnless(type(descriptor) != types.IntType)


    def testPropertyDescriptorWrongType(self):
        """Test setting a property using a value of the wrong type."""
        def test():
            object = PropertyTest()
            object.PublicProperty = "spam"

        self.failUnlessRaises(TypeError, test)


    def testPropertyDescriptorAbuse(self):
        """Test property descriptor abuse."""
        desc = PropertyTest.__dict__['PublicProperty']

        def test():
            desc.__get__(0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.failUnlessRaises(TypeError, test)


    def testInterfaceProperty(self):
        """Test properties of interfaces. Added after a bug report
           that an IsAbstract check was inappropriate and prevented
           use of properties when only the interface is known."""

        from System.Collections import Hashtable, ICollection
        mapping = Hashtable()
        coll = ICollection(mapping)
        self.failUnless(coll.Count == 0)



def test_suite():
    return unittest.makeSuite(PropertyTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

