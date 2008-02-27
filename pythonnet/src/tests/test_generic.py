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
        from Python.Test import GenericNameTest1, GenericNameTest2

        # If both a non-generic and generic type exist for a name, the
        # unadorned name always resolves to the non-generic type.
        _class = GenericNameTest1
        self.failUnless(_class().value == 0)
        self.failUnless(_class.value == 0)

        # If no non-generic type exists for a name, the unadorned name
        # cannot be instantiated. It can only be used to bind a generic.

        def test():
            inst = GenericNameTest2()

        self.failUnlessRaises(TypeError, test)

        _class = GenericNameTest2[int]
        self.failUnless(_class().value == 1)
        self.failUnless(_class.value == 1)

        _class = GenericNameTest2[int, int]
        self.failUnless(_class().value == 2)
        self.failUnless(_class.value == 2)

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

        # Explicit selection (static method)
        result = stype.Overloaded[ptype](value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        # Type inference (static method)
        result = stype.Overloaded(value)
        self.failUnless(result == value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        # Explicit selection (instance method)
        result = itype().Overloaded[ptype](value)
        self.failUnless(result == value)        
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        # Type inference (instance method)
        result = itype().Overloaded(value)
        self.failUnless(result == value)
        if test_type: self.failUnless(result.__class__ == value.__class__)
        else:         self.failUnless(result == value)

        atype = System.Array[ptype]
        items = atype([value, value, value])

        # Explicit selection (static method)
        result = stype.Overloaded[atype](items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        # Type inference (static method)
        result = stype.Overloaded(items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        # Explicit selection (instance method)
        result = itype().Overloaded[atype](items)
        if test_type:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0].__class__ == value.__class__)
            self.failUnless(result[1].__class__ == value.__class__)            
        else:
            self.failUnless(len(result) == 3)
            self.failUnless(result[0] == value)
            self.failUnless(result[1] == value)            

        # Type inference (instance method)
        result = itype().Overloaded(items)
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
        from System import InvalidOperationException
        
        # Can invoke a static member on a closed generic type.
        value = GenericStaticMethodTest[str].Overloaded()
        self.failUnless(value == 1)

        def test():
            # Cannot invoke a static member on an open type.
            GenericStaticMethodTest.Overloaded()

        self.failUnlessRaises(InvalidOperationException, test)

        # Can invoke an instance member on a closed generic type.
        value = GenericMethodTest[str]().Overloaded()
        self.failUnless(value == 1)

        def test():
            # Cannot invoke an instance member on an open type,
            # because the open type cannot be instantiated.
            GenericMethodTest().Overloaded()

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
        self._testGenericMethodByType(System.UInt64, 1844674407370955161L)
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
        # XXX BUG: The value doesn't fit into Int64 and PythonNet doesn't
        # recognize it as UInt64 for unknown reasons.
        self._testGenericMethodByType(System.UInt64, 18446744073709551615L)


    def testGenericMethodOverloadSelection(self):
        """
        Test explicit overload selection with generic methods.
        """
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        type = GenericStaticMethodTest[str]
        inst = GenericMethodTest[str]()

        # public static int Overloaded()
        value = type.Overloaded()
        self.failUnless(value == 1)

        # public int Overloaded()
        value = inst.Overloaded()
        self.failUnless(value == 1)

        # public static T Overloaded(T arg) (inferred)
        value = type.Overloaded("test")
        self.failUnless(value == "test")

        # public T Overloaded(T arg) (inferred)
        value = inst.Overloaded("test")
        self.failUnless(value == "test")

        # public static T Overloaded(T arg) (explicit)
        value = type.Overloaded[str]("test")
        self.failUnless(value == "test")

        # public T Overloaded(T arg) (explicit)
        value = inst.Overloaded[str]("test")
        self.failUnless(value == "test")

        # public static Q Overloaded<Q>(Q arg)
        value = type.Overloaded[float](2.2)
        self.failUnless(value == 2.2)

        # public Q Overloaded<Q>(Q arg)
        value = inst.Overloaded[float](2.2)
        self.failUnless(value == 2.2)

        # public static Q Overloaded<Q>(Q arg)
        value = type.Overloaded[bool](True)
        self.failUnless(value == True)

        # public Q Overloaded<Q>(Q arg)
        value = inst.Overloaded[bool](True)
        self.failUnless(value == True)

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[bool, str](True, "true")
        self.failUnless(value == "true")

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[bool, str](True, "true")
        self.failUnless(value == "true")

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[str, bool]("true", True)
        self.failUnless(value == True)

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[str, bool]("true", True)
        self.failUnless(value == True)

        # public static string Overloaded<T>(int arg1, int arg2, string arg3)
        value = type.Overloaded[str](123, 456, "success")
        self.failUnless(value == "success")

        # public string Overloaded<T>(int arg1, int arg2, string arg3)
        value = inst.Overloaded[str](123, 456, "success")
        self.failUnless(value == "success")

        def test():
            value = type.Overloaded[str, bool, int]("true", True, 1)
        self.failUnlessRaises(TypeError, test)

        def test():
            value = inst.Overloaded[str, bool, int]("true", True, 1)

        self.failUnlessRaises(TypeError, test)

    def testMethodOverloadSelectionWithGenericTypes(self):
        """Check method overload selection using generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper
        inst = InterfaceTest()

        vtype = GenericWrapper[System.Boolean]
        input = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == True)

        vtype = GenericWrapper[bool]
        input = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == True)

        vtype = GenericWrapper[System.Byte]
        input = vtype(255)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 255)

        vtype = GenericWrapper[System.SByte]
        input = vtype(127)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 127)

        vtype = GenericWrapper[System.Char]
        input = vtype(u'A')
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == u'A')

        vtype = GenericWrapper[System.Char]
        input = vtype(65535)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == unichr(65535))

        vtype = GenericWrapper[System.Int16]
        input = vtype(32767)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 32767)

        vtype = GenericWrapper[System.Int32]
        input = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 2147483647)

        vtype = GenericWrapper[int]
        input = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 2147483647)

        vtype = GenericWrapper[System.Int64]
        input = vtype(9223372036854775807L)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 9223372036854775807L)

        vtype = GenericWrapper[long]
        input = vtype(9223372036854775807L)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 9223372036854775807L)

        vtype = GenericWrapper[System.UInt16]
        input = vtype(65000)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 65000)

        vtype = GenericWrapper[System.UInt32]
        input = vtype(4294967295L)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 4294967295L)

        vtype = GenericWrapper[System.UInt64]
        input = vtype(18446744073709551615L)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 18446744073709551615L)

        vtype = GenericWrapper[System.Single]
        input = vtype(3.402823e38)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 3.402823e38)

        vtype = GenericWrapper[System.Double]
        input = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[float]
        input = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[System.Decimal]
        input = vtype(System.Decimal.One)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == System.Decimal.One)

        vtype = GenericWrapper[System.String]
        input = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == "spam")

        vtype = GenericWrapper[str]
        input = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == "spam")

        vtype = GenericWrapper[ShortEnum]
        input = vtype(ShortEnum.Zero)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value == ShortEnum.Zero)

        vtype = GenericWrapper[System.Object]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[InterfaceTest]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[ISayHello1]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value.value.__class__ == inst.__class__)

        vtype = System.Array[GenericWrapper[int]]
        input = vtype([GenericWrapper[int](0), GenericWrapper[int](1)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 0)
        self.failUnless(value[1].value == 1)        

    def testOverloadSelectionWithArraysOfGenericTypes(self):
        """Check overload selection using arrays of generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper
        inst = InterfaceTest()

        gtype = GenericWrapper[System.Boolean]
        vtype = System.Array[gtype]
        input = vtype([gtype(True),gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == True)
        self.failUnless(value.Length == 2)

        gtype = GenericWrapper[bool]
        vtype = System.Array[gtype]
        input = vtype([gtype(True), gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == True)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Byte]
        vtype = System.Array[gtype]
        input = vtype([gtype(255), gtype(255)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 255)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.SByte]
        vtype = System.Array[gtype]
        input = vtype([gtype(127), gtype(127)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 127)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input = vtype([gtype(u'A'), gtype(u'A')])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == u'A')
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input = vtype([gtype(65535), gtype(65535)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == unichr(65535))
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Int16]
        vtype = System.Array[gtype]
        input = vtype([gtype(32767),gtype(32767)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 32767)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Int32]
        vtype = System.Array[gtype]
        input = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 2147483647)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[int]
        vtype = System.Array[gtype]
        input = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 2147483647)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Int64]
        vtype = System.Array[gtype]
        input = vtype([gtype(9223372036854775807L),
                       gtype(9223372036854775807L)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 9223372036854775807L)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[long]
        vtype = System.Array[gtype]
        input = vtype([gtype(9223372036854775807L),
                       gtype(9223372036854775807L)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 9223372036854775807L)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.UInt16]
        vtype = System.Array[gtype]
        input = vtype([gtype(65000), gtype(65000)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 65000)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.UInt32]
        vtype = System.Array[gtype]
        input = vtype([gtype(4294967295L), gtype(4294967295L)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 4294967295L)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.UInt64]
        vtype = System.Array[gtype]
        input = vtype([gtype(18446744073709551615L),
                       gtype(18446744073709551615L)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 18446744073709551615L)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Single]
        vtype = System.Array[gtype]
        input = vtype([gtype(3.402823e38), gtype(3.402823e38)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 3.402823e38)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Double]
        vtype = System.Array[gtype]
        input = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 1.7976931348623157e308)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[float]
        vtype = System.Array[gtype]
        input = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == 1.7976931348623157e308)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Decimal]
        vtype = System.Array[gtype]
        input = vtype([gtype(System.Decimal.One),
                       gtype(System.Decimal.One)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == System.Decimal.One)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.String]
        vtype = System.Array[gtype]
        input = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == "spam")
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[str]
        vtype = System.Array[gtype]
        input = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == "spam")
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[ShortEnum]
        vtype = System.Array[gtype]
        input = vtype([gtype(ShortEnum.Zero), gtype(ShortEnum.Zero)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value == ShortEnum.Zero)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[System.Object]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value.__class__ == inst.__class__)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[InterfaceTest]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value.__class__ == inst.__class__)
        self.failUnless(value.Length == 2)
        
        gtype = GenericWrapper[ISayHello1]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].value.__class__ == inst.__class__)
        self.failUnless(value.Length == 2)

    def testGenericOverloadSelectionMagicNameOnly(self):
        """Test using only __overloads__ to select on type & sig"""
        # XXX NotImplemented
        pass

    def testNestedGenericClass(self):
        """Check nested generic classes."""
        # XXX NotImplemented
        pass
    


def test_suite():
    return unittest.makeSuite(GenericTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()


