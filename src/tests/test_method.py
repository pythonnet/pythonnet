# -*- coding: utf-8 -*-

import unittest

import System
from Python.Test import MethodTest

from _compat import PY2, long, unichr


class MethodTests(unittest.TestCase):
    """Test CLR method support."""

    def test_instance_method_descriptor(self):
        """Test instance method descriptor behavior."""

        with self.assertRaises(AttributeError):
            MethodTest().PublicMethod = 0

        with self.assertRaises(AttributeError):
            MethodTest.PublicMethod = 0

        with self.assertRaises(AttributeError):
            del MethodTest().PublicMethod

        with self.assertRaises(AttributeError):
            del MethodTest.PublicMethod

    def test_static_method_descriptor(self):
        """Test static method descriptor behavior."""

        with self.assertRaises(AttributeError):
            MethodTest().PublicStaticMethod = 0

        with self.assertRaises(AttributeError):
            MethodTest.PublicStaticMethod = 0

        with self.assertRaises(AttributeError):
            del MethodTest().PublicStaticMethod

        with self.assertRaises(AttributeError):
            del MethodTest.PublicStaticMethod

    def test_public_instance_method(self):
        """Test public instance method visibility."""
        ob = MethodTest()
        self.assertTrue(ob.PublicMethod() == "public")

    def test_public_static_method(self):
        """Test public static method visibility."""
        ob = MethodTest()
        self.assertTrue(MethodTest.PublicStaticMethod() == "public static")
        self.assertTrue(ob.PublicStaticMethod() == "public static")

    def test_protected_instance_method(self):
        """Test protected instance method visibility."""
        ob = MethodTest()
        self.assertTrue(ob.ProtectedMethod() == "protected")

    def test_protected_static_method(self):
        """Test protected static method visibility."""
        ob = MethodTest()
        result = "protected static"
        self.assertTrue(MethodTest.ProtectedStaticMethod() == result)
        self.assertTrue(ob.ProtectedStaticMethod() == result)

    def test_internal_method(self):
        """Test internal method visibility."""

        with self.assertRaises(AttributeError):
            _ = MethodTest().InternalMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest.InternalMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest().InternalStaticMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest.InternalStaticMethod

    def test_private_method(self):
        """Test private method visibility."""

        with self.assertRaises(AttributeError):
            _ = MethodTest().PrivateMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest.PrivateMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest().PrivateStaticMethod

        with self.assertRaises(AttributeError):
            _ = MethodTest.PrivateStaticMethod

    def test_unbound_managed_method_call(self):
        """Test calling unbound managed methods."""
        from Python.Test import MethodTestSub

        ob = MethodTest()
        self.assertTrue(MethodTest.PublicMethod(ob) == "public")

        with self.assertRaises(TypeError):
            MethodTest.PublicMethod()

        ob = MethodTestSub()
        self.assertTrue(MethodTestSub.PublicMethod(ob) == "public")
        self.assertTrue(MethodTestSub.PublicMethod(ob, "echo") == "echo")

        with self.assertRaises(TypeError):
            MethodTestSub.PublicMethod("echo")

    def test_overloaded_method_inheritance(self):
        """Test that overloads are inherited properly."""
        from Python.Test import MethodTestSub

        ob = MethodTest()
        self.assertTrue(ob.PublicMethod() == "public")

        with self.assertRaises(TypeError):
            ob = MethodTest()
            ob.PublicMethod("echo")

        ob = MethodTestSub()
        self.assertTrue(ob.PublicMethod() == "public")

        self.assertTrue(ob.PublicMethod("echo") == "echo")

    def test_method_descriptor_abuse(self):
        """Test method descriptor abuse."""
        desc = MethodTest.__dict__['PublicMethod']

        with self.assertRaises(TypeError):
            desc.__get__(0, 0)

        with self.assertRaises(AttributeError):
            desc.__set__(0, 0)

    def test_method_docstrings(self):
        """Test standard method docstring generation"""
        method = MethodTest.GetType
        value = 'System.Type GetType()'
        self.assertTrue(method.__doc__ == value)

    # ======================================================================
    # Tests of specific argument and result conversion scenarios
    # ======================================================================

    def test_method_call_enum_conversion(self):
        """Test enum conversion in method call."""
        from System import TypeCode

        ob = MethodTest()
        r = ob.TestEnumConversion(TypeCode.Int32)
        self.assertTrue(r == TypeCode.Int32)

    def test_method_call_flags_conversion(self):
        """Test flags conversion in method call."""
        from System.IO import FileAccess

        ob = MethodTest()
        flags = FileAccess.Read | FileAccess.Write
        r = ob.TestFlagsConversion(flags)
        self.assertTrue(r == flags)

    def test_method_call_struct_conversion(self):
        """Test struct conversion in method call."""
        from System import Guid

        ob = MethodTest()
        guid = Guid.NewGuid()
        temp = guid.ToString()
        r = ob.TestStructConversion(guid)
        self.assertTrue(r.ToString() == temp)

    def test_subclass_instance_conversion(self):
        """Test subclass instance conversion in method call."""

        class TestSubException(System.Exception):
            pass

        ob = MethodTest()
        instance = TestSubException()
        result = ob.TestSubclassConversion(instance)
        self.assertTrue(isinstance(result, System.Exception))

    def test_null_array_conversion(self):
        """Test null array conversion in method call."""
        ob = MethodTest()
        r = ob.TestNullArrayConversion(None)
        self.assertTrue(r is None)

    def test_string_params_args(self):
        """Test use of string params."""
        result = MethodTest.TestStringParamsArg('one', 'two', 'three')
        self.assertEqual(result.Length, 3)
        self.assertEqual(len(result), 3, result)
        self.assertTrue(result[0] == 'one')
        self.assertTrue(result[1] == 'two')
        self.assertTrue(result[2] == 'three')

        result = MethodTest.TestStringParamsArg(['one', 'two', 'three'])
        self.assertTrue(len(result) == 3)
        self.assertTrue(result[0] == 'one')
        self.assertTrue(result[1] == 'two')
        self.assertTrue(result[2] == 'three')

    def test_object_params_args(self):
        """Test use of object params."""
        result = MethodTest.TestObjectParamsArg('one', 'two', 'three')
        self.assertEqual(len(result), 3, result)
        self.assertTrue(result[0] == 'one')
        self.assertTrue(result[1] == 'two')
        self.assertTrue(result[2] == 'three')

        result = MethodTest.TestObjectParamsArg(['one', 'two', 'three'])
        self.assertEqual(len(result), 3, result)
        self.assertTrue(result[0] == 'one')
        self.assertTrue(result[1] == 'two')
        self.assertTrue(result[2] == 'three')

    def test_value_params_args(self):
        """Test use of value type params."""
        result = MethodTest.TestValueParamsArg(1, 2, 3)
        self.assertEqual(len(result), 3)
        self.assertTrue(result[0] == 1)
        self.assertTrue(result[1] == 2)
        self.assertTrue(result[2] == 3)

        result = MethodTest.TestValueParamsArg([1, 2, 3])
        self.assertEqual(len(result), 3)
        self.assertTrue(result[0] == 1)
        self.assertTrue(result[1] == 2)
        self.assertTrue(result[2] == 3)

    def test_non_params_array_in_last_place(self):
        """Test overload resolution with of non-"params" array as
        last parameter."""
        result = MethodTest.TestNonParamsArrayInLastPlace(1, 2, 3)
        self.assertTrue(result)

    def test_string_out_params(self):
        """Test use of string out-parameters."""
        result = MethodTest.TestStringOutParams("hi", "there")
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringOutParams("hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

    def test_string_ref_params(self):
        """Test use of string byref parameters."""
        result = MethodTest.TestStringRefParams("hi", "there")
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringRefParams("hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

    def test_value_out_params(self):
        """Test use of value type out-parameters."""
        result = MethodTest.TestValueOutParams("hi", 1)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == 42)

        # None cannot be converted to a value type like int, long, etc.
        with self.assertRaises(TypeError):
            MethodTest.TestValueOutParams("hi", None)

    def test_value_ref_params(self):
        """Test use of value type byref parameters."""
        result = MethodTest.TestValueRefParams("hi", 1)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == 42)

        # None cannot be converted to a value type like int, long, etc.
        with self.assertRaises(TypeError):
            MethodTest.TestValueRefParams("hi", None)

    def test_object_out_params(self):
        """Test use of object out-parameters."""
        result = MethodTest.TestObjectOutParams("hi", MethodTest())
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectOutParams("hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Exception))

    def test_object_ref_params(self):
        """Test use of object byref parameters."""
        result = MethodTest.TestObjectRefParams("hi", MethodTest())
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectRefParams("hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Exception))

    def test_struct_out_params(self):
        """Test use of struct out-parameters."""
        result = MethodTest.TestStructOutParams("hi", System.Guid.NewGuid())
        self.assertTrue(isinstance(result, tuple))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Guid))

        # None cannot be converted to a value type like a struct
        with self.assertRaises(TypeError):
            MethodTest.TestValueRefParams("hi", None)

    def test_struct_ref_params(self):
        """Test use of struct byref parameters."""
        result = MethodTest.TestStructRefParams("hi", System.Guid.NewGuid())
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(isinstance(result[1], System.Guid))

        # None cannot be converted to a value type like a struct
        with self.assertRaises(TypeError):
            MethodTest.TestValueRefParams("hi", None)

    def test_void_single_out_param(self):
        """Test void method with single out-parameter."""
        result = MethodTest.TestVoidSingleOutParam(9)
        self.assertTrue(result == 42)

        # None cannot be converted to a value type
        with self.assertRaises(TypeError):
            MethodTest.TestVoidSingleOutParam(None)

    def test_void_single_ref_param(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestVoidSingleRefParam(9)
        self.assertTrue(result == 42)

        # None cannot be converted to a value type
        with self.assertRaises(TypeError):
            MethodTest.TestVoidSingleRefParam(None)

    def test_single_default_param(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestSingleDefaultParam()
        self.assertTrue(result == 5)

    def test_one_arg_and_two_default_param(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestOneArgAndTwoDefaultParam(11)
        self.assertTrue(result == 22)

        result = MethodTest.TestOneArgAndTwoDefaultParam(15)
        self.assertTrue(result == 26)

        result = MethodTest.TestOneArgAndTwoDefaultParam(20)
        self.assertTrue(result == 31)

    def test_two_default_param(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestTwoDefaultParam()
        self.assertTrue(result == 11)

    def test_explicit_selection_with_out_modifier(self):
        """Check explicit overload selection with out modifiers."""
        refstr = System.String("").GetType().MakeByRefType()
        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", "there")
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

    def test_explicit_selection_with_ref_modifier(self):
        """Check explicit overload selection with ref modifiers."""
        refstr = System.String("").GetType().MakeByRefType()
        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", "there")
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", None)
        self.assertTrue(isinstance(result, tuple))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] is True)
        self.assertTrue(result[1] == "output string")

    def test_explicit_overload_selection(self):
        """Check explicit overload selection using [] syntax."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array

        inst = InterfaceTest()

        value = MethodTest.Overloaded.__overloads__[System.Boolean](True)
        self.assertTrue(value is True)

        value = MethodTest.Overloaded.__overloads__[bool](True)
        self.assertTrue(value is True)

        value = MethodTest.Overloaded.__overloads__[System.Byte](255)
        self.assertTrue(value == 255)

        value = MethodTest.Overloaded.__overloads__[System.SByte](127)
        self.assertTrue(value == 127)

        value = MethodTest.Overloaded.__overloads__[System.Char](u'A')
        self.assertTrue(value == u'A')

        value = MethodTest.Overloaded.__overloads__[System.Char](65535)
        self.assertTrue(value == unichr(65535))

        value = MethodTest.Overloaded.__overloads__[System.Int16](32767)
        self.assertTrue(value == 32767)

        value = MethodTest.Overloaded.__overloads__[System.Int32](2147483647)
        self.assertTrue(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[int](2147483647)
        self.assertTrue(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[System.Int64](
            long(9223372036854775807))
        self.assertTrue(value == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            value = MethodTest.Overloaded.__overloads__[long](
                long(9223372036854775807))
            self.assertTrue(value == long(9223372036854775807))

        value = MethodTest.Overloaded.__overloads__[System.UInt16](65000)
        self.assertTrue(value == 65000)

        value = MethodTest.Overloaded.__overloads__[System.UInt32](
            long(4294967295))
        self.assertTrue(value == long(4294967295))

        value = MethodTest.Overloaded.__overloads__[System.UInt64](
            long(18446744073709551615))
        self.assertTrue(value == long(18446744073709551615))

        value = MethodTest.Overloaded.__overloads__[System.Single](3.402823e38)
        self.assertTrue(value == 3.402823e38)

        value = MethodTest.Overloaded.__overloads__[System.Double](
            1.7976931348623157e308)
        self.assertTrue(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[float](
            1.7976931348623157e308)
        self.assertTrue(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[System.Decimal](
            System.Decimal.One)
        self.assertTrue(value == System.Decimal.One)

        value = MethodTest.Overloaded.__overloads__[System.String]("spam")
        self.assertTrue(value == "spam")

        value = MethodTest.Overloaded.__overloads__[str]("spam")
        self.assertTrue(value == "spam")

        value = MethodTest.Overloaded.__overloads__[ShortEnum](ShortEnum.Zero)
        self.assertTrue(value == ShortEnum.Zero)

        value = MethodTest.Overloaded.__overloads__[System.Object](inst)
        self.assertTrue(value.__class__ == inst.__class__)

        value = MethodTest.Overloaded.__overloads__[InterfaceTest](inst)
        self.assertTrue(value.__class__ == inst.__class__)

        value = MethodTest.Overloaded.__overloads__[ISayHello1](inst)
        self.assertTrue(value.__class__ == inst.__class__)

        atype = Array[System.Object]
        value = MethodTest.Overloaded.__overloads__[str, int, atype](
            "one", 1, atype([1, 2, 3]))
        self.assertTrue(value == 3)

        value = MethodTest.Overloaded.__overloads__[str, int]("one", 1)
        self.assertTrue(value == 1)

        value = MethodTest.Overloaded.__overloads__[int, str](1, "one")
        self.assertTrue(value == 1)

    def test_overload_selection_with_array_types(self):
        """Check overload selection using array types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array

        inst = InterfaceTest()

        vtype = Array[System.Boolean]
        input_ = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] is True)
        self.assertTrue(value[1] is True)

        vtype = Array[bool]
        input_ = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] is True)
        self.assertTrue(value[1] is True)

        vtype = Array[System.Byte]
        input_ = vtype([0, 255])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 255)

        vtype = Array[System.SByte]
        input_ = vtype([0, 127])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 127)

        vtype = Array[System.Char]
        input_ = vtype([u'A', u'Z'])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == u'A')
        self.assertTrue(value[1] == u'Z')

        vtype = Array[System.Char]
        input_ = vtype([0, 65535])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == unichr(0))
        self.assertTrue(value[1] == unichr(65535))

        vtype = Array[System.Int16]
        input_ = vtype([0, 32767])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 32767)

        vtype = Array[System.Int32]
        input_ = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)

        vtype = Array[int]
        input_ = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)

        vtype = Array[System.Int64]
        input_ = vtype([0, long(9223372036854775807)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if PY2:
            vtype = Array[long]
            input_ = vtype([0, long(9223372036854775807)])
            value = MethodTest.Overloaded.__overloads__[vtype](input_)
            self.assertTrue(value[0] == 0)
            self.assertTrue(value[1] == long(9223372036854775807))

        vtype = Array[System.UInt16]
        input_ = vtype([0, 65000])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 65000)

        vtype = Array[System.UInt32]
        input_ = vtype([0, long(4294967295)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(4294967295))

        vtype = Array[System.UInt64]
        input_ = vtype([0, long(18446744073709551615)])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(18446744073709551615))

        vtype = Array[System.Single]
        input_ = vtype([0.0, 3.402823e38])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 3.402823e38)

        vtype = Array[System.Double]
        input_ = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)

        vtype = Array[float]
        input_ = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)

        vtype = Array[System.Decimal]
        input_ = vtype([System.Decimal.Zero, System.Decimal.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == System.Decimal.Zero)
        self.assertTrue(value[1] == System.Decimal.One)

        vtype = Array[System.String]
        input_ = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")

        vtype = Array[str]
        input_ = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")

        vtype = Array[ShortEnum]
        input_ = vtype([ShortEnum.Zero, ShortEnum.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0] == ShortEnum.Zero)
        self.assertTrue(value[1] == ShortEnum.One)

        vtype = Array[System.Object]
        input_ = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

        vtype = Array[InterfaceTest]
        input_ = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

        vtype = Array[ISayHello1]
        input_ = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input_)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

    def test_explicit_overload_selection_failure(self):
        """Check that overload selection fails correctly."""

        with self.assertRaises(TypeError):
            _ = MethodTest.Overloaded.__overloads__[System.Type](True)

        with self.assertRaises(TypeError):
            _ = MethodTest.Overloaded.__overloads__[int, int](1, 1)

        with self.assertRaises(TypeError):
            _ = MethodTest.Overloaded.__overloads__[str, int, int]("", 1, 1)

        with self.assertRaises(TypeError):
            _ = MethodTest.Overloaded.__overloads__[int, long](1)

    def test_we_can_bind_to_encoding_get_string(self):
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
        self.assertEqual(data, 'Some testing string')


def test_suite():
    return unittest.makeSuite(MethodTests)
