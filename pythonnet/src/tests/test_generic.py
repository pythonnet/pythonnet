# Copyright (c) 2001, 2002 Zope Corporation and Contributors.
#
# All Rights Reserved.
#
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.

from CLR.System.Collections.Generic import Dictionary
import sys, os, string, unittest, types
import CLR.Python.Test as Test
import CLR.System as System


class GenericTests(unittest.TestCase):
    """Test CLR generics support."""

    def testGenericReferenceTypeDef(self):
        """Test usage of generic reference type definitions."""
        from CLR.Python.Test import GenericTypeDefinition
        bound = GenericTypeDefinition[System.String, System.Int32]
        inst = bound("one", 2)
        self.failUnless(inst.value1 == "one")
        self.failUnless(inst.value2 == 2)

    def testGenericValueTypeDef(self):
        """Test usage of generic value type definitions."""
        object = System.Nullable[System.Int32](10)
        self.failUnless(object.HasValue)
        # XXX  - more

    def testGenericInterfaceTypeDef(self):
        pass

    def testGenericDelegateTypeDef(self):
        pass

    def testOpenGenericType(self):
        """
        Test behavior of reflected open generic types.
        """
        from CLR.Python.Test import DerivedFromOpenGeneric

        OpenGenericType = DerivedFromOpenGeneric.__bases__[0]
        def test():
            bound = OpenGenericType()

        self.failUnlessRaises(TypeError, test)

        type = OpenGenericType[System.String]
        inst = type(1, 'two')
        self.failUnless(inst.value1 == 1)
        self.failUnless(inst.value2 == 'two')

    def testDerivedFromOpenGenericType(self):
        """
        Test a generic type derived from an open generic type.
        """
        from CLR.Python.Test import DerivedFromOpenGeneric
        
        type = DerivedFromOpenGeneric[System.String, System.String]
        inst = type(1, 'two', 'three')

        self.failUnless(inst.value1 == 1)
        self.failUnless(inst.value2 == 'two')
        self.failUnless(inst.value3 == 'three')


    def testClosedGenericType(self):
        pass

    def testGenericTypeNameResolution(self):
        pass

    def testPythonTypeAliasing(self):
        """Test python type alias support with generics."""
        dict = Dictionary[str, str]()
        self.assertEquals(dict.Count, 0)
        dict.Add("one", "one")
        self.failUnless(dict["one"] == "one")

        dict = Dictionary[System.String, System.String]()
        self.assertEquals(dict.Count, 0)
        dict.Add("one", "one")
        self.failUnless(dict["one"] == "one")

        dict = Dictionary[int, int]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1, 1)
        self.failUnless(dict[1] == 1)

        dict = Dictionary[System.Int32, System.Int32]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1, 1)
        self.failUnless(dict[1] == 1)       

        dict = Dictionary[long, long]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1L, 1L)
        self.failUnless(dict[1L] == 1L)

        dict = Dictionary[System.Int64, System.Int64]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1L, 1L)
        self.failUnless(dict[1L] == 1L)

        dict = Dictionary[float, float]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1.5, 1.5)
        self.failUnless(dict[1.5] == 1.5)

        dict = Dictionary[System.Double, System.Double]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1.5, 1.5)
        self.failUnless(dict[1.5] == 1.5)

        dict = Dictionary[bool, bool]()
        self.assertEquals(dict.Count, 0)
        dict.Add(True, False)
        self.failUnless(dict[True] == False)

        dict = Dictionary[System.Boolean, System.Boolean]()
        self.assertEquals(dict.Count, 0)
        dict.Add(True, False)
        self.failUnless(dict[True] == False)


def test_suite():
    return unittest.makeSuite(GenericTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    testcase.setup()
    main()

