# -*- coding: utf-8 -*-

"""Test CLR property support."""

import pytest
from Python.Test import PropertyTest


def test_public_instance_property():
    """Test public instance properties."""
    ob = PropertyTest()

    assert ob.PublicProperty == 0
    ob.PublicProperty = 1
    assert ob.PublicProperty == 1

    with pytest.raises(TypeError):
        del PropertyTest().PublicProperty


def test_public_static_property():
    """Test public static properties."""
    ob = PropertyTest()

    assert PropertyTest.PublicStaticProperty == 0
    PropertyTest.PublicStaticProperty = 1
    assert PropertyTest.PublicStaticProperty == 1

    assert ob.PublicStaticProperty == 1
    ob.PublicStaticProperty = 0
    assert ob.PublicStaticProperty == 0

    with pytest.raises(TypeError):
        del PropertyTest.PublicStaticProperty

    with pytest.raises(TypeError):
        del PropertyTest().PublicStaticProperty


def test_protected_instance_property():
    """Test protected instance properties."""
    ob = PropertyTest()

    assert ob.ProtectedProperty == 0
    ob.ProtectedProperty = 1
    assert ob.ProtectedProperty == 1

    with pytest.raises(TypeError):
        del PropertyTest().ProtectedProperty


def test_protected_static_property():
    """Test protected static properties."""
    ob = PropertyTest()

    assert PropertyTest.ProtectedStaticProperty == 0
    PropertyTest.ProtectedStaticProperty = 1
    assert PropertyTest.ProtectedStaticProperty == 1

    assert ob.ProtectedStaticProperty == 1
    ob.ProtectedStaticProperty = 0
    assert ob.ProtectedStaticProperty == 0

    with pytest.raises(TypeError):
        del PropertyTest.ProtectedStaticProperty

    with pytest.raises(TypeError):
        del PropertyTest().ProtectedStaticProperty


def test_internal_property():
    """Test internal properties."""

    with pytest.raises(AttributeError):
        _ = PropertyTest().InternalProperty

    with pytest.raises(AttributeError):
        _ = PropertyTest().InternalStaticProperty

    with pytest.raises(AttributeError):
        _ = PropertyTest.InternalStaticProperty


def test_private_property():
    """Test private properties."""

    with pytest.raises(AttributeError):
        _ = PropertyTest().PrivateProperty

    with pytest.raises(AttributeError):
        _ = PropertyTest().PrivateStaticProperty

    with pytest.raises(AttributeError):
        _ = PropertyTest.PrivateStaticProperty


def test_property_descriptor_get_set():
    """Test property descriptor get / set."""

    # This test ensures that setting an attribute implemented with
    # a descriptor actually goes through the descriptor (rather than
    # silently replacing the descriptor in the instance or type dict.

    ob = PropertyTest()

    assert PropertyTest.PublicStaticProperty == 0
    assert ob.PublicStaticProperty == 0

    descriptor = PropertyTest.__dict__['PublicStaticProperty']
    assert type(descriptor) != int

    ob.PublicStaticProperty = 0
    descriptor = PropertyTest.__dict__['PublicStaticProperty']
    assert type(descriptor) != int

    PropertyTest.PublicStaticProperty = 0
    descriptor = PropertyTest.__dict__['PublicStaticProperty']
    assert type(descriptor) != int


def test_property_descriptor_wrong_type():
    """Test setting a property using a value of the wrong type."""

    with pytest.raises(TypeError):
        ob = PropertyTest()
        ob.PublicProperty = "spam"


def test_property_descriptor_abuse():
    """Test property descriptor abuse."""
    desc = PropertyTest.__dict__['PublicProperty']

    with pytest.raises(TypeError):
        desc.__get__(0, 0)

    with pytest.raises(TypeError):
        desc.__set__(0, 0)


def test_interface_property():
    """Test properties of interfaces. Added after a bug report
       that an IsAbstract check was inappropriate and prevented
       use of properties when only the interface is known."""
    from System.Collections import Hashtable, ICollection

    mapping = Hashtable()
    coll = ICollection(mapping)
    assert coll.Count == 0
