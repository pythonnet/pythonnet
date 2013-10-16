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
from Python.Test import SubClassTest
from System.Collections.Generic import List

class DerivedClass(SubClassTest):

    def foo(self):
        return "bar"

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
        self.assertEqual(SubClassTest.test_foo(object), "foo")
        self.assertEqual(object.bar("bar", 2), "bar")
        self.assertEqual(SubClassTest.test_bar(object, "bar", 2), "bar")
        self.assertEqual(object.not_overriden(), "not_overriden")
        self.assertEqual(list(object.return_list()), ["a", "b", "c"])
        self.assertEqual(list(SubClassTest.test_list(object)), ["a", "b", "c"])

    def testDerivedClass(self):
        """Test python class derived from managed type"""
        object = DerivedClass()
        self.assertEqual(object.foo(), "bar")
        self.assertEqual(SubClassTest.test_foo(object), "bar")
        self.assertEqual(object.bar("bar", 2), "bar_bar")
        self.assertEqual(SubClassTest.test_bar(object, "bar", 2), "bar_bar")
        self.assertEqual(object.not_overriden(), "not_overriden")
        self.assertEqual(list(object.return_list()), ["A", "B", "C"])
        self.assertEqual(list(SubClassTest.test_list(object)), ["A", "B", "C"])

        x = SubClassTest.pass_through(object)
        self.assertEqual(id(x), id(object))

    def testCreateInstance(self):
        """Test derived instances can be created from managed code"""
        object = SubClassTest.create_instance(DerivedClass)
        self.assertEqual(object.foo(), "bar")
        self.assertEqual(SubClassTest.test_foo(object), "bar")
        self.assertEqual(object.bar("bar", 2), "bar_bar")
        self.assertEqual(SubClassTest.test_bar(object, "bar", 2), "bar_bar")
        self.assertEqual(object.not_overriden(), "not_overriden")

        x = SubClassTest.pass_through(object)
        self.assertEqual(id(x), id(object))

def test_suite():
    return unittest.makeSuite(SubClassTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()
