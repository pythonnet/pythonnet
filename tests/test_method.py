# -*- coding: utf-8 -*-

"""Test CLR method support."""

import System
import pytest
from Python.Test import MethodTest

def test_instance_method_overwritable():
    """Test instance method overwriting."""

    ob = MethodTest()
    ob.OverwritableMethod = lambda: "overwritten"
    assert ob.OverwritableMethod() == "overwritten"


def test_public_instance_method():
    """Test public instance method visibility."""
    ob = MethodTest()
    assert ob.PublicMethod() == "public"


def test_public_static_method():
    """Test public static method visibility."""
    ob = MethodTest()
    assert MethodTest.PublicStaticMethod() == "public static"
    assert ob.PublicStaticMethod() == "public static"


def test_protected_instance_method():
    """Test protected instance method visibility."""
    ob = MethodTest()
    assert ob.ProtectedMethod() == "protected"


def test_protected_static_method():
    """Test protected static method visibility."""
    ob = MethodTest()
    result = "protected static"
    assert MethodTest.ProtectedStaticMethod() == result
    assert ob.ProtectedStaticMethod() == result


def test_internal_method():
    """Test internal method visibility."""

    with pytest.raises(AttributeError):
        _ = MethodTest().InternalMethod

    with pytest.raises(AttributeError):
        _ = MethodTest.InternalMethod

    with pytest.raises(AttributeError):
        _ = MethodTest().InternalStaticMethod

    with pytest.raises(AttributeError):
        _ = MethodTest.InternalStaticMethod


def test_private_method():
    """Test private method visibility."""

    with pytest.raises(AttributeError):
        _ = MethodTest().PrivateMethod

    with pytest.raises(AttributeError):
        _ = MethodTest.PrivateMethod

    with pytest.raises(AttributeError):
        _ = MethodTest().PrivateStaticMethod

    with pytest.raises(AttributeError):
        _ = MethodTest.PrivateStaticMethod


def test_unbound_managed_method_call():
    """Test calling unbound managed methods."""
    from Python.Test import MethodTestSub

    ob = MethodTest()
    assert MethodTest.PublicMethod(ob) == "public"

    with pytest.raises(TypeError):
        MethodTest.PublicMethod()

    ob = MethodTestSub()
    assert MethodTestSub.PublicMethod(ob) == "public"
    assert MethodTestSub.PublicMethod(ob, "echo") == "echo"

    with pytest.raises(TypeError):
        MethodTestSub.PublicMethod("echo")


def test_overloaded_method_inheritance():
    """Test that overloads are inherited properly."""
    from Python.Test import MethodTestSub

    ob = MethodTest()
    assert ob.PublicMethod() == "public"

    with pytest.raises(TypeError):
        ob = MethodTest()
        ob.PublicMethod("echo")

    ob = MethodTestSub()
    assert ob.PublicMethod() == "public"

    assert ob.PublicMethod("echo") == "echo"


def test_method_descriptor_abuse():
    """Test method descriptor abuse."""
    desc = MethodTest.__dict__['PublicMethod']

    with pytest.raises(TypeError):
        desc.__get__(0, 0)

    with pytest.raises(AttributeError):
        desc.__set__(0, 0)


def test_method_docstrings():
    """Test standard method docstring generation"""
    method = MethodTest.GetType
    value = 'System.Type GetType()'
    assert method.__doc__ == value


# ======================================================================
# Tests of specific argument and result conversion scenarios
# ======================================================================
def test_method_call_enum_conversion():
    """Test enum conversion in method call."""
    from System import TypeCode

    ob = MethodTest()
    r = ob.TestEnumConversion(TypeCode.Int32)
    assert r == TypeCode.Int32


def test_method_call_flags_conversion():
    """Test flags conversion in method call."""
    from System.IO import FileAccess

    ob = MethodTest()
    flags = FileAccess.Read | FileAccess.Write
    r = ob.TestFlagsConversion(flags)
    assert r == flags


def test_method_call_struct_conversion():
    """Test struct conversion in method call."""
    from System import Guid

    ob = MethodTest()
    guid = Guid.NewGuid()
    temp = guid.ToString()
    r = ob.TestStructConversion(guid)
    assert r.ToString() == temp


