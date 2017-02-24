# -*- coding: utf-8 -*-

"""Test CLR method support."""

import System
import pytest
from Python.Test import MethodTest

from ._compat import PY2, long, unichr


def test_instance_method_descriptor():
    """Test instance method descriptor behavior."""

    with pytest.raises(AttributeError):
        MethodTest().PublicMethod = 0

    with pytest.raises(AttributeError):
        MethodTest.PublicMethod = 0

    with pytest.raises(AttributeError):
        del MethodTest().PublicMethod

    with pytest.raises(AttributeError):
        del MethodTest.PublicMethod


def test_static_method_descriptor():
    """Test static method descriptor behavior."""

    with pytest.raises(AttributeError):
        MethodTest().PublicStaticMethod = 0

    with pytest.raises(AttributeError):
        MethodTest.PublicStaticMethod = 0

    with pytest.raises(AttributeError):
        del MethodTest().PublicStaticMethod

    with pytest.raises(AttributeError):
        del MethodTest.PublicStaticMethod


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
    assert result.Length == 3
    assert len(result) == 3, result
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'

    result = MethodTest.TestStringParamsArg(['one', 'two', 'three'])
    assert len(result) == 3
    assert result[0] == 'one'
    assert result[1] == 'two'
    assert result[2] == 'three'


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
    assert value == unichr(65535)

    value = MethodTest.Overloaded.__overloads__[System.Int16](32767)
    assert value == 32767

    value = MethodTest.Overloaded.__overloads__[System.Int32](2147483647)
    assert value == 2147483647

    value = MethodTest.Overloaded.__overloads__[int](2147483647)
    assert value == 2147483647

    value = MethodTest.Overloaded.__overloads__[System.Int64](
        long(9223372036854775807))
    assert value == long(9223372036854775807)

    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        value = MethodTest.Overloaded.__overloads__[long](
            long(9223372036854775807))
        assert value == long(9223372036854775807)

    value = MethodTest.Overloaded.__overloads__[System.UInt16](65000)
    assert value == 65000

    value = MethodTest.Overloaded.__overloads__[System.UInt32](
        long(4294967295))
    assert value == long(4294967295)

    value = MethodTest.Overloaded.__overloads__[System.UInt64](
        long(18446744073709551615))
    assert value == long(18446744073709551615)

    value = MethodTest.Overloaded.__overloads__[System.Single](3.402823e38)
    assert value == 3.402823e38

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

    value = MethodTest.Overloaded.__overloads__[ISayHello1](inst)
    assert value.__class__ == inst.__class__

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
    assert value[0] == unichr(0)
    assert value[1] == unichr(65535)

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
    input_ = vtype([0, long(9223372036854775807)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == long(9223372036854775807)

    # Python 3 has no explicit long type, use System.Int64 instead
    if PY2:
        vtype = Array[long]
        input_ = vtype([0, long(9223372036854775807)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        assert value[0] == 0
        assert value[1] == long(9223372036854775807)

    vtype = Array[System.UInt16]
    input_ = vtype([0, 65000])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == 65000

    vtype = Array[System.UInt32]
    input_ = vtype([0, long(4294967295)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == long(4294967295)

    vtype = Array[System.UInt64]
    input_ = vtype([0, long(18446744073709551615)])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0
    assert value[1] == long(18446744073709551615)

    vtype = Array[System.Single]
    input_ = vtype([0.0, 3.402823e38])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0] == 0.0
    assert value[1] == 3.402823e38

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

    vtype = Array[ISayHello1]
    input_ = vtype([inst, inst])
    value = MethodTest.Overloaded.__overloads__[vtype](input_)
    assert value[0].__class__ == inst.__class__
    assert value[1].__class__ == inst.__class__


def test_explicit_overload_selection_failure():
    """Check that overload selection fails correctly."""

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[System.Type](True)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[int, int](1, 1)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[str, int, int]("", 1, 1)

    with pytest.raises(TypeError):
        _ = MethodTest.Overloaded.__overloads__[int, long](1)


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
        read, _ = stream.Read(buff, 0, buff.Length)
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

    with pytest.raises(TypeError):
        MethodTest.TestOverloadedNoObject("test")


def test_object_in_param():
    """Test regression introduced by #151 in which Object method overloads
    aren't being used. See #203 for issue."""

    res = MethodTest.TestOverloadedObject(5)
    assert res == "Got int"

    res = MethodTest.TestOverloadedObject("test")
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


def test_object_in_multiparam_exception():
    """Test method with object multiparams behaves"""

    with pytest.raises(TypeError):
        MethodTest.TestOverloadedObjectThree("foo", "bar")


def test_case_sensitive():
    """Test that case-sensitivity is respected. GH#81"""

    res = MethodTest.CaseSensitive()
    assert res == "CaseSensitive"

    res = MethodTest.Casesensitive()
    assert res == "Casesensitive"

    with pytest.raises(AttributeError):
        MethodTest.casesensitive()
