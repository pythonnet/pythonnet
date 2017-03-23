# -*- coding: utf-8 -*-
# FIXME: This test module randomly passes/fails even if all tests are skipped.
# Something fishy is going on with the Test fixtures. Behavior seen on CI on
# both Linux and Windows
# TODO: Remove delay of class creations. Adding SetUp/TearDown may help

"""Test sub-classing managed types"""

import System
import pytest
from Python.Test import (IInterfaceTest, SubClassTest, EventArgsTest,
                         FunctionsTest)
from System.Collections.Generic import List

from ._compat import range


def interface_test_class_fixture():
    """Delay creation of class until test starts."""

    class InterfaceTestClass(IInterfaceTest):
        """class that implements the test interface"""
        __namespace__ = "Python.Test"

        def foo(self):
            return "InterfaceTestClass"

        def bar(self, x, i):
            return "/".join([x] * i)

    return InterfaceTestClass


def derived_class_fixture():
    """Delay creation of class until test starts."""

    class DerivedClass(SubClassTest):
        """class that derives from a class deriving from IInterfaceTest"""
        __namespace__ = "Python.Test"

        def foo(self):
            return "DerivedClass"

        def base_foo(self):
            return SubClassTest.foo(self)

        def super_foo(self):
            return super(DerivedClass, self).foo()

        def bar(self, x, i):
            return "_".join([x] * i)

        def return_list(self):
            l = List[str]()
            l.Add("A")
            l.Add("B")
            l.Add("C")
            return l

    return DerivedClass


def derived_event_test_class_fixture():
    """Delay creation of class until test starts."""

    class DerivedEventTest(IInterfaceTest):
        """class that implements IInterfaceTest.TestEvent"""
        __namespace__ = "Python.Test"

        def __init__(self):
            self.event_handlers = []

        # event handling
        def add_TestEvent(self, handler):
            self.event_handlers.append(handler)

        def remove_TestEvent(self, handler):
            self.event_handlers.remove(handler)

        def OnTestEvent(self, value):
            args = EventArgsTest(value)
            for handler in self.event_handlers:
                handler(self, args)

    return DerivedEventTest


@pytest.mark.skip(reason="FIXME: test randomly pass/fails")
def test_base_class():
    """Test base class managed type"""
    ob = SubClassTest()
    assert ob.foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "foo"
    assert ob.bar("bar", 2) == "bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["a", "b", "c"]
    assert list(SubClassTest.test_list(ob)) == ["a", "b", "c"]


@pytest.mark.skip(reason="FIXME: test randomly pass/fails")
def test_interface():
    """Test python classes can derive from C# interfaces"""
    InterfaceTestClass = interface_test_class_fixture()
    ob = InterfaceTestClass()
    assert ob.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob) == "InterfaceTestClass"
    assert ob.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar/bar"

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)


@pytest.mark.skip(reason="FIXME: test randomly pass/fails")
def test_derived_class():
    """Test python class derived from managed type"""
    DerivedClass = derived_class_fixture()
    ob = DerivedClass()
    assert ob.foo() == "DerivedClass"
    assert ob.base_foo() == "foo"
    assert ob.super_foo() == "foo"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"
    assert list(ob.return_list()) == ["A", "B", "C"]
    assert list(SubClassTest.test_list(ob)) == ["A", "B", "C"]

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)


@pytest.mark.skip(reason="FIXME: test randomly pass/fails")
def test_create_instance():
    """Test derived instances can be created from managed code"""
    DerivedClass = derived_class_fixture()
    ob = FunctionsTest.create_instance(DerivedClass)
    assert ob.foo() == "DerivedClass"
    assert FunctionsTest.test_foo(ob) == "DerivedClass"
    assert ob.bar("bar", 2) == "bar_bar"
    assert FunctionsTest.test_bar(ob, "bar", 2) == "bar_bar"
    assert ob.not_overriden() == "not_overriden"

    x = FunctionsTest.pass_through(ob)
    assert id(x) == id(ob)

    InterfaceTestClass = interface_test_class_fixture()
    ob2 = FunctionsTest.create_instance(InterfaceTestClass)
    assert ob2.foo() == "InterfaceTestClass"
    assert FunctionsTest.test_foo(ob2) == "InterfaceTestClass"
    assert ob2.bar("bar", 2) == "bar/bar"
    assert FunctionsTest.test_bar(ob2, "bar", 2) == "bar/bar"

    y = FunctionsTest.pass_through(ob2)
    assert id(y) == id(ob2)


@pytest.mark.skip(reason="FIXME: test randomly pass/fails")
def test_events():
    class EventHandler(object):
        def handler(self, x, args):
            self.value = args.value

    event_handler = EventHandler()

    x = SubClassTest()
    x.TestEvent += event_handler.handler
    assert FunctionsTest.test_event(x, 1) == 1
    assert event_handler.value == 1

    InterfaceTestClass = interface_test_class_fixture()
    i = InterfaceTestClass()
    with pytest.raises(System.NotImplementedException):
        FunctionsTest.test_event(i, 2)

    DerivedEventTest = derived_event_test_class_fixture()
    d = DerivedEventTest()
    d.add_TestEvent(event_handler.handler)
    assert FunctionsTest.test_event(d, 3) == 3
    assert event_handler.value == 3
    assert len(d.event_handlers) == 1


def test_isinstance_check():
    a = [str(x) for x in range(0, 1000)]
    b = [System.String(x) for x in a]

    for x in a:
        assert not isinstance(x, System.Object)
        assert not isinstance(x, System.String)

    for x in b:
        assert isinstance(x, System.Object)
        assert isinstance(x, System.String)