def test_subclass_instance_conversion():
    """Test subclass instance conversion in method call."""

    class TestSubException(System.Exception):
        pass

    ob = MethodTest()
    instance = TestSubException()
    result = ob.TestSubclassConversion(instance)
    assert isinstance(result, System.Exception)


def test_null_array_conversion():
    """Test null array conversion in method call."""
    ob = MethodTest()
    r = ob.TestNullArrayConversion(None)
    assert r is None


def test_string_params_args():
    """Test use of string params."""
    result = MethodTest.TestStringParamsArg('one', 'two', 'three')
    assert result.Length == 4
    assert len(result) == 4, result
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'
    # ensures params string[] overload takes precedence over params object[]
    assert result[3] == 'tail'

    result = MethodTest.TestStringParamsArg(['one', 'two', 'three'])
    assert len(result) == 4
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'
    assert result[3] == 'tail'


def test_object_params_args():
    """Test use of object params."""
    result = MethodTest.TestObjectParamsArg('one', 'two', 'three')
    assert len(result) == 3, result
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'

    result = MethodTest.TestObjectParamsArg(['one', 'two', 'three'])
    assert len(result) == 3, result
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'


def test_value_params_args():
    """Test use of value type params."""
    result = MethodTest.TestValueParamsArg(1, 2, 3)
    assert len(result) == 3
    assert result[0] == 1
    assert result[1] == 2
    assert result[2] == 3

    result = MethodTest.TestValueParamsArg([1, 2, 3])
    assert len(result) == 3
    assert result[0] == 1
    assert result[1] == 2
    assert result[2] == 3


def test_non_params_array_in_last_place():
    """Test overload resolution with of non-"params" array as
    last parameter."""
    result = MethodTest.TestNonParamsArrayInLastPlace(1, 2, 3)
    assert result

def test_params_methods_with_no_params():
    """Tests that passing no arguments to a params method
    passes an empty array"""
    result = MethodTest.TestValueParamsArg()
    assert len(result) == 0

    result = MethodTest.TestOneArgWithParams('Some String')
    assert len(result) == 0

    result = MethodTest.TestTwoArgWithParams('Some String', 'Some Other String')
    assert len(result) == 0

def test_params_methods_with_non_lists():
    """Tests that passing single parameters to a params
    method will convert into an array on the .NET side"""
    result = MethodTest.TestOneArgWithParams('Some String', [1, 2, 3, 4])
    assert len(result) == 4

    result = MethodTest.TestOneArgWithParams('Some String', 1, 2, 3, 4)
    assert len(result) == 4

    result = MethodTest.TestOneArgWithParams('Some String', [5])
    assert len(result) == 1

    result = MethodTest.TestOneArgWithParams('Some String', 5)
    assert len(result) == 1

def test_params_method_with_lists():
    """Tests passing multiple lists to a params object[] method"""
    result = MethodTest.TestObjectParamsArg([],[])
    assert len(result) == 2

def test_string_out_params():
    """Test use of string out-parameters."""
    result = MethodTest.TestStringOutParams("hi", "there")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"

    result = MethodTest.TestStringOutParams("hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"


def test_string_out_params_without_passing_string_value():
    """Test use of string out-parameters."""
    # @eirannejad 2022-01-13
    result = MethodTest.TestStringOutParams("hi")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"


def test_string_ref_params():
    """Test use of string byref parameters."""
    result = MethodTest.TestStringRefParams("hi", "there")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"

    result = MethodTest.TestStringRefParams("hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"


def test_value_out_params():
    """Test use of value type out-parameters."""
    result = MethodTest.TestValueOutParams("hi", 1)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == 42

    # None cannot be converted to a value type like int, long, etc.
    with pytest.raises(TypeError):
        MethodTest.TestValueOutParams("hi", None)


def test_value_out_params_without_passing_string_value():
    """Test use of string out-parameters."""
    # @eirannejad 2022-01-13
    result = MethodTest.TestValueOutParams("hi")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == 42


def test_value_ref_params():
    """Test use of value type byref parameters."""
    result = MethodTest.TestValueRefParams("hi", 1)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == 42

    # None cannot be converted to a value type like int, long, etc.
    with pytest.raises(TypeError):
        MethodTest.TestValueRefParams("hi", None)


def test_object_out_params():
    """Test use of object out-parameters."""
    result = MethodTest.TestObjectOutParams("hi", MethodTest())
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Exception)

    result = MethodTest.TestObjectOutParams("hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Exception)


def test_object_out_params_without_passing_string_value():
    """Test use of object out-parameters."""
    result = MethodTest.TestObjectOutParams("hi")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Exception)


def test_object_ref_params():
    """Test use of object byref parameters."""
    result = MethodTest.TestObjectRefParams("hi", MethodTest())
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Exception)

    result = MethodTest.TestObjectRefParams("hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Exception)


def test_struct_out_params():
    """Test use of struct out-parameters."""
    result = MethodTest.TestStructOutParams("hi", System.Guid.NewGuid())
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Guid)

    # None cannot be converted to a value type like a struct
    with pytest.raises(TypeError):
        MethodTest.TestValueRefParams("hi", None)


