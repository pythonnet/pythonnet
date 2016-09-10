import clr

clr.AddReference('Python.Test')

from System.Collections.Generic import Dictionary, List
import sys, os, string, unittest, types
import Python.Test as Test
import System
import six

if six.PY3:
    long = int
    unichr = chr
    unicode = str


class GenericTests(unittest.TestCase):
    """Test CLR generics support."""

    def testPythonTypeAliasing(self):
        """Test python type alias support with generics."""
        dict = Dictionary[str, str]()
        self.assertEquals(dict.Count, 0)
        dict.Add("one", "one")
        self.assertTrue(dict["one"] == "one")

        dict = Dictionary[System.String, System.String]()
        self.assertEquals(dict.Count, 0)
        dict.Add("one", "one")
        self.assertTrue(dict["one"] == "one")

        dict = Dictionary[int, int]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1, 1)
        self.assertTrue(dict[1] == 1)

        dict = Dictionary[System.Int32, System.Int32]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1, 1)
        self.assertTrue(dict[1] == 1)

        dict = Dictionary[long, long]()
        self.assertEquals(dict.Count, 0)
        dict.Add(long(1), long(1))
        self.assertTrue(dict[long(1)] == long(1))

        dict = Dictionary[System.Int64, System.Int64]()
        self.assertEquals(dict.Count, 0)
        dict.Add(long(1), long(1))
        self.assertTrue(dict[long(1)] == long(1))

        dict = Dictionary[float, float]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1.5, 1.5)
        self.assertTrue(dict[1.5] == 1.5)

        dict = Dictionary[System.Double, System.Double]()
        self.assertEquals(dict.Count, 0)
        dict.Add(1.5, 1.5)
        self.assertTrue(dict[1.5] == 1.5)

        dict = Dictionary[bool, bool]()
        self.assertEquals(dict.Count, 0)
        dict.Add(True, False)
        self.assertTrue(dict[True] == False)

        dict = Dictionary[System.Boolean, System.Boolean]()
        self.assertEquals(dict.Count, 0)
        dict.Add(True, False)
        self.assertTrue(dict[True] == False)

    def testGenericReferenceType(self):
        """Test usage of generic reference type definitions."""
        from Python.Test import GenericTypeDefinition
        inst = GenericTypeDefinition[System.String, System.Int32]("one", 2)
        self.assertTrue(inst.value1 == "one")
        self.assertTrue(inst.value2 == 2)

    def testGenericValueType(self):
        """Test usage of generic value type definitions."""
        inst = System.Nullable[System.Int32](10)
        self.assertTrue(inst.HasValue)
        self.assertTrue(inst.Value == 10)

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

        self.assertRaises(TypeError, test)

        def test():
            type = OpenGenericType[System.String]

        self.assertRaises(TypeError, test)

    def testDerivedFromOpenGenericType(self):
        """
        Test a generic type derived from an open generic type.
        """
        from Python.Test import DerivedFromOpenGeneric

        type = DerivedFromOpenGeneric[System.String, System.String]
        inst = type(1, 'two', 'three')

        self.assertTrue(inst.value1 == 1)
        self.assertTrue(inst.value2 == 'two')
        self.assertTrue(inst.value3 == 'three')

    def testGenericTypeNameResolution(self):
        """
        Test the ability to disambiguate generic type names.
        """
        from Python.Test import GenericNameTest1, GenericNameTest2

        # If both a non-generic and generic type exist for a name, the
        # unadorned name always resolves to the non-generic type.
        _class = GenericNameTest1
        self.assertTrue(_class().value == 0)
        self.assertTrue(_class.value == 0)

        # If no non-generic type exists for a name, the unadorned name
        # cannot be instantiated. It can only be used to bind a generic.

        def test():
            inst = GenericNameTest2()

        self.assertRaises(TypeError, test)

        _class = GenericNameTest2[int]
        self.assertTrue(_class().value == 1)
        self.assertTrue(_class.value == 1)

        _class = GenericNameTest2[int, int]
        self.assertTrue(_class().value == 2)
        self.assertTrue(_class.value == 2)

    def _testGenericWrapperByType(self, ptype, value, test_type=0):
        from Python.Test import GenericWrapper
        import System

        inst = GenericWrapper[ptype](value)
        self.assertTrue(inst.value == value)

        atype = System.Array[ptype]
        items = atype([value, value, value])
        inst = GenericWrapper[atype](items)
        self.assertTrue(len(inst.value) == 3)
        self.assertTrue(inst.value[0] == value)
        self.assertTrue(inst.value[1] == value)

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
        self._testGenericWrapperByType(System.Char, six.u('A'))
        self._testGenericWrapperByType(System.Int16, 32767)
        self._testGenericWrapperByType(System.Int32, 2147483647)
        self._testGenericWrapperByType(int, 2147483647)
        self._testGenericWrapperByType(System.Int64, long(9223372036854775807))
        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            self._testGenericWrapperByType(long, long(9223372036854775807))
        self._testGenericWrapperByType(System.UInt16, 65000)
        self._testGenericWrapperByType(System.UInt32, long(4294967295))
        self._testGenericWrapperByType(System.UInt64, long(18446744073709551615))
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
        if test_type:
            self.assertTrue(result.__class__ == value.__class__)
        else:
            self.assertTrue(result == value)

        # Type inference (static method)
        result = stype.Overloaded(value)
        self.assertTrue(result == value)
        if test_type:
            self.assertTrue(result.__class__ == value.__class__)
        else:
            self.assertTrue(result == value)

        # Explicit selection (instance method)
        result = itype().Overloaded[ptype](value)
        self.assertTrue(result == value)
        if test_type:
            self.assertTrue(result.__class__ == value.__class__)
        else:
            self.assertTrue(result == value)

        # Type inference (instance method)
        result = itype().Overloaded(value)
        self.assertTrue(result == value)
        if test_type:
            self.assertTrue(result.__class__ == value.__class__)
        else:
            self.assertTrue(result == value)

        atype = System.Array[ptype]
        items = atype([value, value, value])

        # Explicit selection (static method)
        result = stype.Overloaded[atype](items)
        if test_type:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0].__class__ == value.__class__)
            self.assertTrue(result[1].__class__ == value.__class__)
        else:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0] == value)
            self.assertTrue(result[1] == value)

        # Type inference (static method)
        result = stype.Overloaded(items)
        if test_type:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0].__class__ == value.__class__)
            self.assertTrue(result[1].__class__ == value.__class__)
        else:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0] == value)
            self.assertTrue(result[1] == value)

        # Explicit selection (instance method)
        result = itype().Overloaded[atype](items)
        if test_type:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0].__class__ == value.__class__)
            self.assertTrue(result[1].__class__ == value.__class__)
        else:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0] == value)
            self.assertTrue(result[1] == value)

        # Type inference (instance method)
        result = itype().Overloaded(items)
        if test_type:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0].__class__ == value.__class__)
            self.assertTrue(result[1].__class__ == value.__class__)
        else:
            self.assertTrue(len(result) == 3)
            self.assertTrue(result[0] == value)
            self.assertTrue(result[1] == value)

    def testGenericMethodBinding(self):
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        from System import InvalidOperationException

        # Can invoke a static member on a closed generic type.
        value = GenericStaticMethodTest[str].Overloaded()
        self.assertTrue(value == 1)

        def test():
            # Cannot invoke a static member on an open type.
            GenericStaticMethodTest.Overloaded()

        self.assertRaises(InvalidOperationException, test)

        # Can invoke an instance member on a closed generic type.
        value = GenericMethodTest[str]().Overloaded()
        self.assertTrue(value == 1)

        def test():
            # Cannot invoke an instance member on an open type,
            # because the open type cannot be instantiated.
            GenericMethodTest().Overloaded()

        self.assertRaises(TypeError, test)

    def testGenericMethodTypeHandling(self):
        """
        Test argument conversion / binding for generic methods.
        """
        from Python.Test import InterfaceTest, ISayHello1, ShortEnum
        import System

        # XXX BUG: The value doesn't fit into Int64 and PythonNet doesn't
        # recognize it as UInt64 for unknown reasons.
        ##        self._testGenericMethodByType(System.UInt64, 18446744073709551615L)
        self._testGenericMethodByType(System.Boolean, True)
        self._testGenericMethodByType(bool, True)
        self._testGenericMethodByType(System.Byte, 255)
        self._testGenericMethodByType(System.SByte, 127)
        self._testGenericMethodByType(System.Char, six.u('A'))
        self._testGenericMethodByType(System.Int16, 32767)
        self._testGenericMethodByType(System.Int32, 2147483647)
        self._testGenericMethodByType(int, 2147483647)
        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            self._testGenericMethodByType(System.Int64, long(9223372036854775807))
            self._testGenericMethodByType(long, long(9223372036854775807))
            self._testGenericMethodByType(System.UInt32, long(4294967295))
            self._testGenericMethodByType(System.Int64, long(1844674407370955161))
        self._testGenericMethodByType(System.UInt16, 65000)
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

    def testCorrectOverloadSelection(self):
        """
        Test correct overloading selection for common types.
        """
        from System.Drawing import Font

        from System import (String, Double, Single,
                            Int16, Int32, Int64)
        from System import Math

        substr = String("substring")
        self.assertTrue(substr.Substring(2) == substr.Substring.__overloads__[Int32](
            Int32(2)))
        self.assertTrue(substr.Substring(2, 3) == substr.Substring.__overloads__[Int32, Int32](
            Int32(2), Int32(3)))

        for atype, value1, value2 in zip([Double, Single, Int16, Int32, Int64],
                                         [1.0, 1.0, 1, 1, 1],
                                         [2.0, 0.5, 2, 0, -1]):
            self.assertTrue(Math.Abs(atype(value1)) == Math.Abs.__overloads__[atype](atype(value1)))
            self.assertTrue(Math.Abs(value1) == Math.Abs.__overloads__[atype](atype(value1)))
            self.assertTrue(
                Math.Max(atype(value1),
                         atype(value2)) == Math.Max.__overloads__[atype, atype](
                    atype(value1),
                    atype(value2)))
            if (atype is Int64) and six.PY2:
                value2 = long(value2)
            self.assertTrue(
                Math.Max(atype(value1),
                         value2) == Math.Max.__overloads__[atype, atype](
                    atype(value1),
                    atype(value2)))

        clr.AddReference("System.Runtime.InteropServices")
        from System.Runtime.InteropServices import GCHandle, GCHandleType
        from System import Array, Byte
        CSArray = Array.CreateInstance(Byte, 1000)
        handler = GCHandle.Alloc(CSArray, GCHandleType.Pinned)

    def testGenericMethodOverloadSelection(self):
        """
        Test explicit overload selection with generic methods.
        """
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        type = GenericStaticMethodTest[str]
        inst = GenericMethodTest[str]()

        # public static int Overloaded()
        value = type.Overloaded()
        self.assertTrue(value == 1)

        # public int Overloaded()
        value = inst.Overloaded()
        self.assertTrue(value == 1)

        # public static T Overloaded(T arg) (inferred)
        value = type.Overloaded("test")
        self.assertTrue(value == "test")

        # public T Overloaded(T arg) (inferred)
        value = inst.Overloaded("test")
        self.assertTrue(value == "test")

        # public static T Overloaded(T arg) (explicit)
        value = type.Overloaded[str]("test")
        self.assertTrue(value == "test")

        # public T Overloaded(T arg) (explicit)
        value = inst.Overloaded[str]("test")
        self.assertTrue(value == "test")

        # public static Q Overloaded<Q>(Q arg)
        value = type.Overloaded[float](2.2)
        self.assertTrue(value == 2.2)

        # public Q Overloaded<Q>(Q arg)
        value = inst.Overloaded[float](2.2)
        self.assertTrue(value == 2.2)

        # public static Q Overloaded<Q>(Q arg)
        value = type.Overloaded[bool](True)
        self.assertTrue(value == True)

        # public Q Overloaded<Q>(Q arg)
        value = inst.Overloaded[bool](True)
        self.assertTrue(value == True)

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[bool, str](True, "true")
        self.assertTrue(value == "true")

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[bool, str](True, "true")
        self.assertTrue(value == "true")

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[str, bool]("true", True)
        self.assertTrue(value == True)

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[str, bool]("true", True)
        self.assertTrue(value == True)

        # public static string Overloaded<T>(int arg1, int arg2, string arg3)
        value = type.Overloaded[str](123, 456, "success")
        self.assertTrue(value == "success")

        # public string Overloaded<T>(int arg1, int arg2, string arg3)
        value = inst.Overloaded[str](123, 456, "success")
        self.assertTrue(value == "success")

        def test():
            value = type.Overloaded[str, bool, int]("true", True, 1)

        self.assertRaises(TypeError, test)

        def test():
            value = inst.Overloaded[str, bool, int]("true", True, 1)

        self.assertRaises(TypeError, test)

    def testMethodOverloadSelectionWithGenericTypes(self):
        """Check method overload selection using generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper
        inst = InterfaceTest()

        vtype = GenericWrapper[System.Boolean]
        input = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == True)

        vtype = GenericWrapper[bool]
        input = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == True)

        vtype = GenericWrapper[System.Byte]
        input = vtype(255)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 255)

        vtype = GenericWrapper[System.SByte]
        input = vtype(127)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 127)

        vtype = GenericWrapper[System.Char]
        input = vtype(six.u('A'))
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == six.u('A'))

        vtype = GenericWrapper[System.Char]
        input = vtype(65535)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == unichr(65535))

        vtype = GenericWrapper[System.Int16]
        input = vtype(32767)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 32767)

        vtype = GenericWrapper[System.Int32]
        input = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 2147483647)

        vtype = GenericWrapper[int]
        input = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 2147483647)

        vtype = GenericWrapper[System.Int64]
        input = vtype(long(9223372036854775807))
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            vtype = GenericWrapper[long]
            input = vtype(long(9223372036854775807))
            value = MethodTest.Overloaded.__overloads__[vtype](input)
            self.assertTrue(value.value == long(9223372036854775807))

        vtype = GenericWrapper[System.UInt16]
        input = vtype(65000)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 65000)

        vtype = GenericWrapper[System.UInt32]
        input = vtype(long(4294967295))
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == long(4294967295))

        vtype = GenericWrapper[System.UInt64]
        input = vtype(long(18446744073709551615))
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == long(18446744073709551615))

        vtype = GenericWrapper[System.Single]
        input = vtype(3.402823e38)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 3.402823e38)

        vtype = GenericWrapper[System.Double]
        input = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[float]
        input = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[System.Decimal]
        input = vtype(System.Decimal.One)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == System.Decimal.One)

        vtype = GenericWrapper[System.String]
        input = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == "spam")

        vtype = GenericWrapper[str]
        input = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == "spam")

        vtype = GenericWrapper[ShortEnum]
        input = vtype(ShortEnum.Zero)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value == ShortEnum.Zero)

        vtype = GenericWrapper[System.Object]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[InterfaceTest]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[ISayHello1]
        input = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = System.Array[GenericWrapper[int]]
        input = vtype([GenericWrapper[int](0), GenericWrapper[int](1)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 0)
        self.assertTrue(value[1].value == 1)

    def testOverloadSelectionWithArraysOfGenericTypes(self):
        """Check overload selection using arrays of generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper
        inst = InterfaceTest()

        gtype = GenericWrapper[System.Boolean]
        vtype = System.Array[gtype]
        input = vtype([gtype(True), gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == True)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[bool]
        vtype = System.Array[gtype]
        input = vtype([gtype(True), gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == True)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Byte]
        vtype = System.Array[gtype]
        input = vtype([gtype(255), gtype(255)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 255)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.SByte]
        vtype = System.Array[gtype]
        input = vtype([gtype(127), gtype(127)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 127)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input = vtype([gtype(six.u('A')), gtype(six.u('A'))])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == six.u('A'))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input = vtype([gtype(65535), gtype(65535)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == unichr(65535))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int16]
        vtype = System.Array[gtype]
        input = vtype([gtype(32767), gtype(32767)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 32767)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int32]
        vtype = System.Array[gtype]
        input = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 2147483647)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[int]
        vtype = System.Array[gtype]
        input = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 2147483647)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int64]
        vtype = System.Array[gtype]
        input = vtype([gtype(long(9223372036854775807)),
                       gtype(long(9223372036854775807))])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == long(9223372036854775807))
        self.assertTrue(value.Length == 2)

        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            gtype = GenericWrapper[long]
            vtype = System.Array[gtype]
            input = vtype([gtype(long(9223372036854775807)),
                           gtype(long(9223372036854775807))])
            value = MethodTest.Overloaded.__overloads__[vtype](input)
            self.assertTrue(value[0].value == long(9223372036854775807))
            self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt16]
        vtype = System.Array[gtype]
        input = vtype([gtype(65000), gtype(65000)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 65000)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt32]
        vtype = System.Array[gtype]
        input = vtype([gtype(long(4294967295)), gtype(long(4294967295))])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == long(4294967295))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt64]
        vtype = System.Array[gtype]
        input = vtype([gtype(long(18446744073709551615)),
                       gtype(long(18446744073709551615))])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == long(18446744073709551615))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Single]
        vtype = System.Array[gtype]
        input = vtype([gtype(3.402823e38), gtype(3.402823e38)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 3.402823e38)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Double]
        vtype = System.Array[gtype]
        input = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[float]
        vtype = System.Array[gtype]
        input = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Decimal]
        vtype = System.Array[gtype]
        input = vtype([gtype(System.Decimal.One),
                       gtype(System.Decimal.One)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == System.Decimal.One)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.String]
        vtype = System.Array[gtype]
        input = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == "spam")
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[str]
        vtype = System.Array[gtype]
        input = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == "spam")
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[ShortEnum]
        vtype = System.Array[gtype]
        input = vtype([gtype(ShortEnum.Zero), gtype(ShortEnum.Zero)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value == ShortEnum.Zero)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Object]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[InterfaceTest]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[ISayHello1]
        vtype = System.Array[gtype]
        input = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

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
