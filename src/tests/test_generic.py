# -*- coding: utf-8 -*-

import clr
import unittest

import System

from _compat import PY2, long, unicode, unichr, zip


class GenericTests(unittest.TestCase):
    """Test CLR generics support."""

    def _assert_generic_wrapper_by_type(self, ptype, value):
        """Test Helper"""
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

    def _assert_generic_method_by_type(self, ptype, value, test_type=0):
        """Test Helper"""
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

    def test_python_type_aliasing(self):
        """Test python type alias support with generics."""
        from System.Collections.Generic import Dictionary

        dict_ = Dictionary[str, str]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add("one", "one")
        self.assertTrue(dict_["one"] == "one")

        dict_ = Dictionary[System.String, System.String]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add("one", "one")
        self.assertTrue(dict_["one"] == "one")

        dict_ = Dictionary[int, int]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(1, 1)
        self.assertTrue(dict_[1] == 1)

        dict_ = Dictionary[System.Int32, System.Int32]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(1, 1)
        self.assertTrue(dict_[1] == 1)

        dict_ = Dictionary[long, long]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(long(1), long(1))
        self.assertTrue(dict_[long(1)] == long(1))

        dict_ = Dictionary[System.Int64, System.Int64]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(long(1), long(1))
        self.assertTrue(dict_[long(1)] == long(1))

        dict_ = Dictionary[float, float]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(1.5, 1.5)
        self.assertTrue(dict_[1.5] == 1.5)

        dict_ = Dictionary[System.Double, System.Double]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(1.5, 1.5)
        self.assertTrue(dict_[1.5] == 1.5)

        dict_ = Dictionary[bool, bool]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(True, False)
        self.assertTrue(dict_[True] is False)

        dict_ = Dictionary[System.Boolean, System.Boolean]()
        self.assertEquals(dict_.Count, 0)
        dict_.Add(True, False)
        self.assertTrue(dict_[True] is False)

    def test_generic_reference_type(self):
        """Test usage of generic reference type definitions."""
        from Python.Test import GenericTypeDefinition

        inst = GenericTypeDefinition[System.String, System.Int32]("one", 2)
        self.assertTrue(inst.value1 == "one")
        self.assertTrue(inst.value2 == 2)

    def test_generic_value_type(self):
        """Test usage of generic value type definitions."""
        inst = System.Nullable[System.Int32](10)
        self.assertTrue(inst.HasValue)
        self.assertTrue(inst.Value == 10)

    def test_generic_interface(self):
        # TODO NotImplemented
        pass

    def test_generic_delegate(self):
        # TODO NotImplemented
        pass

    def test_open_generic_type(self):
        """Test behavior of reflected open constructed generic types."""
        from Python.Test import DerivedFromOpenGeneric

        open_generic_type = DerivedFromOpenGeneric.__bases__[0]

        with self.assertRaises(TypeError):
            _ = open_generic_type()

        with self.assertRaises(TypeError):
            _ = open_generic_type[System.String]

    def test_derived_from_open_generic_type(self):
        """Test a generic type derived from an open generic type."""
        from Python.Test import DerivedFromOpenGeneric

        type_ = DerivedFromOpenGeneric[System.String, System.String]
        inst = type_(1, 'two', 'three')

        self.assertTrue(inst.value1 == 1)
        self.assertTrue(inst.value2 == 'two')
        self.assertTrue(inst.value3 == 'three')

    def test_generic_type_name_resolution(self):
        """Test the ability to disambiguate generic type names."""
        from Python.Test import GenericNameTest1, GenericNameTest2

        # If both a non-generic and generic type exist for a name, the
        # unadorned name always resolves to the non-generic type.
        _class = GenericNameTest1
        self.assertTrue(_class().value == 0)
        self.assertTrue(_class.value == 0)

        # If no non-generic type exists for a name, the unadorned name
        # cannot be instantiated. It can only be used to bind a generic.

        with self.assertRaises(TypeError):
            _ = GenericNameTest2()

        _class = GenericNameTest2[int]
        self.assertTrue(_class().value == 1)
        self.assertTrue(_class.value == 1)

        _class = GenericNameTest2[int, int]
        self.assertTrue(_class().value == 2)
        self.assertTrue(_class.value == 2)

    def test_generic_type_binding(self):
        """Test argument conversion / binding for generic methods."""
        from Python.Test import InterfaceTest, ISayHello1, ShortEnum
        import System

        self._assert_generic_wrapper_by_type(System.Boolean, True)
        self._assert_generic_wrapper_by_type(bool, True)
        self._assert_generic_wrapper_by_type(System.Byte, 255)
        self._assert_generic_wrapper_by_type(System.SByte, 127)
        self._assert_generic_wrapper_by_type(System.Char, u'A')
        self._assert_generic_wrapper_by_type(System.Int16, 32767)
        self._assert_generic_wrapper_by_type(System.Int32, 2147483647)
        self._assert_generic_wrapper_by_type(int, 2147483647)
        self._assert_generic_wrapper_by_type(System.Int64, long(9223372036854775807))
        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            self._assert_generic_wrapper_by_type(long, long(9223372036854775807))
        self._assert_generic_wrapper_by_type(System.UInt16, 65000)
        self._assert_generic_wrapper_by_type(System.UInt32, long(4294967295))
        self._assert_generic_wrapper_by_type(System.UInt64, long(18446744073709551615))
        self._assert_generic_wrapper_by_type(System.Single, 3.402823e38)
        self._assert_generic_wrapper_by_type(System.Double, 1.7976931348623157e308)
        self._assert_generic_wrapper_by_type(float, 1.7976931348623157e308)
        self._assert_generic_wrapper_by_type(System.Decimal, System.Decimal.One)
        self._assert_generic_wrapper_by_type(System.String, "test")
        self._assert_generic_wrapper_by_type(unicode, "test")
        self._assert_generic_wrapper_by_type(str, "test")
        self._assert_generic_wrapper_by_type(ShortEnum, ShortEnum.Zero)
        self._assert_generic_wrapper_by_type(System.Object, InterfaceTest())
        self._assert_generic_wrapper_by_type(InterfaceTest, InterfaceTest())
        self._assert_generic_wrapper_by_type(ISayHello1, InterfaceTest())

    def test_generic_method_binding(self):
        from Python.Test import GenericMethodTest, GenericStaticMethodTest
        from System import InvalidOperationException

        # Can invoke a static member on a closed generic type.
        value = GenericStaticMethodTest[str].Overloaded()
        self.assertTrue(value == 1)

        with self.assertRaises(InvalidOperationException):
            # Cannot invoke a static member on an open type.
            GenericStaticMethodTest.Overloaded()

        # Can invoke an instance member on a closed generic type.
        value = GenericMethodTest[str]().Overloaded()
        self.assertTrue(value == 1)

        with self.assertRaises(TypeError):
            # Cannot invoke an instance member on an open type,
            # because the open type cannot be instantiated.
            GenericMethodTest().Overloaded()

    def test_generic_method_type_handling(self):
        """Test argument conversion / binding for generic methods."""
        from Python.Test import InterfaceTest, ISayHello1, ShortEnum
        import System

        # FIXME: The value doesn't fit into Int64 and PythonNet doesn't
        # recognize it as UInt64 for unknown reasons.
        # self._assert_generic_method_by_type(System.UInt64, 18446744073709551615L)
        self._assert_generic_method_by_type(System.Boolean, True)
        self._assert_generic_method_by_type(bool, True)
        self._assert_generic_method_by_type(System.Byte, 255)
        self._assert_generic_method_by_type(System.SByte, 127)
        self._assert_generic_method_by_type(System.Char, u'A')
        self._assert_generic_method_by_type(System.Int16, 32767)
        self._assert_generic_method_by_type(System.Int32, 2147483647)
        self._assert_generic_method_by_type(int, 2147483647)
        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            self._assert_generic_method_by_type(System.Int64, long(9223372036854775807))
            self._assert_generic_method_by_type(long, long(9223372036854775807))
            self._assert_generic_method_by_type(System.UInt32, long(4294967295))
            self._assert_generic_method_by_type(System.Int64, long(1844674407370955161))
        self._assert_generic_method_by_type(System.UInt16, 65000)
        self._assert_generic_method_by_type(System.Single, 3.402823e38)
        self._assert_generic_method_by_type(System.Double, 1.7976931348623157e308)
        self._assert_generic_method_by_type(float, 1.7976931348623157e308)
        self._assert_generic_method_by_type(System.Decimal, System.Decimal.One)
        self._assert_generic_method_by_type(System.String, "test")
        self._assert_generic_method_by_type(unicode, "test")
        self._assert_generic_method_by_type(str, "test")
        self._assert_generic_method_by_type(ShortEnum, ShortEnum.Zero)
        self._assert_generic_method_by_type(System.Object, InterfaceTest())
        self._assert_generic_method_by_type(InterfaceTest, InterfaceTest(), 1)
        self._assert_generic_method_by_type(ISayHello1, InterfaceTest(), 1)

    def test_correct_overload_selection(self):
        """Test correct overloading selection for common types."""
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
                    atype(value1), atype(value2)))
            if PY2 and atype is Int64:
                value2 = long(value2)
            self.assertTrue(
                Math.Max(atype(value1),
                         value2) == Math.Max.__overloads__[atype, atype](
                    atype(value1), atype(value2)))

        clr.AddReference("System.Runtime.InteropServices")
        from System.Runtime.InteropServices import GCHandle, GCHandleType
        from System import Array, Byte
        cs_array = Array.CreateInstance(Byte, 1000)
        handler = GCHandle.Alloc(cs_array, GCHandleType.Pinned)

    def test_generic_method_overload_selection(self):
        """Test explicit overload selection with generic methods."""
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
        self.assertTrue(value is True)

        # public Q Overloaded<Q>(Q arg)
        value = inst.Overloaded[bool](True)
        self.assertTrue(value is True)

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[bool, str](True, "true")
        self.assertTrue(value == "true")

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[bool, str](True, "true")
        self.assertTrue(value == "true")

        # public static U Overloaded<Q, U>(Q arg1, U arg2)
        value = type.Overloaded[str, bool]("true", True)
        self.assertTrue(value is True)

        # public U Overloaded<Q, U>(Q arg1, U arg2)
        value = inst.Overloaded[str, bool]("true", True)
        self.assertTrue(value is True)

        # public static string Overloaded<T>(int arg1, int arg2, string arg3)
        value = type.Overloaded[str](123, 456, "success")
        self.assertTrue(value == "success")

        # public string Overloaded<T>(int arg1, int arg2, string arg3)
        value = inst.Overloaded[str](123, 456, "success")
        self.assertTrue(value == "success")

        with self.assertRaises(TypeError):
            _ = type.Overloaded[str, bool, int]("true", True, 1)

        with self.assertRaises(TypeError):
            _ = inst.Overloaded[str, bool, int]("true", True, 1)

    def test_method_overload_selection_with_generic_types(self):
        """Check method overload selection using generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper

        inst = InterfaceTest()

        vtype = GenericWrapper[System.Boolean]
        input_ = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value is True)

        vtype = GenericWrapper[bool]
        input_ = vtype(True)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value is True)

        vtype = GenericWrapper[System.Byte]
        input_ = vtype(255)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 255)

        vtype = GenericWrapper[System.SByte]
        input_ = vtype(127)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 127)

        vtype = GenericWrapper[System.Char]
        input_ = vtype(u'A')
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == u'A')

        vtype = GenericWrapper[System.Char]
        input_ = vtype(65535)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == unichr(65535))

        vtype = GenericWrapper[System.Int16]
        input_ = vtype(32767)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 32767)

        vtype = GenericWrapper[System.Int32]
        input_ = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 2147483647)

        vtype = GenericWrapper[int]
        input_ = vtype(2147483647)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 2147483647)

        vtype = GenericWrapper[System.Int64]
        input_ = vtype(long(9223372036854775807))
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            vtype = GenericWrapper[long]
            input_ = vtype(long(9223372036854775807))
            value = MethodTest.Overloaded.__overloads__[vtype](input_)
            self.assertTrue(value.value == long(9223372036854775807))

        vtype = GenericWrapper[System.UInt16]
        input_ = vtype(65000)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 65000)

        vtype = GenericWrapper[System.UInt32]
        input_ = vtype(long(4294967295))
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == long(4294967295))

        vtype = GenericWrapper[System.UInt64]
        input_ = vtype(long(18446744073709551615))
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == long(18446744073709551615))

        vtype = GenericWrapper[System.Single]
        input_ = vtype(3.402823e38)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 3.402823e38)

        vtype = GenericWrapper[System.Double]
        input_ = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[float]
        input_ = vtype(1.7976931348623157e308)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == 1.7976931348623157e308)

        vtype = GenericWrapper[System.Decimal]
        input_ = vtype(System.Decimal.One)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == System.Decimal.One)

        vtype = GenericWrapper[System.String]
        input_ = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == "spam")

        vtype = GenericWrapper[str]
        input_ = vtype("spam")
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == "spam")

        vtype = GenericWrapper[ShortEnum]
        input_ = vtype(ShortEnum.Zero)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value == ShortEnum.Zero)

        vtype = GenericWrapper[System.Object]
        input_ = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[InterfaceTest]
        input_ = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = GenericWrapper[ISayHello1]
        input_ = vtype(inst)
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value.value.__class__ == inst.__class__)

        vtype = System.Array[GenericWrapper[int]]
        input_ = vtype([GenericWrapper[int](0), GenericWrapper[int](1)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 0)
        self.assertTrue(value[1].value == 1)

    def test_overload_selection_with_arrays_of_generic_types(self):
        """Check overload selection using arrays of generic types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from Python.Test import MethodTest, GenericWrapper

        inst = InterfaceTest()

        gtype = GenericWrapper[System.Boolean]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(True), gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value is True)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[bool]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(True), gtype(True)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value is True)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Byte]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(255), gtype(255)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 255)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.SByte]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(127), gtype(127)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 127)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(u'A'), gtype(u'A')])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == u'A')
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Char]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(65535), gtype(65535)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == unichr(65535))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int16]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(32767), gtype(32767)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 32767)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int32]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 2147483647)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[int]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(2147483647), gtype(2147483647)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 2147483647)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Int64]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(long(9223372036854775807)),
                       gtype(long(9223372036854775807))])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == long(9223372036854775807))
        self.assertTrue(value.Length == 2)

        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            gtype = GenericWrapper[long]
            vtype = System.Array[gtype]
            input_ = vtype([gtype(long(9223372036854775807)),
                           gtype(long(9223372036854775807))])
            value = MethodTest.Overloaded.__overloads__[vtype](input_)
            self.assertTrue(value[0].value == long(9223372036854775807))
            self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt16]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(65000), gtype(65000)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 65000)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt32]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(long(4294967295)), gtype(long(4294967295))])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == long(4294967295))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.UInt64]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(long(18446744073709551615)),
                       gtype(long(18446744073709551615))])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == long(18446744073709551615))
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Single]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(3.402823e38), gtype(3.402823e38)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 3.402823e38)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Double]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[float]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(1.7976931348623157e308),
                       gtype(1.7976931348623157e308)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == 1.7976931348623157e308)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Decimal]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(System.Decimal.One),
                       gtype(System.Decimal.One)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == System.Decimal.One)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.String]
        vtype = System.Array[gtype]
        input_ = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == "spam")
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[str]
        vtype = System.Array[gtype]
        input_ = vtype([gtype("spam"), gtype("spam")])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == "spam")
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[ShortEnum]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(ShortEnum.Zero), gtype(ShortEnum.Zero)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value == ShortEnum.Zero)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[System.Object]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[InterfaceTest]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

        gtype = GenericWrapper[ISayHello1]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(inst), gtype(inst)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].value.__class__ == inst.__class__)
        self.assertTrue(value.Length == 2)

    def test_generic_overload_selection_magic_name_only(self):
        """Test using only __overloads__ to select on type & sig"""
        # TODO NotImplemented
        pass

    def test_nested_generic_class(self):
        """Check nested generic classes."""
        # TODO NotImplemented
        pass


def test_suite():
    return unittest.makeSuite(GenericTests)