def test_struct_out_params_without_passing_string_value():
    """Test use of struct out-parameters."""
    result = MethodTest.TestStructOutParams("hi")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Guid)


def test_struct_ref_params():
    """Test use of struct byref parameters."""
    result = MethodTest.TestStructRefParams("hi", System.Guid.NewGuid())
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert isinstance(result[1], System.Guid)

    # None cannot be converted to a value type like a struct
    with pytest.raises(TypeError):
        MethodTest.TestValueRefParams("hi", None)


def test_void_single_out_param():
    """Test void method with single out-parameter."""
    result = MethodTest.TestVoidSingleOutParam(9)
    assert result == 42

    # None cannot be converted to a value type
    with pytest.raises(TypeError):
        MethodTest.TestVoidSingleOutParam(None)


def test_void_single_ref_param():
    """Test void method with single ref-parameter."""
    result = MethodTest.TestVoidSingleRefParam(9)
    assert result == 42

    # None cannot be converted to a value type
    with pytest.raises(TypeError):
        MethodTest.TestVoidSingleRefParam(None)


def test_single_default_param():
    """Test void method with single ref-parameter."""
    result = MethodTest.TestSingleDefaultParam()
    assert result == 5


def test_one_arg_and_two_default_param():
    """Test void method with single ref-parameter."""
    result = MethodTest.TestOneArgAndTwoDefaultParam(11)
    assert result == 22

    result = MethodTest.TestOneArgAndTwoDefaultParam(15)
    assert result == 26

    result = MethodTest.TestOneArgAndTwoDefaultParam(20)
    assert result == 31


def test_two_default_param():
    """Test void method with single ref-parameter."""
    result = MethodTest.TestTwoDefaultParam()
    assert result == 11


def test_explicit_selection_with_out_modifier():
    """Check explicit overload selection with out modifiers."""
    refstr = System.String("").GetType().MakeByRefType()
    result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
        "hi", "there")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"

    result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
        "hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"


def test_explicit_selection_with_ref_modifier():
    """Check explicit overload selection with ref modifiers."""
    refstr = System.String("").GetType().MakeByRefType()
    result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
        "hi", "there")
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"

    result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
        "hi", None)
    assert isinstance(result, tuple)
    assert len(result) == 2
    assert result[0] is True
    assert result[1] == "output string"


