# -*- coding: utf-8 -*-

"""Test CLR generics support."""

import clr

import System
import pytest

from ._compat import PY2, long, unicode, unichr, zip


def assert_generic_wrapper_by_type(ptype, value):
    """Test Helper"""
    from Python.Test import GenericWrapper
    import System

    inst = GenericWrapper[ptype](value)
    assert inst.value == value

    atype = System.Array[ptype]
    items = atype([value, value, value])
    inst = GenericWrapper[atype](items)
    assert len(inst.value) == 3
    assert inst.value[0] == value
    assert inst.value[1] == value


def assert_generic_method_by_type(ptype, value, test_type=0):
    """Test Helper"""
    from Python.Test import GenericMethodTest, GenericStaticMethodTest
    import System

    itype = GenericMethodTest[System.Type]
    stype = GenericStaticMethodTest[System.Type]

    # Explicit selection (static method)
    result = stype.Overloaded[ptype](value)
    if test_type:
        assert result.__class__ == value.__class__
    else:
        assert result == value

    # Type inference (static method)
    result = stype.Overloaded(value)
    assert result == value
    if test_type:
        assert result.__class__ == value.__class__
    else:
        assert result == value

    # Explicit selection (instance method)
    result = itype().Overloaded[ptype](value)
    assert result == value
    if test_type:
        assert result.__class__ == value.__class__
    else:
        assert result == value

    # Type inference (instance method)
    result = itype().Overloaded(value)
    assert result == value
    if test_type:
        assert result.__class__ == value.__class__
    else:
        assert result == value

    atype = System.Array[ptype]
    items = atype([value, value, value])

    # Explicit selection (static method)
    result = stype.Overloaded[atype](items)
    if test_type:
        assert len(result) == 3
        assert result[0].__class__ == value.__class__
        assert result[1].__class__ == value.__class__
    else:
        assert len(result) == 3
        assert result[0] == value
        assert result[1] == value

    # Type inference (static method)
    result = stype.Overloaded(items)
    if test_type:
        assert len(result) == 3
        assert result[0].__class__ == value.__class__
        assert result[1].__class__ == value.__class__
    else:
        assert len(result) == 3
        assert result[0] == value
        assert result[1] == value

    # Explicit selection (instance method)
    result = itype().Overloaded[atype](items)
    if test_type:
        assert len(result) == 3
        assert result[0].__class__ == value.__class__
        assert result[1].__class__ == value.__class__
    else:
        assert len(result) == 3
        assert result[0] == value
        assert result[1] == value

    # Type inference (instance method)
    result = itype().Overloaded(items)
    if test_type:
        assert len(result) == 3
        assert result[0].__class__ == value.__class__
        assert result[1].__class__ == value.__class__
    else:
        assert len(result) == 3
        assert result[0] == value
        assert result[1] == value


def test_python_type_aliasing():
    """Test python type alias support with generics."""
    from System.Collections.Generic import Dictionary

    dict_ = Dictionary[str, str]()
    assert dict_.Count == 0
    dict_.Add("one", "one")
    assert dict_["one"] == "one"

    dict_ = Dictionary[System.String, System.String]()
    assert dict_.Count == 0
    dict_.Add("one", "one")
    assert dict_["one"] == "one"

    dict_ = Dictionary[int, int]()
    assert dict_.Count == 0
    dict_.Add(1, 1)
    assert dict_[1] == 1

    dict_ = Dictionary[System.Int32, System.Int32]()
    assert dict_.Count == 0
    dict_.Add(1, 1)
    assert dict_[1] == 1

    dict_ = Dictionary[long, long]()
    assert dict_.Count == 0
    dict_.Add(long(1), long(1))
    assert dict_[long(1)] == long(1)

    dict_ = Dictionary[System.Int64, System.Int64]()
    assert dict_.Count == 0
    dict_.Add(long(1), long(1))
    assert dict_[long(1)] == long(1)

    dict_ = Dictionary[float, float]()
    assert dict_.Count == 0
    dict_.Add(1.5, 1.5)
    assert dict_[1.5] == 1.5

    dict_ = Dictionary[System.Double, System.Double]()
    assert dict_.Count == 0
    dict_.Add(1.5, 1.5)
    assert dict_[1.5] == 1.5

    dict_ = Dictionary[bool, bool]()
    assert dict_.Count == 0
    dict_.Add(True, False)
    assert dict_[True] is False

    dict_ = Dictionary[System.Boolean, System.Boolean]()
    assert dict_.Count == 0
    dict_.Add(True, False)
    assert dict_[True] is False


