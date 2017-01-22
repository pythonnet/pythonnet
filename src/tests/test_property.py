# -*- coding: utf-8 -*-

import unittest

from Python.Test import PropertyTest


class PropertyTests(unittest.TestCase):
    """Test CLR property support."""

    def test_public_instance_property(self):
        """Test public instance properties."""
        ob = PropertyTest()

        self.assertTrue(ob.PublicProperty == 0)
        ob.PublicProperty = 1
        self.assertTrue(ob.PublicProperty == 1)

        with self.assertRaises(TypeError):
            del PropertyTest().PublicProperty

    def test_public_static_property(self):
        """Test public static properties."""
        ob = PropertyTest()

        self.assertTrue(PropertyTest.PublicStaticProperty == 0)
        PropertyTest.PublicStaticProperty = 1
        self.assertTrue(PropertyTest.PublicStaticProperty == 1)

        self.assertTrue(ob.PublicStaticProperty == 1)
        ob.PublicStaticProperty = 0
        self.assertTrue(ob.PublicStaticProperty == 0)

        with self.assertRaises(TypeError):
            del PropertyTest.PublicStaticProperty

        with self.assertRaises(TypeError):
            del PropertyTest().PublicStaticProperty

    def test_protected_instance_property(self):
        """Test protected instance properties."""
        ob = PropertyTest()

        self.assertTrue(ob.ProtectedProperty == 0)
        ob.ProtectedProperty = 1
        self.assertTrue(ob.ProtectedProperty == 1)

        with self.assertRaises(TypeError):
            del PropertyTest().ProtectedProperty

    def test_protected_static_property(self):
        """Test protected static properties."""
        ob = PropertyTest()

        self.assertTrue(PropertyTest.ProtectedStaticProperty == 0)
        PropertyTest.ProtectedStaticProperty = 1
        self.assertTrue(PropertyTest.ProtectedStaticProperty == 1)

        self.assertTrue(ob.ProtectedStaticProperty == 1)
        ob.ProtectedStaticProperty = 0
        self.assertTrue(ob.ProtectedStaticProperty == 0)

        with self.assertRaises(TypeError):
            del PropertyTest.ProtectedStaticProperty

        with self.assertRaises(TypeError):
            del PropertyTest().ProtectedStaticProperty

    def test_internal_property(self):
        """Test internal properties."""

        with self.assertRaises(AttributeError):
            _ = PropertyTest().InternalProperty

        with self.assertRaises(AttributeError):
            _ = PropertyTest().InternalStaticProperty

        with self.assertRaises(AttributeError):
            _ = PropertyTest.InternalStaticProperty

    def test_private_property(self):
        """Test private properties."""

        with self.assertRaises(AttributeError):
            _ = PropertyTest().PrivateProperty

        with self.assertRaises(AttributeError):
            _ = PropertyTest().PrivateStaticProperty

        with self.assertRaises(AttributeError):
            _ = PropertyTest.PrivateStaticProperty

    def test_property_descriptor_get_set(self):
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

    def test_property_descriptor_wrong_type(self):
        """Test setting a property using a value of the wrong type."""

        with self.assertRaises(TypeError):
            ob = PropertyTest()
            ob.PublicProperty = "spam"

    def test_property_descriptor_abuse(self):
        """Test property descriptor abuse."""
        desc = PropertyTest.__dict__['PublicProperty']

        with self.assertRaises(TypeError):
            desc.__get__(0, 0)

        with self.assertRaises(TypeError):
            desc.__set__(0, 0)

    def test_interface_property(self):
        """Test properties of interfaces. Added after a bug report
           that an IsAbstract check was inappropriate and prevented
           use of properties when only the interface is known."""
        from System.Collections import Hashtable, ICollection

        mapping = Hashtable()
        coll = ICollection(mapping)
        self.assertTrue(coll.Count == 0)


def test_suite():
    return unittest.makeSuite(PropertyTests)