def test_explicit_overload_selection():
    """Check explicit overload selection using [] syntax."""
    from Python.Test import ISayHello1, InterfaceTest, ShortEnum
    from System import Array

    inst = InterfaceTest()

    value = MethodTest.Overloaded.__overloads__[System.Boolean](True)
    assert value is True

    value = MethodTest.Overloaded.__overloads__[bool](True)
    assert value is True

    value = MethodTest.Overloaded.__overloads__[System.Byte](255)
    assert value == 255

    value = MethodTest.Overloaded.__overloads__[System.SByte](127)
    assert value == 127

    value = MethodTest.Overloaded.__overloads__[System.Char](u'A')
    assert value == u'A'

    value = MethodTest.Overloaded.__overloads__[System.Char](65535)
    assert value == chr(65535)

    value = MethodTest.Overloaded.__overloads__[System.Int16](32767)
    assert value == 32767

    value = MethodTest.Overloaded.__overloads__[System.Int32](2147483647)
    assert value == 2147483647

    value = MethodTest.Overloaded.__overloads__[int](2147483647)
    assert value == 2147483647

    value = MethodTest.Overloaded.__overloads__[System.Int64](
        9223372036854775807
    )
    assert value == 9223372036854775807

    value = MethodTest.Overloaded.__overloads__[System.UInt16](65000)
    assert value == 65000

    value = MethodTest.Overloaded.__overloads__[System.UInt32](
        4294967295
    )
    assert value == 4294967295

    value = MethodTest.Overloaded.__overloads__[System.UInt64](
        18446744073709551615
    )
    assert value == 18446744073709551615

    value = MethodTest.Overloaded.__overloads__[System.Single](3.402823e38)
    assert value == System.Single(3.402823e38)

    value = MethodTest.Overloaded.__overloads__[System.Double](
        1.7976931348623157e308)
    assert value == 1.7976931348623157e308

    value = MethodTest.Overloaded.__overloads__[float](
        1.7976931348623157e308)
    assert value == 1.7976931348623157e308

    value = MethodTest.Overloaded.__overloads__[System.Decimal](
        System.Decimal.One)
    assert value == System.Decimal.One

    value = MethodTest.Overloaded.__overloads__[System.String]("spam")
    assert value == "spam"

    value = MethodTest.Overloaded.__overloads__[str]("spam")
    assert value == "spam"

    value = MethodTest.Overloaded.__overloads__[ShortEnum](ShortEnum.Zero)
    assert value == ShortEnum.Zero

    value = MethodTest.Overloaded.__overloads__[System.Object](inst)
    assert value.__class__ == inst.__class__

    value = MethodTest.Overloaded.__overloads__[InterfaceTest](inst)
    assert value.__class__ == inst.__class__

    iface_class = ISayHello1(InterfaceTest()).__class__
    value = MethodTest.Overloaded.__overloads__[ISayHello1](inst)
    assert value.__class__ != inst.__class__
    assert value.__class__ == iface_class

    atype = Array[System.Object]
    value = MethodTest.Overloaded.__overloads__[str, int, atype](
        "one", 1, atype([1, 2, 3]))
    assert value == 3

    value = MethodTest.Overloaded.__overloads__[str, int]("one", 1)
    assert value == 1

    value = MethodTest.Overloaded.__overloads__[int, str](1, "one")
    assert value == 1


def test_overload_selection_with_array_types():
    """Check overload selection using array types."""
    from Python.Test import ISayHello1, InterfaceTest, ShortEnum
    from System import Array

    inst = InterfaceTest()

    vtype = Array[System.Boolean]
    input_ = vtype([True, True])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] is True
    assert value[1] is True

    vtype = Array[bool]
    input_ = vtype([True, True])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] is True
    assert value[1] is True

    vtype = Array[System.Byte]
    input_ = vtype([0, 255])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 255

    vtype = Array[System.SByte]
    input_ = vtype([0, 127])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 127

    vtype = Array[System.Char]
    input_ = vtype([u'A', u'Z'])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == u'A'
    assert value[1] == u'Z'

    vtype = Array[System.Char]
    input_ = vtype([0, 65535])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == chr(0)
    assert value[1] == chr(65535)

    vtype = Array[System.Int16]
    input_ = vtype([0, 32767])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 32767

    vtype = Array[System.Int32]
    input_ = vtype([0, 2147483647])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 2147483647

    vtype = Array[int]
    input_ = vtype([0, 2147483647])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 2147483647

    vtype = Array[System.Int64]
    input_ = vtype([0, 9223372036854775807])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 9223372036854775807

    vtype = Array[System.UInt16]
    input_ = vtype([0, 65000])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 65000

    vtype = Array[System.UInt32]
    input_ = vtype([0, 4294967295])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 4294967295

    vtype = Array[System.UInt64]
    input_ = vtype([0, 18446744073709551615])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 18446744073709551615

    vtype = Array[System.Single]
    input_ = vtype([0.0, 3.402823e38])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0.0
    assert value[1] == System.Single(3.402823e38)

    vtype = Array[System.Double]
    input_ = vtype([0.0, 1.7976931348623157e308])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0.0
    assert value[1] == 1.7976931348623157e308

    vtype = Array[float]
    input_ = vtype([0.0, 1.7976931348623157e308])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0.0
    assert value[1] == 1.7976931348623157e308

    vtype = Array[System.Decimal]
    input_ = vtype([System.Decimal.Zero, System.Decimal.One])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == System.Decimal.Zero
    assert value[1] == System.Decimal.One

    vtype = Array[System.String]
    input_ = vtype(["one", "two"])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == "one"
    assert value[1] == "two"

    vtype = Array[str]
    input_ = vtype(["one", "two"])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == "one"
    assert value[1] == "two"

    vtype = Array[ShortEnum]
    input_ = vtype([ShortEnum.Zero, ShortEnum.One])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == ShortEnum.Zero
    assert value[1] == ShortEnum.One

    vtype = Array[System.Object]
    input_ = vtype([inst, inst])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].__class__ == inst.__class__
    assert value[1].__class__ == inst.__class__

    vtype = Array[InterfaceTest]
    input_ = vtype([inst, inst])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].__class__ == inst.__class__
    assert value[1].__class__ == inst.__class__

    iface_class = ISayHello1(inst).__class__
    vtype = Array[ISayHello1]
    input_ = vtype([inst, inst])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].__class__ == iface_class
    assert value[1].__class__ == iface_class