def test_generic_reference_type():
    """Test usage of generic reference type definitions."""
    from Python.Test import GenericTypeDefinition

    inst = GenericTypeDefinition[System.String, System.Int32]("one", 2)
    assert inst.value1 == "one"
    assert inst.value2 == 2


def test_generic_value_type():
    """Test usage of generic value type definitions."""
    inst = System.Nullable[System.Int32](10)
    assert inst.HasValue
    assert inst.Value == 10


def test_generic_interface():
    # TODO NotImplemented
    pass


def test_generic_delegate():
    # TODO NotImplemented
    pass


def test_open_generic_type():
    """Test behavior of reflected open constructed generic types."""
    from Python.Test import DerivedFromOpenGeneric

    open_generic_type = DerivedFromOpenGeneric.__bases__[0]

    with pytest.raises(TypeError):
        _ = open_generic_type()

    with pytest.raises(TypeError):
        _ = open_generic_type[System.String]


def test_derived_from_open_generic_type():
    """Test a generic type derived from an open generic type."""
    from Python.Test import DerivedFromOpenGeneric

    type_ = DerivedFromOpenGeneric[System.String, System.String]
    inst = type_(1, 'two', 'three')

    assert inst.value1 == 1
    assert inst.value2 == 'two'
    assert inst.value3 == 'three'


def test_generic_type_name_resolution():
    """Test the ability to disambiguate generic type names."""
    from Python.Test import GenericNameTest1, GenericNameTest2

    # If both a non-generic and generic type exist for a name, the
    # unadorned name always resolves to the non-generic type.
    _class = GenericNameTest1
    assert _class().value == 0
    assert _class.value == 0

    # If no non-generic type exists for a name, the unadorned name
    # cannot be instantiated. It can only be used to bind a generic.

    with pytest.raises(TypeError):
        _ = GenericNameTest2()

    _class = GenericNameTest2[int]
    assert _class().value == 1
    assert _class.value == 1

    _class = GenericNameTest2[int, int]
    assert _class().value == 2
    assert _class.value == 2


def test_generic_type_binding():
    """Test argument conversion / binding for generic methods."""
    from Python.Test import InterfaceTest, ISayHello1, ShortEnum
    import System

    assert_generic_wrapper_by_type(System.Boolean, True)
    assert_generic_wrapper_by_type(bool, True)
    assert_generic_wrapper_by_type(System.Byte, 255)
    assert_generic_wrapper_by_type(System.SByte, 127)
    assert_generic_wrapper_by_type(System.Char, u'A')
    assert_generic_wrapper_by_type(System.Int16, 32767)
    assert_generic_wrapper_by_type(System.Int32, 2147483647)
    assert_generic_wrapper_by_type(int, 2147483647)
    assert_generic_wrapper_by_type(System.Int64, long(9223372036854775807))
    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        assert_generic_wrapper_by_type(long, long(9223372036854775807))
    assert_generic_wrapper_by_type(System.UInt16, 65000)
    assert_generic_wrapper_by_type(System.UInt32, long(4294967295))
    assert_generic_wrapper_by_type(System.UInt64, long(18446744073709551615))
    assert_generic_wrapper_by_type(System.Single, 3.402823e38)
    assert_generic_wrapper_by_type(System.Double, 1.7976931348623157e308)
    assert_generic_wrapper_by_type(float, 1.7976931348623157e308)
    assert_generic_wrapper_by_type(System.Decimal, System.Decimal.One)
    assert_generic_wrapper_by_type(System.String, "test")
    assert_generic_wrapper_by_type(unicode, "test")
    assert_generic_wrapper_by_type(str, "test")
    assert_generic_wrapper_by_type(ShortEnum, ShortEnum.Zero)
    assert_generic_wrapper_by_type(System.Object, InterfaceTest())
    assert_generic_wrapper_by_type(InterfaceTest, InterfaceTest())
    assert_generic_wrapper_by_type(ISayHello1, InterfaceTest())


def test_generic_method_binding():
    from Python.Test import GenericMethodTest, GenericStaticMethodTest
    from System import InvalidOperationException

    # Can invoke a static member on a closed generic type.
    value = GenericStaticMethodTest[str].Overloaded()
    assert value == 1

    with pytest.raises(InvalidOperationException):
        # Cannot invoke a static member on an open type.
        GenericStaticMethodTest.Overloaded()

    # Can invoke an instance member on a closed generic type.
    value = GenericMethodTest[str]().Overloaded()
    assert value == 1

    with pytest.raises(TypeError):
        # Cannot invoke an instance member on an open type,
        # because the open type cannot be instantiated.
        GenericMethodTest().Overloaded()


