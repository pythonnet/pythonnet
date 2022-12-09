# -*- coding: utf-8 -*-
"""Test sub-classing managed abstract types"""

import System
import pytest
from Python.Test import (AbstractSubClassTestEventArgs, AbstractSubClassTest, AbstractSubClassTestConsumer)


def abstract_derived_class_fixture_a():
    """Delay creation of class until test starts."""

    class FixtureA(AbstractSubClassTest):
        """class that derives from an abstract clr class"""

        _prop_value = 0

        def PublicMethod(self, value):
            """Implementation for abstract PublicMethod"""
            self.PublicProperty = value

        def get_PublicProperty(self):
            return self._prop_value + 10

        def set_PublicProperty(self, value):
            self._prop_value = value

    return FixtureA


def abstract_derived_class_fixture_b():
    """Delay creation of class until test starts."""

    class FixtureB(AbstractSubClassTest):
        """class that derives from an abstract clr class"""

        def BaseMethod(self, value):
            """Overriding implementation of BaseMethod"""
            return super().BaseMethod(value) + 10

    return FixtureB


def abstract_derived_class_fixture_c():
    """Delay creation of class until test starts."""

    class FixtureC(AbstractSubClassTest):
        """class that derives from an abstract clr class"""
        
        _event_handlers = []

        def add_PublicEvent(self, value):
            """Add event implementation"""
            self._event_handlers.append(value)

        def OnPublicEvent(self, value):
            for event_handler in self._event_handlers:
                event_handler(self, value)

    return FixtureC


def test_abstract_derived_class():
    """Test python class derived from abstract managed type"""
    tvalue = 42
    Fixture = abstract_derived_class_fixture_a()
    ob = Fixture()

    # test setter/getter implementations
    ob.PublicProperty = tvalue + 10
    assert ob._prop_value == tvalue + 10
    assert ob.PublicProperty == (tvalue + 20)
    
    # test method implementations
    ob.PublicMethod(tvalue)
    assert ob._prop_value == tvalue
    assert ob.PublicProperty == (tvalue + 10)

    # test base methods
    assert ob.BaseMethod(tvalue) == tvalue


def test_base_methods_of_abstract_derived_class():
    """Test base methods of python class derived from abstract managed type"""
    tvalue = 42
    Fixture = abstract_derived_class_fixture_b()
    ob = Fixture()

    # test base methods
    assert ob.BaseMethod(tvalue) == tvalue + 10


def test_abstract_derived_class_passed_to_clr():
    tvalue = 42
    Fixture = abstract_derived_class_fixture_a()
    ob = Fixture()

    # test setter/getter implementations
    AbstractSubClassTestConsumer.TestPublicProperty(ob, tvalue + 10)
    assert ob._prop_value == tvalue + 10
    assert ob.PublicProperty == (tvalue + 20)
    
    # test method implementations
    AbstractSubClassTestConsumer.TestPublicMethod(ob, tvalue)
    assert ob._prop_value == tvalue
    assert ob.PublicProperty == (tvalue + 10)


def test_events_of_abstract_derived_class():
    """Test base methods of python class derived from abstract managed type"""
    class Handler:
        event_value = 0

        def Handler(self, s, e):
            print(s, e)
            self.event_value = e.Value

    Fixture = abstract_derived_class_fixture_c()
    ob = Fixture()
    handler =  Handler()

    # test base methods
    ob.PublicEvent += handler.Handler
    assert len(ob._event_handlers) == 1

    ob.OnPublicEvent(AbstractSubClassTestEventArgs(42))
    assert handler.event_value == 42