def test_explicit_overload_selection_failure():
    """Check that overload selection fails correctly."""

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[System.Type](True)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[int, int](1, 1)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[str, int, int]("", 1, 1)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[int, int](1)


def test_we_can_bind_to_encoding_get_string():
    """Check that we can bind to the Encoding.GetString method
    with variables."""
    from System.Text import Encoding
    from System.IO import MemoryStream

    my_bytes = Encoding.UTF8.GetBytes('Some testing string')
    stream = MemoryStream()
    stream.Write(my_bytes, 0, my_bytes.Length)
    stream.Position = 0

    buff = System.Array.CreateInstance(System.Byte, 3)
    buff.Initialize()
    data = []
    read = 1

    while read > 0:
        read = stream.Read(buff, 0, buff.Length)
        temp = Encoding.UTF8.GetString(buff, 0, read)
        data.append(temp)

    data = ''.join(data)
    assert data == 'Some testing string'


def test_wrong_overload():
    """Test regression in which implicit conversion caused the wrong types
    to be used. See #131 for issue. Fixed by #137, #151"""

    # Used to return `50L`
    res = System.Math.Abs(50.5)
    assert res == 50.5
    assert type(res) == float

    res = System.Math.Abs(-50.5)
    assert res == 50.5
    assert type(res) == float

    res = System.Math.Max(50.5, 50.1)
    assert res == 50.5
    assert type(res) == float

    res = System.Math.Max(System.Double(10.5), System.Double(50.5))
    assert res == 50.5
    assert type(res) == float  # Should it return a System.Double?

    res = System.Math.Max(System.Double(50.5), 50.1)
    assert res == 50.5
    assert type(res) == float


def test_no_object_in_param():
    """Test that fix for #203 doesn't break behavior w/ no object overload"""

    res = MethodTest.TestOverloadedNoObject(5)
    assert res == "Got int"

    res = MethodTest.TestOverloadedNoObject(i=7)
    assert res == "Got int"

    with pytest.raises(TypeError):
        MethodTest.TestOverloadedNoObject("test")

    with pytest.raises(TypeError):
        MethodTest.TestOverloadedNoObject(5.5)

    # Ensure that the top-level error is TypeError even if the inner error is an OverflowError
    with pytest.raises(TypeError):
        MethodTest.TestOverloadedNoObject(2147483648)

def test_object_in_param():
    """Test regression introduced by #151 in which Object method overloads
    aren't being used. See #203 for issue."""

    res = MethodTest.TestOverloadedObject(5)
    assert res == "Got int"

    res = MethodTest.TestOverloadedObject(i=7)
    assert res == "Got int"

    res = MethodTest.TestOverloadedObject("test")
    assert res == "Got object"

    res = MethodTest.TestOverloadedObject(o="test")
    assert res == "Got object"