def test_generic_method_type_handling():
    """Test argument conversion / binding for generic methods."""
    from Python.Test import InterfaceTest, ISayHello1, ShortEnum
    import System

    # FIXME: The value doesn't fit into Int64 and PythonNet doesn't
    # recognize it as UInt64 for unknown reasons.
    # assert_generic_method_by_type(System.UInt64, 18446744073709551615L)
    assert_generic_method_by_type(System.Boolean, True)
    assert_generic_method_by_type(bool, True)
    assert_generic_method_by_type(System.Byte, 255)
    assert_generic_method_by_type(System.SByte, 127)
    assert_generic_method_by_type(System.Char, u'A')
    assert_generic_method_by_type(System.Int16, 32767)
    assert_generic_method_by_type(System.Int32, 2147483647)
    assert_generic_method_by_type(int, 2147483647)
    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        assert_generic_method_by_type(System.Int64, long(9223372036854775807))
        assert_generic_method_by_type(long, long(9223372036854775807))
        assert_generic_method_by_type(System.UInt32, long(4294967295))
        assert_generic_method_by_type(System.Int64, long(1844674407370955161))
    assert_generic_method_by_type(System.UInt16, 65000)
    assert_generic_method_by_type(System.Single, 3.402823e38)
    assert_generic_method_by_type(System.Double, 1.7976931348623157e308)
    assert_generic_method_by_type(float, 1.7976931348623157e308)
    assert_generic_method_by_type(System.Decimal, System.Decimal.One)
    assert_generic_method_by_type(System.String, "test")
    assert_generic_method_by_type(unicode, "test")
    assert_generic_method_by_type(str, "test")
    assert_generic_method_by_type(ShortEnum, ShortEnum.Zero)
    assert_generic_method_by_type(System.Object, InterfaceTest())
    assert_generic_method_by_type(InterfaceTest, InterfaceTest(), 1)
    assert_generic_method_by_type(ISayHello1, InterfaceTest(), 1)


def test_correct_overload_selection():
    """Test correct overloading selection for common types."""
    from System import (String, Double, Single,
                        Int16, Int32, Int64)
    from System import Math

    substr = String("substring")
    assert substr.Substring(2) == substr.Substring.__overloads__[Int32](
        Int32(2))
    assert substr.Substring(2, 3) == substr.Substring.__overloads__[Int32, Int32](
        Int32(2), Int32(3))

    for atype, value1, value2 in zip([Double, Single, Int16, Int32, Int64],
                                     [1.0, 1.0, 1, 1, 1],
                                     [2.0, 0.5, 2, 0, -1]):
        assert Math.Abs(atype(value1)) == Math.Abs.__overloads__[atype](atype(value1))
        assert Math.Abs(value1) == Math.Abs.__overloads__[atype](atype(value1))
        assert Math.Max(atype(value1),
                        atype(value2)) == Math.Max.__overloads__[atype, atype](
            atype(value1), atype(value2))
        if PY2 and atype is Int64:
            value2 = long(value2)
        assert Math.Max(atype(value1),
                        value2) == Math.Max.__overloads__[atype, atype](
            atype(value1), atype(value2))

    clr.AddReference("System.Runtime.InteropServices")
    from System.Runtime.InteropServices import GCHandle, GCHandleType
    from System import Array, Byte
    cs_array = Array.CreateInstance(Byte, 1000)
    handler = GCHandle.Alloc(cs_array, GCHandleType.Pinned)


