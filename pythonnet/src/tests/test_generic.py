# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

from System.Collections.Generic import Dictionary
import sys, os, string, unittest, types
import Python.Test as Test
import System


class GenericTests(unittest.TestCase):
    """Test CLR generics support."""

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

    def testGenericReferenceType(self):
        """Test usage of generic reference type definitions."""
        from Python.Test import GenericTypeDefinition
        inst = GenericTypeDefinition[System.String, System.Int32]("one", 2)
        self.failUnless(inst.value1 == "one")
        self.failUnless(inst.value2 == 2)

    def testGenericValueType(self):
        """Test usage of generic value type definitions."""
        inst = System.Nullable[System.Int32](10)
        self.failUnless(inst.HasValue)
        self.failUnless(inst.Value == 10)


    def testGenericInterface(self):
        pass

    def testGenericDelegate(self):
        pass

    def testOpenGenericType(self):
        """
        Test behavior of reflected open constructed generic types.
        """
        from Python.Test import DerivedFromOpenGeneric

        OpenGenericType = DerivedFromOpenGeneric.__bases__[0]
        def test():
            inst = OpenGenericType()

        self.failUnlessRaises(TypeError, test)

        def test():
            type = OpenGenericType[System.String]
            
        self.failUnlessRaises(TypeError, test)

    def testDerivedFromOpenGenericType(self):
        """
        Test a generic type derived from an open generic type.
        """
        from Python.Test import DerivedFromOpenGeneric
        
        type = DerivedFromOpenGeneric[System.String, System.String]
        inst = type(1, 'two', 'three')

        self.failUnless(inst.value1 == 1)
        self.failUnless(inst.value2 == 'two')
        self.failUnless(inst.value3 == 'three')

    def testGenericTypeNameResolution(self):
        """
        Test the ability to disambiguate generic type names.
        """
        from Python.Test import GenericNameTest1

        # If both a non-generic and generic type exist for a name, the
        # unadorned name always resolves to the non-generic type.
        inst = GenericNameTest1()
        self.failUnless(_class().value == 0)
        self.failUnless(_class.value == 0)

        # We can also explicitly select the non-generic type using the []
        # syntax by passing None for the type binding argument.
        _class = GenericNameTest1[None]
        self.failUnless(_class().value == 0)
        self.failUnless(_class.value == 0)

        # If no non-generic type exists for a name, the unadorned name
        # cannot be instantiated. It can only be used to bind a generic.

        def test():
            inst = GenericcNameTest2()

        self.failUnlessRaises(TypeError, test)

        _class = GenericNameTest2[int]
        self.failUnless(_class().value == 1)
        self.failUnless(_class.value == 1)

        _class = GenericNameTest2[int, int]
        self.failUnless(_class().value == 2)
        self.failUnless(_class.value == 2)

    def testGenericTypeIntrospectionMurkiness(self):
        """
        Test (and document) some murky areas with overloaded type names.
        """
        raise # dir(GenericNameTest1) ?
        

    def _testGenericWrapperByType(self, ptype, value, test_type=0):
        from Python.Test import GenericWrapper
        import System

        inst = GenericWrapper[ptype](value)
        self.failUnless(inst.value == value)

        atype = System.Array[ptype]
        items = atype([value, value, value])
        inst = GenericWrapper[atype](items)
        self.failUnless(len(inst.value) == 3)
        self.failUnless(inst.value[0] == value)
        self.failUnless(inst.value[1] == value)            
        
    def testGenericTypeBinding(self):
        """
        Test argument conversion / binding for generic methods.
        """
        from Python.Test import InterfaceTest, ISayHello1, ShortEnum
        import System
        
        self._testGenericWrapperByType(System.Boolean, True)
        self._testGenericWrapperByType(bool, True)
        self._testGenericWrapperByType(System.Byte, 255)
        self._testGenericWrapperByType(System.SByte, 127)
        self._testGenericWrapperByType(System.Char, u'A')
        self._testGenericWrapperByType(System.Int16, 32767)
        self._testGenericWrapperByType(System.Int32, 2147483647)
        self._testGenericWrapperByType(int, 2147483647)
        self._testGenericWrapperByType(System.Int64, 9223372036854775807L)
        self._testGenericWrapperByType(long, 9223372036854775807L)         
        self._testGenericWrapperByType(System.UInt16, 65000)
        self._testGenericWrapperByType(System.UInt32, 4294967295L)
        self._testGenericWrapperByType(System.UInt64, 18446744073709551615L)
        self._testGenericWrapperByType(System.Single, 3.402823e38)
        self._testGenericWrapperByType(System.Double, 1.7976931348623157e308)
        self._testGenericWrapperByType(float, 1.7976931348623157e308)         
        self._testGenericWrapperByType(System.Decimal, System.Decimal.One)
        self._testGenericWrapperByType(System.String, "test")
        self._testGenericWrapperByType(unicode, "test")
        self._testGenericWrapperByType(str, "test")
        self._testGenericWrapperByType(ShortEnum, ShortEnum.Zero)
        self._testGenericWrapperByType(System.Object, InterfaceTest())
        self._testGenericWrapperByType(InterfaceTest, InterfaceTest())
        self._testGenericWrapperByType(ISayHello1, InterfaceTest())

    def _testGenericMethodByType(self, ptype, value, test_type=0):
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        import System

        itype = GenericMethodTest[System.Type]
        stype = GenericStaticMethodTest[System.Type]

        result = stype.OverloadedMethod[ptype](value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        result = stype.OverloadedMethod(value)
        self.failUnless(result == value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        result = itype().OverloadedMethod[ptype](value)
        self.failUnless(result == value)        
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        result = itype().OverloadedMethod(value)
        self.failUnless(result == value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        atype = System.Array[ptype]
        items = atype([value, value, value])

        result = stype.OverloadedMethod[atype](items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        result = stype.OverloadedMethod(items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        result = itype().OverloadedMethod[atype](items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        result = itype().OverloadedMethod(items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

    def testGenericMethodBinding(self):
        from Python.Test import GenericMethodTest, GenericStaticMethodTest

        # Can invoke a static member on a closed generic type.
        value = GenericStaticMethodTest[str].OverloadedMethod()
        self.failUnless(value == 1)

        def test():
            # Cannot invoke a static member on an open type.
            GenericStaticMethodTest.OverloadedMethod()

        self.failUnlessRaises(TypeError, test)

        # Can invoke an instance member on a closed generic type.
        value = GenericMethodTest[str]().OverloadedMethod()
        self.failUnless(value == 1)

        def test():
            # Cannot invoke an instance member on an open type.
            GenericMethodTest().OverloadedMethod()

        self.failUnlessRaises(TypeError, test)

    def testGenericMethodTypeHandling(self):
        """
        Test argument conversion / binding for generic methods.
        """
        from Python.Test import InterfaceTest, ISayHello1, ShortEnum
        import System
        
        self._testGenericMethodByType(System.Boolean, True)
        self._testGenericMethodByType(bool, True)
        self._testGenericMethodByType(System.Byte, 255)
        self._testGenericMethodByType(System.SByte, 127)
        self._testGenericMethodByType(System.Char, u'A')
        self._testGenericMethodByType(System.Int16, 32767)
        self._testGenericMethodByType(System.Int32, 2147483647)
        self._testGenericMethodByType(int, 2147483647)
        self._testGenericMethodByType(System.Int64, 9223372036854775807L)
        self._testGenericMethodByType(long, 9223372036854775807L)         
        self._testGenericMethodByType(System.UInt16, 65000)
        self._testGenericMethodByType(System.UInt32, 4294967295L)
        self._testGenericMethodByType(System.UInt64, 18446744073709551615L)
        self._testGenericMethodByType(System.Single, 3.402823e38)
        self._testGenericMethodByType(System.Double, 1.7976931348623157e308)
        self._testGenericMethodByType(float, 1.7976931348623157e308)         
        self._testGenericMethodByType(System.Decimal, System.Decimal.One)
        self._testGenericMethodByType(System.String, "test")
        self._testGenericMethodByType(unicode, "test")
        self._testGenericMethodByType(str, "test")
        self._testGenericMethodByType(ShortEnum, ShortEnum.Zero)
        self._testGenericMethodByType(System.Object, InterfaceTest())
        self._testGenericMethodByType(InterfaceTest, InterfaceTest(), 1)
        self._testGenericMethodByType(ISayHello1, InterfaceTest(), 1)

    def testGenericMethodOverloadSelection(self):
        """
        Test explicit overload selection with generic methods.
        """
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        type = GenericStaticMethodTest[str]
        inst = GenericMethodTest[str]()

        # public static int OverloadedMethod()
        value = type.OverloadedMethod()
        self.failUnless(value == 1)

        # public int OverloadedMethod()
        value = inst.OverloadedMethod()
        self.failUnless(value == 1)
    
        # public static int OverloadedMethod(int)
        value = type.OverloadedMethod(2)
        self.failUnless(value == 2)

        # public static int OverloadedMethod(int) (explicit)
        value = type.OverloadedMethod[int](2)
        self.failUnless(value == 2)

        # public int OverloadedMethod(int)
        value = inst.OverloadedMethod(2)
        self.failUnless(value == 2)

        # public int OverloadedMethod(int) (explicit)
        value = inst.OverloadedMethod[int](2)
        self.failUnless(value == 2)

        # public static T OverloadedMethod(T arg) (inferred)
        value = type.OverloadedMethod("test")
        self.failUnless(value == "test")

        # public T OverloadedMethod(T arg) (inferred)
        value = inst.OverloadedMethod("test")
        self.failUnless(value == "test")

        # public static T OverloadedMethod(T arg) (explicit)
        value = type.OverloadedMethod[str]("test")
        self.failUnless(value == "test")

        # public T OverloadedMethod(T arg) (explicit)
        value = inst.OverloadedMethod[str]("test")
        self.failUnless(value == "test")

        # public static Q OverloadedMethod<Q>(Q arg)
        value = type.OverloadedMethod[float](2.2)
        self.failUnless(value == 2.2)

        # public Q OverloadedMethod<Q>(Q arg)
        value = inst.OverloadedMethod[float](2.2)
        self.failUnless(value == 2.2)

        # public static Q OverloadedMethod<Q>(Q arg)
        value = type.OverloadedMethod[bool](True)
        self.failUnless(value == True)

        # public Q OverloadedMethod<Q>(Q arg)
        value = inst.OverloadedMethod[bool](True)
        self.failUnless(value == True)

        # public static U OverloadedMethod<Q, U>(Q arg1, U arg2)
        value = type.OverloadedMethod[bool, str](True, "true")
        self.failUnless(value == "true")

        # public U OverloadedMethod<Q, U>(Q arg1, U arg2)
        value = inst.OverloadedMethod[bool, str](True, "true")
        self.failUnless(value == "true")

        # public static U OverloadedMethod<Q, U>(Q arg1, U arg2)
        value = type.OverloadedMethod[str, bool]("true", True)
        self.failUnless(value == True)

        # public U OverloadedMethod<Q, U>(Q arg1, U arg2)
        value = inst.OverloadedMethod[str, bool]("true", True)
        self.failUnless(value == True)

        def test():
            value = type.OverloadedMethod[str, bool, int]("true", True, 1)
        self.failUnlessRaises(TypeError, test)

        def test():
            value = inst.OverloadedMethod[str, bool, int]("true", True, 1)

        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(GenericTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    testcase.setup()
    main()