def test_object_in_multiparam():
    """Test method with object multiparams behaves"""

    res = MethodTest.TestOverloadedObjectTwo(5, 5)
    assert res == "Got int-int"

    res = MethodTest.TestOverloadedObjectTwo(5, "foo")
    assert res == "Got int-string"

    res = MethodTest.TestOverloadedObjectTwo("foo", 7.24)
    assert res == "Got string-object"

    res = MethodTest.TestOverloadedObjectTwo("foo", "bar")
    assert res == "Got string-string"

    res = MethodTest.TestOverloadedObjectTwo("foo", 5)
    assert res == "Got string-int"

    res = MethodTest.TestOverloadedObjectTwo(7.24, 7.24)
    assert res == "Got object-object"

    res = MethodTest.TestOverloadedObjectTwo(a=5, b=5)
    assert res == "Got int-int"

    res = MethodTest.TestOverloadedObjectTwo(5, b=5)
    assert res == "Got int-int"

    res = MethodTest.TestOverloadedObjectTwo(a=5, b="foo")
    assert res == "Got int-string"

    res = MethodTest.TestOverloadedObjectTwo(5, b="foo")
    assert res == "Got int-string"

    res = MethodTest.TestOverloadedObjectTwo(a="foo", b=7.24)
    assert res == "Got string-object"

    res = MethodTest.TestOverloadedObjectTwo("foo", b=7.24)
    assert res == "Got string-object"

    res = MethodTest.TestOverloadedObjectTwo(a="foo", b="bar")
    assert res == "Got string-string"

    res = MethodTest.TestOverloadedObjectTwo("foo", b="bar")
    assert res == "Got string-string"

    res = MethodTest.TestOverloadedObjectTwo(a="foo", b=5)
    assert res == "Got string-int"

    res = MethodTest.TestOverloadedObjectTwo("foo", b=5)
    assert res == "Got string-int"

    res = MethodTest.TestOverloadedObjectTwo(a=7.24, b=7.24)
    assert res == "Got object-object"

    res = MethodTest.TestOverloadedObjectTwo(7.24, b=7.24)
    assert res == "Got object-object"


def test_object_in_multiparam_exception():
    """Test method with object multiparams behaves"""

    with pytest.raises(TypeError) as excinfo:
        MethodTest.TestOverloadedObjectThree("foo", "bar")

    e = excinfo.value
    c = e.__cause__
    assert c.GetType().FullName == 'System.AggregateException'
    assert len(c.InnerExceptions) == 2

def test_case_sensitive():
    """Test that case-sensitivity is respected. GH#81"""

    res = MethodTest.CaseSensitive()
    assert res == "CaseSensitive"

    res = MethodTest.Casesensitive()
    assert res == "Casesensitive"

    with pytest.raises(AttributeError):
        MethodTest.casesensitive()

def test_getting_generic_method_binding_does_not_leak_ref_count():
    """Test that managed object is freed after calling generic method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import sys

    refCount = sys.getrefcount(PlainOldClass().GenericMethod[str])
    assert refCount == 1

def test_getting_generic_method_binding_does_not_leak_memory():
    """Test that managed object is freed after calling generic method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import psutil, os, gc, clr

    process = psutil.Process(os.getpid())
    processBytesBeforeCall = process.memory_info().rss
    print("\n\nMemory consumption (bytes) at start of test: " + str(processBytesBeforeCall))

    iterations = 500
    for i in range(iterations):
        PlainOldClass().GenericMethod[str]

    gc.collect()
    System.GC.Collect()

    processBytesAfterCall = process.memory_info().rss
    print("Memory consumption (bytes) at end of test: " + str(processBytesAfterCall))
    processBytesDelta = processBytesAfterCall - processBytesBeforeCall
    print("Memory delta: " + str(processBytesDelta))

    bytesAllocatedPerIteration = pow(2, 20)  # 1MB
    bytesLeakedPerIteration = processBytesDelta / iterations

    # Allow 50% threshold - this shows the original issue is fixed, which leaks the full allocated bytes per iteration
    failThresholdBytesLeakedPerIteration = bytesAllocatedPerIteration / 2

    assert bytesLeakedPerIteration < failThresholdBytesLeakedPerIteration