def test_generic_method_overload_selection():
    """Test explicit overload selection with generic methods."""
    from Python.Test import GenericMethodTest, GenericStaticMethodTest

    type = GenericStaticMethodTest[str]
    inst = GenericMethodTest[str]()

    # public static int Overloaded()
    value = type.Overloaded()
    assert value == 1

    # public int Overloaded()
    value = inst.Overloaded()
    assert value == 1

    # public static T Overloaded(T arg) (inferred)
    value = type.Overloaded("test")
    assert value == "test"

    # public T Overloaded(T arg) (inferred)
    value = inst.Overloaded("test")
    assert value == "test"

    # public static T Overloaded(T arg) (explicit)
    value = type.Overloaded[str]("test")
    assert value == "test"

    # public T Overloaded(T arg) (explicit)
    value = inst.Overloaded[str]("test")
    assert value == "test"

    # public static Q Overloaded<Q>(Q arg)
    value = type.Overloaded[float](2.2)
    assert value == 2.2

    # public Q Overloaded<Q>(Q arg)
    value = inst.Overloaded[float](2.2)
    assert value == 2.2

    # public static Q Overloaded<Q>(Q arg)
    value = type.Overloaded[bool](True)
    assert value is True

    # public Q Overloaded<Q>(Q arg)
    value = inst.Overloaded[bool](True)
    assert value is True

    # public static U Overloaded<Q, U>(Q arg1, U arg2)
    value = type.Overloaded[bool, str](True, "true")
    assert value == "true"

    # public U Overloaded<Q, U>(Q arg1, U arg2)
    value = inst.Overloaded[bool, str](True, "true")
    assert value == "true"

    # public static U Overloaded<Q, U>(Q arg1, U arg2)
    value = type.Overloaded[str, bool]("true", True)
    assert value is True

    # public U Overloaded<Q, U>(Q arg1, U arg2)
    value = inst.Overloaded[str, bool]("true", True)
    assert value is True

    # public static string Overloaded<T>(int arg1, int arg2, string arg3)
    value = type.Overloaded[str](123, 456, "success")
    assert value == "success"

    # public string Overloaded<T>(int arg1, int arg2, string arg3)
    value = inst.Overloaded[str](123, 456, "success")
    assert value == "success"

    with pytest.raises(TypeError):
        _ = type.Overloaded[str, bool, int]("true", True, 1)

    with pytest.raises(TypeError):
        _ = inst.Overloaded[str, bool, int]("true", True, 1)


