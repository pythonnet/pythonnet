# -*- coding: utf-8 -*-
#
import unittest

from Python.Test import PropertyTest


class PropertyTests(unittest.TestCase):
    """Test CLR property support."""

    def testPublicInstanceProperty(self):
        """Test public instance properties."""
        ob = PropertyTest()

        self.assertTrue(ob.PublicProperty == 0)
        ob.PublicProperty = 1
        self.assertTrue(ob.PublicProperty == 1)

        def test():
            del PropertyTest().PublicProperty

        self.assertRaises(TypeError, test)

    def testPublicStaticProperty(self):
        """Test public static properties."""
        ob = PropertyTest()

        self.assertTrue(PropertyTest.PublicStaticProperty == 0)
        PropertyTest.PublicStaticProperty = 1
        self.assertTrue(PropertyTest.PublicStaticProperty == 1)

        self.assertTrue(ob.PublicStaticProperty == 1)
        ob.PublicStaticProperty = 0
        self.assertTrue(ob.PublicStaticProperty == 0)

        def test():
            del PropertyTest.PublicStaticProperty

        self.assertRaises(TypeError, test)

        def test():
            del PropertyTest().PublicStaticProperty

        self.assertRaises(TypeError, test)

    def testProtectedInstanceProperty(self):
        """Test protected instance properties."""
        ob = PropertyTest()

        self.assertTrue(ob.ProtectedProperty == 0)
        ob.ProtectedProperty = 1
        self.assertTrue(ob.ProtectedProperty == 1)

        def test():
            del PropertyTest().ProtectedProperty

        self.assertRaises(TypeError, test)

    def testProtectedStaticProperty(self):
        """Test protected static properties."""
        ob = PropertyTest()

        self.assertTrue(PropertyTest.ProtectedStaticProperty == 0)
        PropertyTest.ProtectedStaticProperty = 1
        self.assertTrue(PropertyTest.ProtectedStaticProperty == 1)

        self.assertTrue(ob.ProtectedStaticProperty == 1)
        ob.ProtectedStaticProperty = 0
        self.assertTrue(ob.ProtectedStaticProperty == 0)

        def test():
            del PropertyTest.ProtectedStaticProperty

        self.assertRaises(TypeError, test)

        def test():
            del PropertyTest().ProtectedStaticProperty

        self.assertRaises(TypeError, test)

    def testInternalProperty(self):
        """Test internal properties."""

        def test():
            return PropertyTest().InternalProperty

        self.assertRaises(AttributeError, test)

        def test():
            return PropertyTest().InternalStaticProperty

        self.assertRaises(AttributeError, test)

        def test():
            return PropertyTest.InternalStaticProperty

        self.assertRaises(AttributeError, test)

    def testPrivateProperty(self):
        """Test private properties."""

        def test():
            return PropertyTest().PrivateProperty

        self.assertRaises(AttributeError, test)

        def test():
            return PropertyTest().PrivateStaticProperty

        self.assertRaises(AttributeError, test)

        def test():
            return PropertyTest.PrivateStaticProperty

        self.assertRaises(AttributeError, test)

    def testPropertyDescriptorGetSet(self):
        """Test property descriptor get / set."""

        # This test ensures that setting an attribute implemented with
        # a descriptor actually goes through the descriptor (rather than
        # silently replacing the descriptor in the instance or type dict.

        ob = PropertyTest()

        self.assertTrue(PropertyTest.PublicStaticProperty == 0)
        self.assertTrue(ob.PublicStaticProperty == 0)

        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.assertTrue(type(descriptor) != int)

        ob.PublicStaticProperty = 0
        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.assertTrue(type(descriptor) != int)

        PropertyTest.PublicStaticProperty = 0
        descriptor = PropertyTest.__dict__['PublicStaticProperty']
        self.assertTrue(type(descriptor) != int)

    def testPropertyDescriptorWrongType(self):
        """Test setting a property using a value of the wrong type."""

        def test():
            ob = PropertyTest()
            ob.PublicProperty = "spam"

        self.assertTrue(TypeError, test)

    def testPropertyDescriptorAbuse(self):
        """Test property descriptor abuse."""
        desc = PropertyTest.__dict__['PublicProperty']

        def test():
            desc.__get__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.assertRaises(TypeError, test)

    def testInterfaceProperty(self):
        """Test properties of interfaces. Added after a bug report
           that an IsAbstract check was inappropriate and prevented
           use of properties when only the interface is known."""
        from System.Collections import Hashtable, ICollection

        mapping = Hashtable()
        coll = ICollection(mapping)
        self.assertTrue(coll.Count == 0)


def test_suite():
    return unittest.makeSuite(PropertyTests)