def test_getting_overloaded_method_binding_does_not_leak_ref_count():
    """Test that managed object is freed after calling overloaded method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import sys

    refCount = sys.getrefcount(PlainOldClass().OverloadedMethod.Overloads[int])
    assert refCount == 1

def test_getting_overloaded_method_binding_does_not_leak_memory():
    """Test that managed object is freed after calling overloaded method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import psutil, os, gc, clr

    process = psutil.Process(os.getpid())
    processBytesBeforeCall = process.memory_info().rss
    print("\n\nMemory consumption (bytes) at start of test: " + str(processBytesBeforeCall))

    iterations = 500
    for i in range(iterations):
        PlainOldClass().OverloadedMethod.Overloads[int]

    gc.collect()
    System.GC.Collect()

    processBytesAfterCall = process.memory_info().rss
    print("Memory consumption (bytes) at end of test: " + str(processBytesAfterCall))
    processBytesDelta = processBytesAfterCall - processBytesBeforeCall
    print("Memory delta: " + str(processBytesDelta))

    bytesAllocatedPerIteration = pow(2, 20)  # 1MB
    bytesLeakedPerIteration = processBytesDelta / iterations

    # Allow 50% threshold - this shows the original issue is fixed, which leaks the full allocated bytes per iteration
    failThresholdBytesLeakedPerIteration = bytesAllocatedPerIteration / 2

    assert bytesLeakedPerIteration < failThresholdBytesLeakedPerIteration

def test_getting_method_overloads_binding_does_not_leak_ref_count():
    """Test that managed object is freed after calling overloaded method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import sys

    refCount = sys.getrefcount(PlainOldClass().OverloadedMethod.Overloads)
    assert refCount == 1

def test_getting_method_overloads_binding_does_not_leak_memory():
    """Test that managed object is freed after calling overloaded method. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import psutil, os, gc, clr

    process = psutil.Process(os.getpid())
    processBytesBeforeCall = process.memory_info().rss
    print("\n\nMemory consumption (bytes) at start of test: " + str(processBytesBeforeCall))

    iterations = 500
    for i in range(iterations):
        PlainOldClass().OverloadedMethod.Overloads

    gc.collect()
    System.GC.Collect()

    processBytesAfterCall = process.memory_info().rss
    print("Memory consumption (bytes) at end of test: " + str(processBytesAfterCall))
    processBytesDelta = processBytesAfterCall - processBytesBeforeCall
    print("Memory delta: " + str(processBytesDelta))

    bytesAllocatedPerIteration = pow(2, 20)  # 1MB
    bytesLeakedPerIteration = processBytesDelta / iterations

    # Allow 50% threshold - this shows the original issue is fixed, which leaks the full allocated bytes per iteration
    failThresholdBytesLeakedPerIteration = bytesAllocatedPerIteration / 2

    assert bytesLeakedPerIteration < failThresholdBytesLeakedPerIteration

def test_getting_overloaded_constructor_binding_does_not_leak_ref_count():
    """Test that managed object is freed after calling overloaded constructor, constructorbinding.cs mp_subscript. Issue #691"""

    from PlainOldNamespace import PlainOldClass

    import sys

    # simple test
    refCount = sys.getrefcount(PlainOldClass.Overloads[int])
    assert refCount == 1


def test_default_params():
    # all positional parameters
    res = MethodTest.DefaultParams(1,2,3,4)
    assert res == "1234"

    res = MethodTest.DefaultParams(1, 2, 3)
    assert res == "1230"

    res = MethodTest.DefaultParams(1, 2)
    assert res == "1200"

    res = MethodTest.DefaultParams(1)
    assert res == "1000"

    res = MethodTest.DefaultParams(a=2)
    assert res == "2000"

    res = MethodTest.DefaultParams(b=3)
    assert res == "0300"

    res = MethodTest.DefaultParams(c=4)
    assert res == "0040"

    res = MethodTest.DefaultParams(d=7)
    assert res == "0007"

    res = MethodTest.DefaultParams(a=2, c=5)
    assert res == "2050"

    res = MethodTest.DefaultParams(1, d=7, c=3)
    assert res == "1037"

    with pytest.raises(TypeError):
        MethodTest.DefaultParams(1,2,3,4,5)

