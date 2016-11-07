import clr

clr.AddReference('Python.Test')
clr.AddReference('System')

import sys, os, string, unittest, types
from Python.Test import TestFunctions, SubClassTest, IInterfaceTest, TestEventArgs
from System.Collections.Generic import List
from System import NotImplementedException


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
        object = SubClassTest()
        self.assertEqual(object.foo(), "foo")
        self.assertEqual(TestFunctions.test_foo(object), "foo")
        self.assertEqual(object.bar("bar", 2), "bar")
        self.assertEqual(TestFunctions.test_bar(object, "bar", 2), "bar")
        self.assertEqual(object.not_overriden(), "not_overriden")
        self.assertEqual(list(object.return_list()), ["a", "b", "c"])
        self.assertEqual(list(SubClassTest.test_list(object)), ["a", "b", "c"])

    def testInterface(self):
        """Test python classes can derive from C# interfaces"""
        object = InterfaceTestClass()
        self.assertEqual(object.foo(), "InterfaceTestClass")
        self.assertEqual(TestFunctions.test_foo(object), "InterfaceTestClass")
        self.assertEqual(object.bar("bar", 2), "bar/bar")
        self.assertEqual(TestFunctions.test_bar(object, "bar", 2), "bar/bar")

        x = TestFunctions.pass_through(object)
        self.assertEqual(id(x), id(object))

    def testDerivedClass(self):
        """Test python class derived from managed type"""
        object = DerivedClass()
        self.assertEqual(object.foo(), "DerivedClass")
        self.assertEqual(object.base_foo(), "foo")
        self.assertEqual(object.super_foo(), "foo")
        self.assertEqual(TestFunctions.test_foo(object), "DerivedClass")
        self.assertEqual(object.bar("bar", 2), "bar_bar")
        self.assertEqual(TestFunctions.test_bar(object, "bar", 2), "bar_bar")
        self.assertEqual(object.not_overriden(), "not_overriden")
        self.assertEqual(list(object.return_list()), ["A", "B", "C"])
        self.assertEqual(list(SubClassTest.test_list(object)), ["A", "B", "C"])

        x = TestFunctions.pass_through(object)
        self.assertEqual(id(x), id(object))

    def testCreateInstance(self):
        """Test derived instances can be created from managed code"""
        object = TestFunctions.create_instance(DerivedClass)
        self.assertEqual(object.foo(), "DerivedClass")
        self.assertEqual(TestFunctions.test_foo(object), "DerivedClass")
        self.assertEqual(object.bar("bar", 2), "bar_bar")
        self.assertEqual(TestFunctions.test_bar(object, "bar", 2), "bar_bar")
        self.assertEqual(object.not_overriden(), "not_overriden")

        x = TestFunctions.pass_through(object)
        self.assertEqual(id(x), id(object))

        object2 = TestFunctions.create_instance(InterfaceTestClass)
        self.assertEqual(object2.foo(), "InterfaceTestClass")
        self.assertEqual(TestFunctions.test_foo(object2), "InterfaceTestClass")
        self.assertEqual(object2.bar("bar", 2), "bar/bar")
        self.assertEqual(TestFunctions.test_bar(object2, "bar", 2), "bar/bar")

        y = TestFunctions.pass_through(object2)
        self.assertEqual(id(y), id(object2))

    def testEvents(self):
        class EventHandler:
            def handler(self, x, args):
                self.value = args.value

        event_handler = EventHandler()

        x = SubClassTest()
        x.TestEvent += event_handler.handler
        self.assertEqual(TestFunctions.test_event(x, 1), 1)
        self.assertEqual(event_handler.value, 1)

        i = InterfaceTestClass()
        self.assertRaises(NotImplementedException, TestFunctions.test_event, i, 2)

        d = DerivedEventTest()
        d.add_TestEvent(event_handler.handler)
        self.assertEqual(TestFunctions.test_event(d, 3), 3)
        self.assertEqual(event_handler.value, 3)
        self.assertEqual(len(d.event_handlers), 1)

    def test_isinstance(self):
        from System import Object
        from System import String

        a = [str(x) for x in range(0, 1000)]
        b = [String(x) for x in a]

        for x in a:
            self.assertFalse(isinstance(x, Object))
            self.assertFalse(isinstance(x, String))

        for x in b:
            self.assertTrue(isinstance(x, Object))
            self.assertTrue(isinstance(x, String))


def test_suite():
    return unittest.makeSuite(SubClassTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
