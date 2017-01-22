# -*- coding: utf-8 -*-
# FIXME: This test module fails on Linux

import unittest

import System
from Python.Test import (IInterfaceTest, SubClassTest, TestEventArgs,
                         TestFunctions)
from System.Collections.Generic import List

from _compat import range


# class that implements the test interface
class InterfaceTestClass(IInterfaceTest):
    __namespace__ = "Python.Test"

    def foo(self):
        return "InterfaceTestClass"

    def bar(self, x, i):
        return "/".join([x] * i)


# class that derives from a class deriving from IInterfaceTest
class DerivedClass(SubClassTest):
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


# class that implements IInterfaceTest.TestEvent
class DerivedEventTest(IInterfaceTest):
    __namespace__ = "Python.Test"

    def __init__(self):
        self.event_handlers = []

    # event handling
    def add_TestEvent(self, handler):
        self.event_handlers.append(handler)

    def remove_TestEvent(self, handler):
        self.event_handlers.remove(handler)

    def OnTestEvent(self, value):
        args = TestEventArgs(value)
        for handler in self.event_handlers:
            handler(self, args)


class SubClassTests(unittest.TestCase):
    """Test subclassing managed types"""

    def testBaseClass(self):
        """Test base class managed type"""
        ob = SubClassTest()
        self.assertEqual(ob.foo(), "foo")
        self.assertEqual(TestFunctions.test_foo(ob), "foo")
        self.assertEqual(ob.bar("bar", 2), "bar")
        self.assertEqual(TestFunctions.test_bar(ob, "bar", 2), "bar")
        self.assertEqual(ob.not_overriden(), "not_overriden")
        self.assertEqual(list(ob.return_list()), ["a", "b", "c"])
        self.assertEqual(list(SubClassTest.test_list(ob)), ["a", "b", "c"])

    def testInterface(self):
        """Test python classes can derive from C# interfaces"""
        ob = InterfaceTestClass()
        self.assertEqual(ob.foo(), "InterfaceTestClass")
        self.assertEqual(TestFunctions.test_foo(ob), "InterfaceTestClass")
        self.assertEqual(ob.bar("bar", 2), "bar/bar")
        self.assertEqual(TestFunctions.test_bar(ob, "bar", 2), "bar/bar")

        x = TestFunctions.pass_through(ob)
        self.assertEqual(id(x), id(ob))

    def testDerivedClass(self):
        """Test python class derived from managed type"""
        ob = DerivedClass()
        self.assertEqual(ob.foo(), "DerivedClass")
        self.assertEqual(ob.base_foo(), "foo")
        self.assertEqual(ob.super_foo(), "foo")
        self.assertEqual(TestFunctions.test_foo(ob), "DerivedClass")
        self.assertEqual(ob.bar("bar", 2), "bar_bar")
        self.assertEqual(TestFunctions.test_bar(ob, "bar", 2), "bar_bar")
        self.assertEqual(ob.not_overriden(), "not_overriden")
        self.assertEqual(list(ob.return_list()), ["A", "B", "C"])
        self.assertEqual(list(SubClassTest.test_list(ob)), ["A", "B", "C"])

        x = TestFunctions.pass_through(ob)
        self.assertEqual(id(x), id(ob))

    def testCreateInstance(self):
        """Test derived instances can be created from managed code"""
        ob = TestFunctions.create_instance(DerivedClass)
        self.assertEqual(ob.foo(), "DerivedClass")
        self.assertEqual(TestFunctions.test_foo(ob), "DerivedClass")
        self.assertEqual(ob.bar("bar", 2), "bar_bar")
        self.assertEqual(TestFunctions.test_bar(ob, "bar", 2), "bar_bar")
        self.assertEqual(ob.not_overriden(), "not_overriden")

        x = TestFunctions.pass_through(ob)
        self.assertEqual(id(x), id(ob))

        ob2 = TestFunctions.create_instance(InterfaceTestClass)
        self.assertEqual(ob2.foo(), "InterfaceTestClass")
        self.assertEqual(TestFunctions.test_foo(ob2), "InterfaceTestClass")
        self.assertEqual(ob2.bar("bar", 2), "bar/bar")
        self.assertEqual(TestFunctions.test_bar(ob2, "bar", 2), "bar/bar")

        y = TestFunctions.pass_through(ob2)
        self.assertEqual(id(y), id(ob2))

    def testEvents(self):
        class EventHandler(object):
            def handler(self, x, args):
                self.value = args.value

        event_handler = EventHandler()

        x = SubClassTest()
        x.TestEvent += event_handler.handler
        self.assertEqual(TestFunctions.test_event(x, 1), 1)
        self.assertEqual(event_handler.value, 1)

        i = InterfaceTestClass()
        with self.assertRaises(System.NotImplementedException):
            TestFunctions.test_event(i, 2)

        d = DerivedEventTest()
        d.add_TestEvent(event_handler.handler)
        self.assertEqual(TestFunctions.test_event(d, 3), 3)
        self.assertEqual(event_handler.value, 3)
        self.assertEqual(len(d.event_handlers), 1)

    def test_isinstance(self):
        a = [str(x) for x in range(0, 1000)]
        b = [System.String(x) for x in a]

        for x in a:
            self.assertFalse(isinstance(x, System.Object))
            self.assertFalse(isinstance(x, System.String))

        for x in b:
            self.assertTrue(isinstance(x, System.Object))
            self.assertTrue(isinstance(x, System.String))


def test_suite():
    return unittest.makeSuite(SubClassTests)