def test_method_overload_selection_with_generic_types():
    """Check method overload selection using generic types."""
    from Python.Test import ISayHello1, InterfaceTest, ShortEnum
    from Python.Test import MethodTest, GenericWrapper

    inst = InterfaceTest()

    vtype = GenericWrapper[System.Boolean]
    input_ = vtype(True)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value is True

    vtype = GenericWrapper[bool]
    input_ = vtype(True)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value is True

    vtype = GenericWrapper[System.Byte]
    input_ = vtype(255)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 255

    vtype = GenericWrapper[System.SByte]
    input_ = vtype(127)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 127

    vtype = GenericWrapper[System.Char]
    input_ = vtype(u'A')
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == u'A'

    vtype = GenericWrapper[System.Char]
    input_ = vtype(65535)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == unichr(65535)

    vtype = GenericWrapper[System.Int16]
    input_ = vtype(32767)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 32767

    vtype = GenericWrapper[System.Int32]
    input_ = vtype(2147483647)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 2147483647

    vtype = GenericWrapper[int]
    input_ = vtype(2147483647)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 2147483647

    vtype = GenericWrapper[System.Int64]
    input_ = vtype(long(9223372036854775807))
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == long(9223372036854775807)

    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        vtype = GenericWrapper[long]
        input_ = vtype(long(9223372036854775807))
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        assert value.value == long(9223372036854775807)

    vtype = GenericWrapper[System.UInt16]
    input_ = vtype(65000)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 65000

    vtype = GenericWrapper[System.UInt32]
    input_ = vtype(long(4294967295))
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == long(4294967295)

    vtype = GenericWrapper[System.UInt64]
    input_ = vtype(long(18446744073709551615))
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == long(18446744073709551615)

    vtype = GenericWrapper[System.Single]
    input_ = vtype(3.402823e38)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 3.402823e38

    vtype = GenericWrapper[System.Double]
    input_ = vtype(1.7976931348623157e308)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 1.7976931348623157e308

    vtype = GenericWrapper[float]
    input_ = vtype(1.7976931348623157e308)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == 1.7976931348623157e308

    vtype = GenericWrapper[System.Decimal]
    input_ = vtype(System.Decimal.One)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == System.Decimal.One

    vtype = GenericWrapper[System.String]
    input_ = vtype("spam")
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == "spam"

    vtype = GenericWrapper[str]
    input_ = vtype("spam")
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == "spam"

    vtype = GenericWrapper[ShortEnum]
    input_ = vtype(ShortEnum.Zero)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value == ShortEnum.Zero

    vtype = GenericWrapper[System.Object]
    input_ = vtype(inst)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value.__class__ == inst.__class__

    vtype = GenericWrapper[InterfaceTest]
    input_ = vtype(inst)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value.__class__ == inst.__class__

    vtype = GenericWrapper[ISayHello1]
    input_ = vtype(inst)
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value.value.__class__ == inst.__class__

    vtype = System.Array[GenericWrapper[int]]
    input_ = vtype([GenericWrapper[int](0), GenericWrapper[int](1)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 0
    assert value[1].value == 1


def test_overload_selection_with_arrays_of_generic_types():
    """Check overload selection using arrays of generic types."""
    from Python.Test import ISayHello1, InterfaceTest, ShortEnum
    from Python.Test import MethodTest, GenericWrapper

    inst = InterfaceTest()

    gtype = GenericWrapper[System.Boolean]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(True), gtype(True)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value is True
    assert value.Length == 2

    gtype = GenericWrapper[bool]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(True), gtype(True)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value is True
    assert value.Length == 2

    gtype = GenericWrapper[System.Byte]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(255), gtype(255)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 255
    assert value.Length == 2

    gtype = GenericWrapper[System.SByte]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(127), gtype(127)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 127
    assert value.Length == 2

    gtype = GenericWrapper[System.Char]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(u'A'), gtype(u'A')])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == u'A'
    assert value.Length == 2

    gtype = GenericWrapper[System.Char]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(65535), gtype(65535)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == unichr(65535)
    assert value.Length == 2

    gtype = GenericWrapper[System.Int16]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(32767), gtype(32767)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 32767
    assert value.Length == 2

    gtype = GenericWrapper[System.Int32]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(2147483647), gtype(2147483647)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 2147483647
    assert value.Length == 2

    gtype = GenericWrapper[int]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(2147483647), gtype(2147483647)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 2147483647
    assert value.Length == 2

    gtype = GenericWrapper[System.Int64]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(long(9223372036854775807)),
                    gtype(long(9223372036854775807))])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == long(9223372036854775807)
    assert value.Length == 2

    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        gtype = GenericWrapper[long]
        vtype = System.Array[gtype]
        input_ = vtype([gtype(long(9223372036854775807)),
                        gtype(long(9223372036854775807))])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        assert value[0].value == long(9223372036854775807)
        assert value.Length == 2

    gtype = GenericWrapper[System.UInt16]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(65000), gtype(65000)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 65000
    assert value.Length == 2

    gtype = GenericWrapper[System.UInt32]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(long(4294967295)), gtype(long(4294967295))])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == long(4294967295)
    assert value.Length == 2

    gtype = GenericWrapper[System.UInt64]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(long(18446744073709551615)),
                    gtype(long(18446744073709551615))])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == long(18446744073709551615)
    assert value.Length == 2

    gtype = GenericWrapper[System.Single]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(3.402823e38), gtype(3.402823e38)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 3.402823e38
    assert value.Length == 2

    gtype = GenericWrapper[System.Double]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(1.7976931348623157e308),
                    gtype(1.7976931348623157e308)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 1.7976931348623157e308
    assert value.Length == 2

    gtype = GenericWrapper[float]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(1.7976931348623157e308),
                    gtype(1.7976931348623157e308)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == 1.7976931348623157e308
    assert value.Length == 2

    gtype = GenericWrapper[System.Decimal]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(System.Decimal.One),
                    gtype(System.Decimal.One)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == System.Decimal.One
    assert value.Length == 2

    gtype = GenericWrapper[System.String]
    vtype = System.Array[gtype]
    input_ = vtype([gtype("spam"), gtype("spam")])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == "spam"
    assert value.Length == 2

    gtype = GenericWrapper[str]
    vtype = System.Array[gtype]
    input_ = vtype([gtype("spam"), gtype("spam")])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == "spam"
    assert value.Length == 2

    gtype = GenericWrapper[ShortEnum]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(ShortEnum.Zero), gtype(ShortEnum.Zero)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value == ShortEnum.Zero
    assert value.Length == 2

    gtype = GenericWrapper[System.Object]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(inst), gtype(inst)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value.__class__ == inst.__class__
    assert value.Length == 2

    gtype = GenericWrapper[InterfaceTest]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(inst), gtype(inst)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value.__class__ == inst.__class__
    assert value.Length == 2

    gtype = GenericWrapper[ISayHello1]
    vtype = System.Array[gtype]
    input_ = vtype([gtype(inst), gtype(inst)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].value.__class__ == inst.__class__
    assert value.Length == 2


def test_generic_overload_selection_magic_name_only():
    """Test using only __overloads__ to select on type & sig"""
    # TODO NotImplemented
    pass


def test_nested_generic_class():
    """Check nested generic classes."""
    # TODO NotImplemented
    pass
