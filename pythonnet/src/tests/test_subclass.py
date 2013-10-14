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

import sys, os, string, unittest, types
from Python.Test import SubClassTest

class DerivedClass(SubClassTest):
	def foo(self):
		return "bar"

class SubClassTests(unittest.TestCase):
    """Test subclassing managed types"""

    def testSubClass(self):
        """Test subclassing managed types"""
        object = SubClassTest()
        self.assertEqual(object.foo(), "foo")
        self.assertEqual(SubClassTest.test(object), "foo")

        object = DerivedClass()
        self.assertEqual(object.foo(), "bar")
        self.assertEqual(SubClassTest.test(object), "bar")

def test_suite():
    return unittest.makeSuite(SubClassTests)

def main():
    for i in range(50):
        unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()