def test_optional_params():
    res = MethodTest.OptionalParams(1, 2, 3, 4)
    assert res == "1234"

    res = MethodTest.OptionalParams(1, 2, 3)
    assert res == "1230"

    res = MethodTest.OptionalParams(1, 2)
    assert res == "1200"

    res = MethodTest.OptionalParams(1)
    assert res == "1000"

    res = MethodTest.OptionalParams(a=2)
    assert res == "2000"

    res = MethodTest.OptionalParams(b=3)
    assert res == "0300"

    res = MethodTest.OptionalParams(c=4)
    assert res == "0040"

    res = MethodTest.OptionalParams(d=7)
    assert res == "0007"

    res = MethodTest.OptionalParams(a=2, c=5)
    assert res == "2050"

    res = MethodTest.OptionalParams(1, d=7, c=3)
    assert res == "1037"

    res = MethodTest.OptionalParams_TestMissing()
    assert res == True

    res = MethodTest.OptionalParams_TestMissing(None)
    assert res == False

    res = MethodTest.OptionalParams_TestMissing(a = None)
    assert res == False

    res = MethodTest.OptionalParams_TestMissing(a='hi')
    assert res == False

    res = MethodTest.OptionalParams_TestReferenceType()
    assert res == True

    res = MethodTest.OptionalParams_TestReferenceType(None)
    assert res == True

    res = MethodTest.OptionalParams_TestReferenceType(a=None)
    assert res == True

    res = MethodTest.OptionalParams_TestReferenceType('hi')
    assert res == False

    res = MethodTest.OptionalParams_TestReferenceType(a='hi')
    assert res == False

def test_optional_and_default_params():

    res = MethodTest.OptionalAndDefaultParams()
    assert res == "0000"

    res = MethodTest.OptionalAndDefaultParams(1)
    assert res == "1000"

    res = MethodTest.OptionalAndDefaultParams(1, c=4)
    assert res == "1040"

    res = MethodTest.OptionalAndDefaultParams(b=4, c=7)
    assert res == "0470"

    res = MethodTest.OptionalAndDefaultParams2()
    assert res == "0012"

    res = MethodTest.OptionalAndDefaultParams2(a=1,b=2,c=3,d=4)
    assert res == "1234"

    res = MethodTest.OptionalAndDefaultParams2(b=2, c=3)
    assert res == "0232"

def test_default_params_overloads():
    res = MethodTest.DefaultParamsWithOverloading(1, 2)
    assert res == "12"

    res = MethodTest.DefaultParamsWithOverloading(b=5)
    assert res == "25"

    res = MethodTest.DefaultParamsWithOverloading("d")
    assert res == "dbX"

    res = MethodTest.DefaultParamsWithOverloading(b="c")
    assert res == "acX"

    res = MethodTest.DefaultParamsWithOverloading(c=3)
    assert res == "013XX"

    res = MethodTest.DefaultParamsWithOverloading(5, c=2)
    assert res == "512XX"

    res = MethodTest.DefaultParamsWithOverloading(c=0, d=1)
    assert res == "5601XXX"

    res = MethodTest.DefaultParamsWithOverloading(1, d=1)
    assert res == "1671XXX"

def test_default_params_overloads_ambiguous_call():
    with pytest.raises(TypeError):
        MethodTest.DefaultParamsWithOverloading()

def test_keyword_arg_method_resolution():
    from Python.Test import MethodArityTest

    ob = MethodArityTest()
    assert ob.Foo(1, b=2) == "Arity 2"

def test_params_array_overload():
    res = MethodTest.ParamsArrayOverloaded()
    assert res == "without params-array"

    res = MethodTest.ParamsArrayOverloaded(1)
    assert res == "without params-array"

    res = MethodTest.ParamsArrayOverloaded(i=1)
    assert res == "without params-array"

    res = MethodTest.ParamsArrayOverloaded(1, 2)
    assert res == "with params-array"

    res = MethodTest.ParamsArrayOverloaded(1, 2, 3)
    assert res == "with params-array"

    res = MethodTest.ParamsArrayOverloaded(1, paramsArray=[])
    assert res == "with params-array"

    res = MethodTest.ParamsArrayOverloaded(1, i=1)
    assert res == "with params-array"

    res = MethodTest.ParamsArrayOverloaded(1, 2, 3, i=1)
    assert res == "with params-array"

@pytest.mark.skip(reason="FIXME: incorrectly failing")
def test_params_array_overloaded_failing():
    res = MethodTest.ParamsArrayOverloaded(1, 2, i=1)
    assert res == "with params-array"

    res = MethodTest.ParamsArrayOverloaded(paramsArray=[], i=1)
    assert res == "with params-array"

def test_method_encoding():
    MethodTest.EncodingTestÅngström()

def test_method_with_pointer_array_argument():
    with pytest.raises(TypeError):
        MethodTest.PointerArray([0])
