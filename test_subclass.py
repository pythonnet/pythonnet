# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================
import clr
clr.AddReference('Python.Test')
clr.AddReference('System')

import sys, os, string, unittest, types
from Python.Test import TestFunctions, SubClassTest, IInterfaceTest
from System.Collections.Generic import List

# class that implements the test interface
class InterfaceTestClass(IInterfaceTest):
    def foo(self):
        return "InterfaceTestClass"

    def bar(self, x, i):
        return "/".join([x] * i)

# class that derives from a class deriving from IInterfaceTest
class DerivedClass(SubClassTest):

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

def test_suite():
    return unittest.makeSuite(SubClassTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()
