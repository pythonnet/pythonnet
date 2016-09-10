import sys, os, string, unittest, types
import clr

clr.AddReference("Python.Test")

from Python.Test import MethodTest, MethodTestSub
import System
import six

if six.PY3:
    long = int
    unichr = chr


class MethodTests(unittest.TestCase):
    """Test CLR method support."""

    def testInstanceMethodDescriptor(self):
        """Test instance method descriptor behavior."""

        def test():
            MethodTest().PublicMethod = 0

        self.assertRaises(AttributeError, test)

        def test():
            MethodTest.PublicMethod = 0

        self.assertRaises(AttributeError, test)

        def test():
            del MethodTest().PublicMethod

        self.assertRaises(AttributeError, test)

        def test():
            del MethodTest.PublicMethod

        self.assertRaises(AttributeError, test)

    def testStaticMethodDescriptor(self):
        """Test static method descriptor behavior."""

        def test():
            MethodTest().PublicStaticMethod = 0

        self.assertRaises(AttributeError, test)

        def test():
            MethodTest.PublicStaticMethod = 0

        self.assertRaises(AttributeError, test)

        def test():
            del MethodTest().PublicStaticMethod

        self.assertRaises(AttributeError, test)

        def test():
            del MethodTest.PublicStaticMethod

        self.assertRaises(AttributeError, test)

    def testPublicInstanceMethod(self):
        """Test public instance method visibility."""
        object = MethodTest();
        self.assertTrue(object.PublicMethod() == "public")

    def testPublicStaticMethod(self):
        """Test public static method visibility."""
        object = MethodTest();
        self.assertTrue(MethodTest.PublicStaticMethod() == "public static")
        self.assertTrue(object.PublicStaticMethod() == "public static")

    def testProtectedInstanceMethod(self):
        """Test protected instance method visibility."""
        object = MethodTest();
        self.assertTrue(object.ProtectedMethod() == "protected")

    def testProtectedStaticMethod(self):
        """Test protected static method visibility."""
        object = MethodTest();
        result = "protected static"
        self.assertTrue(MethodTest.ProtectedStaticMethod() == result)
        self.assertTrue(object.ProtectedStaticMethod() == result)

    def testInternalMethod(self):
        """Test internal method visibility."""

        def test():
            f = MethodTest().InternalMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest.InternalMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest().InternalStaticMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest.InternalStaticMethod

        self.assertRaises(AttributeError, test)

    def testPrivateMethod(self):
        """Test private method visibility."""

        def test():
            f = MethodTest().PrivateMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest.PrivateMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest().PrivateStaticMethod

        self.assertRaises(AttributeError, test)

        def test():
            f = MethodTest.PrivateStaticMethod

        self.assertRaises(AttributeError, test)

    def testUnboundManagedMethodCall(self):
        """Test calling unbound managed methods."""

        object = MethodTest()
        self.assertTrue(MethodTest.PublicMethod(object) == "public")

        def test():
            MethodTest.PublicMethod()

        self.assertRaises(TypeError, test)

        object = MethodTestSub();
        self.assertTrue(MethodTestSub.PublicMethod(object) == "public")
        self.assertTrue(MethodTestSub.PublicMethod(object, "echo") == "echo")

        def test():
            MethodTestSub.PublicMethod("echo")

        self.assertRaises(TypeError, test)

    def testOverloadedMethodInheritance(self):
        """Test that overloads are inherited properly."""

        object = MethodTest()
        self.assertTrue(object.PublicMethod() == "public")

        def test():
            object = MethodTest()
            object.PublicMethod("echo")

        self.assertRaises(TypeError, test)

        object = MethodTestSub();
        self.assertTrue(object.PublicMethod() == "public")

        self.assertTrue(object.PublicMethod("echo") == "echo")

    def testMethodDescriptorAbuse(self):
        """Test method descriptor abuse."""
        desc = MethodTest.__dict__['PublicMethod']

        def test():
            desc.__get__(0, 0)

        self.assertRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.assertRaises(AttributeError, test)

    def testMethodDocstrings(self):
        """Test standard method docstring generation"""
        method = MethodTest.GetType
        value = 'System.Type GetType()'
        self.assertTrue(method.__doc__ == value)

    # ======================================================================
    # Tests of specific argument and result conversion scenarios
    # ======================================================================

    def testMethodCallEnumConversion(self):
        """Test enum conversion in method call."""
        from System import TypeCode

        object = MethodTest()
        r = object.TestEnumConversion(TypeCode.Int32)
        self.assertTrue(r == TypeCode.Int32)

    def testMethodCallFlagsConversion(self):
        """Test flags conversion in method call."""
        from System.IO import FileAccess

        object = MethodTest()
        flags = FileAccess.Read | FileAccess.Write
        r = object.TestFlagsConversion(flags)
        self.assertTrue(r == flags)

    def testMethodCallStructConversion(self):
        """Test struct conversion in method call."""
        from System import Guid

        object = MethodTest()
        guid = Guid.NewGuid()
        temp = guid.ToString()
        r = object.TestStructConversion(guid)
        self.assertTrue(r.ToString() == temp)

    def testSubclassInstanceConversion(self):
        """Test subclass instance conversion in method call."""

        class TestSubException(System.Exception):
            pass

        object = MethodTest()
        instance = TestSubException()
        result = object.TestSubclassConversion(instance)
        self.assertTrue(isinstance(result, System.Exception))

    def testNullArrayConversion(self):
        """Test null array conversion in method call."""
        from System import Type

        object = MethodTest()
        r = object.TestNullArrayConversion(None)
        self.assertTrue(r == None)

    def testStringParamsArgs(self):
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

    def testObjectParamsArgs(self):
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

    def testValueParamsArgs(self):
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

    def testNonParamsArrayInLastPlace(self):
        """Test overload resolution with of non-"params" array as last parameter."""
        result = MethodTest.TestNonParamsArrayInLastPlace(1, 2, 3)
        self.assertTrue(result)

    def testStringOutParams(self):
        """Test use of string out-parameters."""
        result = MethodTest.TestStringOutParams("hi", "there")
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringOutParams("hi", None)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

    def testStringRefParams(self):
        """Test use of string byref parameters."""
        result = MethodTest.TestStringRefParams("hi", "there")
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringRefParams("hi", None)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

    def testValueOutParams(self):
        """Test use of value type out-parameters."""
        result = MethodTest.TestValueOutParams("hi", 1)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == 42)

        def test():
            MethodTest.TestValueOutParams("hi", None)

        # None cannot be converted to a value type like int, long, etc.
        self.assertRaises(TypeError, test)

    def testValueRefParams(self):
        """Test use of value type byref parameters."""
        result = MethodTest.TestValueRefParams("hi", 1)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == 42)

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like int, long, etc.
        self.assertRaises(TypeError, test)

    def testObjectOutParams(self):
        """Test use of object out-parameters."""
        result = MethodTest.TestObjectOutParams("hi", MethodTest())
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectOutParams("hi", None)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Exception))

    def testObjectRefParams(self):
        """Test use of object byref parameters."""
        result = MethodTest.TestObjectRefParams("hi", MethodTest())
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectRefParams("hi", None)
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Exception))

    def testStructOutParams(self):
        """Test use of struct out-parameters."""
        result = MethodTest.TestStructOutParams("hi", System.Guid.NewGuid())
        self.assertTrue(type(result) == type(()))
        self.assertEqual(len(result), 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.assertRaises(TypeError, test)

    def testStructRefParams(self):
        """Test use of struct byref parameters."""
        result = MethodTest.TestStructRefParams("hi", System.Guid.NewGuid())
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(isinstance(result[1], System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.assertRaises(TypeError, test)

    def testVoidSingleOutParam(self):
        """Test void method with single out-parameter."""
        result = MethodTest.TestVoidSingleOutParam(9)
        self.assertTrue(result == 42)

        def test():
            MethodTest.TestVoidSingleOutParam(None)

        # None cannot be converted to a value type
        self.assertRaises(TypeError, test)

    def testVoidSingleRefParam(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestVoidSingleRefParam(9)
        self.assertTrue(result == 42)

        def test():
            MethodTest.TestVoidSingleRefParam(None)

        # None cannot be converted to a value type
        self.assertRaises(TypeError, test)

    def testSingleDefaultParam(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestSingleDefaultParam()
        self.assertTrue(result == 5)

    def testOneArgAndTwoDefaultParam(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestOneArgAndTwoDefaultParam(11)
        self.assertTrue(result == 22)

        result = MethodTest.TestOneArgAndTwoDefaultParam(15)
        self.assertTrue(result == 26)

        result = MethodTest.TestOneArgAndTwoDefaultParam(20)
        self.assertTrue(result == 31)

    def testTwoDefaultParam(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestTwoDefaultParam()
        self.assertTrue(result == 11)

    def testExplicitSelectionWithOutModifier(self):
        """Check explicit overload selection with out modifiers."""
        refstr = System.String("").GetType().MakeByRefType()
        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", "there"
        )
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", None
        )
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

    def testExplicitSelectionWithRefModifier(self):
        """Check explicit overload selection with ref modifiers."""
        refstr = System.String("").GetType().MakeByRefType()
        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", "there"
        )
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", None
        )
        self.assertTrue(type(result) == type(()))
        self.assertTrue(len(result) == 2)
        self.assertTrue(result[0] == True)
        self.assertTrue(result[1] == "output string")

    def testExplicitOverloadSelection(self):
        """Check explicit overload selection using [] syntax."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        value = MethodTest.Overloaded.__overloads__[System.Boolean](True)
        self.assertTrue(value == True)

        value = MethodTest.Overloaded.__overloads__[bool](True)
        self.assertTrue(value == True)

        value = MethodTest.Overloaded.__overloads__[System.Byte](255)
        self.assertTrue(value == 255)

        value = MethodTest.Overloaded.__overloads__[System.SByte](127)
        self.assertTrue(value == 127)

        value = MethodTest.Overloaded.__overloads__[System.Char](six.u('A'))
        self.assertTrue(value == six.u('A'))

        value = MethodTest.Overloaded.__overloads__[System.Char](65535)
        self.assertTrue(value == unichr(65535))

        value = MethodTest.Overloaded.__overloads__[System.Int16](32767)
        self.assertTrue(value == 32767)

        value = MethodTest.Overloaded.__overloads__[System.Int32](2147483647)
        self.assertTrue(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[int](2147483647)
        self.assertTrue(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[System.Int64](
            long(9223372036854775807)
        )
        self.assertTrue(value == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            value = MethodTest.Overloaded.__overloads__[long](
                long(9223372036854775807)
            )
            self.assertTrue(value == long(9223372036854775807))

        value = MethodTest.Overloaded.__overloads__[System.UInt16](65000)
        self.assertTrue(value == 65000)

        value = MethodTest.Overloaded.__overloads__[System.UInt32](long(4294967295))
        self.assertTrue(value == long(4294967295))

        value = MethodTest.Overloaded.__overloads__[System.UInt64](
            long(18446744073709551615)
        )
        self.assertTrue(value == long(18446744073709551615))

        value = MethodTest.Overloaded.__overloads__[System.Single](3.402823e38)
        self.assertTrue(value == 3.402823e38)

        value = MethodTest.Overloaded.__overloads__[System.Double](
            1.7976931348623157e308
        )
        self.assertTrue(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[float](
            1.7976931348623157e308
        )
        self.assertTrue(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[System.Decimal](
            System.Decimal.One
        )
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
            "one", 1, atype([1, 2, 3])
        )
        self.assertTrue(value == 3)

        value = MethodTest.Overloaded.__overloads__[str, int]("one", 1)
        self.assertTrue(value == 1)

        value = MethodTest.Overloaded.__overloads__[int, str](1, "one")
        self.assertTrue(value == 1)

    def testOverloadSelectionWithArrayTypes(self):
        """Check overload selection using array types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        vtype = Array[System.Boolean]
        input = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == True)
        self.assertTrue(value[1] == True)

        vtype = Array[bool]
        input = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == True)
        self.assertTrue(value[1] == True)

        vtype = Array[System.Byte]
        input = vtype([0, 255])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 255)

        vtype = Array[System.SByte]
        input = vtype([0, 127])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 127)

        vtype = Array[System.Char]
        input = vtype([six.u('A'), six.u('Z')])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == six.u('A'))
        self.assertTrue(value[1] == six.u('Z'))

        vtype = Array[System.Char]
        input = vtype([0, 65535])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == unichr(0))
        self.assertTrue(value[1] == unichr(65535))

        vtype = Array[System.Int16]
        input = vtype([0, 32767])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 32767)

        vtype = Array[System.Int32]
        input = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)

        vtype = Array[int]
        input = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 2147483647)

        vtype = Array[System.Int64]
        input = vtype([0, long(9223372036854775807)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(9223372036854775807))

        # Python 3 has no explicit long type, use System.Int64 instead
        if not six.PY3:
            vtype = Array[long]
            input = vtype([0, long(9223372036854775807)])
            value = MethodTest.Overloaded.__overloads__[vtype](input)
            self.assertTrue(value[0] == 0)
            self.assertTrue(value[1] == long(9223372036854775807))

        vtype = Array[System.UInt16]
        input = vtype([0, 65000])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == 65000)

        vtype = Array[System.UInt32]
        input = vtype([0, long(4294967295)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(4294967295))

        vtype = Array[System.UInt64]
        input = vtype([0, long(18446744073709551615)])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0)
        self.assertTrue(value[1] == long(18446744073709551615))

        vtype = Array[System.Single]
        input = vtype([0.0, 3.402823e38])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 3.402823e38)

        vtype = Array[System.Double]
        input = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)

        vtype = Array[float]
        input = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == 0.0)
        self.assertTrue(value[1] == 1.7976931348623157e308)

        vtype = Array[System.Decimal]
        input = vtype([System.Decimal.Zero, System.Decimal.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == System.Decimal.Zero)
        self.assertTrue(value[1] == System.Decimal.One)

        vtype = Array[System.String]
        input = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")

        vtype = Array[str]
        input = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == "one")
        self.assertTrue(value[1] == "two")

        vtype = Array[ShortEnum]
        input = vtype([ShortEnum.Zero, ShortEnum.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0] == ShortEnum.Zero)
        self.assertTrue(value[1] == ShortEnum.One)

        vtype = Array[System.Object]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

        vtype = Array[InterfaceTest]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

        vtype = Array[ISayHello1]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.assertTrue(value[0].__class__ == inst.__class__)
        self.assertTrue(value[1].__class__ == inst.__class__)

    def testExplicitOverloadSelectionFailure(self):
        """Check that overload selection fails correctly."""

        def test():
            value = MethodTest.Overloaded.__overloads__[System.Type](True)

        self.assertRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[int, int](1, 1)

        self.assertRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[str, int, int](
                "", 1, 1
            )

        self.assertRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[int, long](1)

        self.assertRaises(TypeError, test)

    def testWeCanBindToEncodingGetString(self):
        """Check that we can bind to the Encoding.GetString method with variables."""
        
        from System.Text import Encoding
        from System.IO import MemoryStream
        myBytes = Encoding.UTF8.GetBytes('Some testing string')
        stream = MemoryStream()
        stream.Write(myBytes, 0, myBytes.Length)
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


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
