# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
from Python.Test import MethodTest, MethodTestSub
import System

class MethodTests(unittest.TestCase):
    """Test CLR method support."""

    def testInstanceMethodDescriptor(self):
        """Test instance method descriptor behavior."""
        def test():
            MethodTest().PublicMethod = 0

        self.failUnlessRaises(AttributeError, test)

        def test():
            MethodTest.PublicMethod = 0

        self.failUnlessRaises(AttributeError, test)

        def test():
            del MethodTest().PublicMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            del MethodTest.PublicMethod

        self.failUnlessRaises(AttributeError, test)


    def testStaticMethodDescriptor(self):
        """Test static method descriptor behavior."""
        def test():
            MethodTest().PublicStaticMethod = 0

        self.failUnlessRaises(AttributeError, test)

        def test():
            MethodTest.PublicStaticMethod = 0

        self.failUnlessRaises(AttributeError, test)

        def test():
            del MethodTest().PublicStaticMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            del MethodTest.PublicStaticMethod

        self.failUnlessRaises(AttributeError, test)


    def testPublicInstanceMethod(self):
        """Test public instance method visibility."""
        object = MethodTest();
        self.failUnless(object.PublicMethod() == "public")


    def testPublicStaticMethod(self):
        """Test public static method visibility."""
        object = MethodTest();
        self.failUnless(MethodTest.PublicStaticMethod() == "public static")
        self.failUnless(object.PublicStaticMethod() == "public static")


    def testProtectedInstanceMethod(self):
        """Test protected instance method visibility."""
        object = MethodTest();
        self.failUnless(object.ProtectedMethod() == "protected")


    def testProtectedStaticMethod(self):
        """Test protected static method visibility."""
        object = MethodTest();
        result = "protected static"
        self.failUnless(MethodTest.ProtectedStaticMethod() == result)
        self.failUnless(object.ProtectedStaticMethod() == result)


    def testInternalMethod(self):
        """Test internal method visibility."""
        def test():
            f = MethodTest().InternalMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest.InternalMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest().InternalStaticMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest.InternalStaticMethod

        self.failUnlessRaises(AttributeError, test)


    def testPrivateMethod(self):
        """Test private method visibility."""
        def test():
            f = MethodTest().PrivateMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest.PrivateMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest().PrivateStaticMethod

        self.failUnlessRaises(AttributeError, test)

        def test():
            f = MethodTest.PrivateStaticMethod

        self.failUnlessRaises(AttributeError, test)


    def testUnboundManagedMethodCall(self):
        """Test calling unbound managed methods."""

        object = MethodTest()
        self.failUnless(MethodTest.PublicMethod(object) == "public")

        object = MethodTestSub();
        self.failUnless(MethodTest.PublicMethod(object) == "public")
        self.failUnless(MethodTestSub.PublicMethod(object) == "public")

        self.failUnless(MethodTestSub.PublicMethod(object, "echo") == "echo")


    def testOverloadedMethodInheritance(self):
        """Test that overloads are inherited properly."""

        object = MethodTest()
        self.failUnless(object.PublicMethod() == "public")

        def test():
            object = MethodTest()
            object.PublicMethod("echo")

        self.failUnlessRaises(TypeError, test)


        object = MethodTestSub();
        self.failUnless(object.PublicMethod() == "public")

        self.failUnless(object.PublicMethod("echo") == "echo")


    def testMethodDescriptorAbuse(self):
        """Test method descriptor abuse."""
        desc = MethodTest.__dict__['PublicMethod']

        def test():
            desc.__get__(0, 0)

        self.failUnlessRaises(TypeError, test)

        def test():
            desc.__set__(0, 0)

        self.failUnlessRaises(AttributeError, test)


    def testMethodDocstrings(self):
        """Test standard method docstring generation"""
        method = MethodTest.GetType
        value = 'System.Type GetType()'
        self.failUnless(method.__doc__ == value)


    #======================================================================
    # Tests of specific argument and result conversion scenarios
    #======================================================================

    def testMethodCallEnumConversion(self):
        """Test enum conversion in method call."""
        from System import TypeCode

        object = MethodTest()
        r = object.TestEnumConversion(TypeCode.Int32)
        self.failUnless(r == TypeCode.Int32)


    def testMethodCallFlagsConversion(self):
        """Test flags conversion in method call."""
        from System.IO import FileAccess

        object = MethodTest()
        flags = FileAccess.Read | FileAccess.Write
        r = object.TestFlagsConversion(flags)
        self.failUnless(r == flags)


    def testMethodCallStructConversion(self):
        """Test struct conversion in method call."""
        from System import Guid

        object = MethodTest()
        guid = Guid.NewGuid()
        temp = guid.ToString()
        r = object.TestStructConversion(guid)
        self.failUnless(r.ToString() == temp)


    def testSubclassInstanceConversion(self):
        """Test subclass instance conversion in method call."""
        from System.Windows.Forms import Form, Control

        class sub(Form):
            pass

        object = MethodTest()
        form = sub()
        result = object.TestSubclassConversion(form)
        self.failUnless(isinstance(result, Control))


    def testNullArrayConversion(self):
        """Test null array conversion in method call."""
        from System import Type

        object = MethodTest()
        r = object.TestNullArrayConversion(None)
        self.failUnless(r == None)


    def testStringParamsArgs(self):
        """Test use of string params."""
        result = MethodTest.TestStringParamsArg('one', 'two', 'three')
        self.failUnlessEqual(result.Length, 3)
        self.failUnlessEqual(len(result), 3, result)
        self.failUnless(result[0] == 'one')
        self.failUnless(result[1] == 'two')
        self.failUnless(result[2] == 'three')        

        result = MethodTest.TestStringParamsArg(['one', 'two', 'three'])
        self.failUnless(len(result) == 3)
        self.failUnless(result[0] == 'one')
        self.failUnless(result[1] == 'two')
        self.failUnless(result[2] == 'three')        


    def testObjectParamsArgs(self):
        """Test use of object params."""
        result = MethodTest.TestObjectParamsArg('one', 'two', 'three')
        self.failUnlessEqual(len(result), 3, result)
        self.failUnless(result[0] == 'one')
        self.failUnless(result[1] == 'two')
        self.failUnless(result[2] == 'three')        

        result = MethodTest.TestObjectParamsArg(['one', 'two', 'three'])
        self.failUnlessEqual(len(result), 3, result)
        self.failUnless(result[0] == 'one')
        self.failUnless(result[1] == 'two')
        self.failUnless(result[2] == 'three')        


    def testValueParamsArgs(self):
        """Test use of value type params."""
        result = MethodTest.TestValueParamsArg(1, 2, 3)
        self.failUnlessEqual(len(result), 3)
        self.failUnless(result[0] == 1)
        self.failUnless(result[1] == 2)
        self.failUnless(result[2] == 3)        

        result = MethodTest.TestValueParamsArg([1, 2, 3])
        self.failUnlessEqual(len(result), 3)
        self.failUnless(result[0] == 1)
        self.failUnless(result[1] == 2)
        self.failUnless(result[2] == 3)        


    def testStringOutParams(self):
        """Test use of string out-parameters."""
        result = MethodTest.TestStringOutParams("hi", "there")
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringOutParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testStringRefParams(self):
        """Test use of string byref parameters."""
        result = MethodTest.TestStringRefParams("hi", "there")
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringRefParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testValueOutParams(self):
        """Test use of value type out-parameters."""
        result = MethodTest.TestValueOutParams("hi", 1)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == 42)

        def test():
            MethodTest.TestValueOutParams("hi", None)

        # None cannot be converted to a value type like int, long, etc.
        self.failUnlessRaises(TypeError, test)


    def testValueRefParams(self):
        """Test use of value type byref parameters."""
        result = MethodTest.TestValueRefParams("hi", 1)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == 42)

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like int, long, etc.
        self.failUnlessRaises(TypeError, test)


    def testObjectOutParams(self):
        """Test use of object out-parameters."""
        result = MethodTest.TestObjectOutParams("hi", MethodTest())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectOutParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Exception))


    def testObjectRefParams(self):
        """Test use of object byref parameters."""
        result = MethodTest.TestObjectRefParams("hi", MethodTest())
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Exception))

        result = MethodTest.TestObjectRefParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Exception))


    def testStructOutParams(self):
        """Test use of struct out-parameters."""
        result = MethodTest.TestStructOutParams("hi",System.Guid.NewGuid())
        self.failUnless(type(result) == type(()))
        self.failUnlessEqual(len(result), 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.failUnlessRaises(TypeError, test)


    def testStructRefParams(self):
        """Test use of struct byref parameters."""
        result = MethodTest.TestStructRefParams("hi",System.Guid.NewGuid())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.failUnlessRaises(TypeError, test)


    def testVoidSingleOutParam(self):
        """Test void method with single out-parameter."""
        result = MethodTest.TestVoidSingleOutParam(9)
        self.failUnless(result == 42)

        def test():
            MethodTest.TestVoidSingleOutParam(None)

        # None cannot be converted to a value type
        self.failUnlessRaises(TypeError, test)


    def testVoidSingleRefParam(self):
        """Test void method with single ref-parameter."""
        result = MethodTest.TestVoidSingleRefParam(9)
        self.failUnless(result == 42)

        def test():
            MethodTest.TestVoidSingleRefParam(None)

        # None cannot be converted to a value type
        self.failUnlessRaises(TypeError, test)


    def testExplicitSelectionWithOutModifier(self):
        """Check explicit overload selection with out modifiers."""
        refstr = System.String("").GetType().MakeByRefType()
        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", "there"
            )
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringOutParams.__overloads__[str, refstr](
            "hi", None
            )
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testExplicitSelectionWithRefModifier(self):
        """Check explicit overload selection with ref modifiers."""
        refstr = System.String("").GetType().MakeByRefType()        
        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", "there"
            )
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringRefParams.__overloads__[str, refstr](
            "hi", None
            )
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testExplicitOverloadSelection(self):
        """Check explicit overload selection using [] syntax."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        value =MethodTest.Overloaded.__overloads__[System.Boolean](True)
        self.failUnless(value == True)

        value = MethodTest.Overloaded.__overloads__[bool](True)
        self.failUnless(value == True)

        value = MethodTest.Overloaded.__overloads__[System.Byte](255)
        self.failUnless(value == 255)

        value = MethodTest.Overloaded.__overloads__[System.SByte](127)
        self.failUnless(value == 127)

        value = MethodTest.Overloaded.__overloads__[System.Char](u'A')
        self.failUnless(value == u'A')

        value = MethodTest.Overloaded.__overloads__[System.Char](65535)
        self.failUnless(value == unichr(65535))

        value = MethodTest.Overloaded.__overloads__[System.Int16](32767)
        self.failUnless(value == 32767)

        value = MethodTest.Overloaded.__overloads__[System.Int32](2147483647)
        self.failUnless(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[int](2147483647)
        self.failUnless(value == 2147483647)

        value = MethodTest.Overloaded.__overloads__[System.Int64](
            9223372036854775807L
            )
        self.failUnless(value == 9223372036854775807L)

        value = MethodTest.Overloaded.__overloads__[long](
            9223372036854775807L
            )
        self.failUnless(value == 9223372036854775807L)

        value = MethodTest.Overloaded.__overloads__[System.UInt16](65000)
        self.failUnless(value == 65000)

        value = MethodTest.Overloaded.__overloads__[System.UInt32](4294967295L)
        self.failUnless(value == 4294967295L)

        value = MethodTest.Overloaded.__overloads__[System.UInt64](
            18446744073709551615L
            )
        self.failUnless(value == 18446744073709551615L)

        value = MethodTest.Overloaded.__overloads__[System.Single](3.402823e38)
        self.failUnless(value == 3.402823e38)

        value = MethodTest.Overloaded.__overloads__[System.Double](
            1.7976931348623157e308
            )
        self.failUnless(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[float](
            1.7976931348623157e308
            )
        self.failUnless(value == 1.7976931348623157e308)

        value = MethodTest.Overloaded.__overloads__[System.Decimal](
            System.Decimal.One
            )
        self.failUnless(value == System.Decimal.One)

        value = MethodTest.Overloaded.__overloads__[System.String]("spam")
        self.failUnless(value == "spam")

        value = MethodTest.Overloaded.__overloads__[str]("spam")
        self.failUnless(value == "spam")

        value = MethodTest.Overloaded.__overloads__[ShortEnum](ShortEnum.Zero)
        self.failUnless(value == ShortEnum.Zero)

        value = MethodTest.Overloaded.__overloads__[System.Object](inst)
        self.failUnless(value.__class__ == inst.__class__)

        value = MethodTest.Overloaded.__overloads__[InterfaceTest](inst)
        self.failUnless(value.__class__ == inst.__class__)

        value = MethodTest.Overloaded.__overloads__[ISayHello1](inst)
        self.failUnless(value.__class__ == inst.__class__)

        atype = Array[System.Object]
        value = MethodTest.Overloaded.__overloads__[str, int, atype](
            "one", 1, atype([1, 2, 3])
            )
        self.failUnless(value == 3)

        value = MethodTest.Overloaded.__overloads__[str, int]("one", 1)
        self.failUnless(value == 1)

        value = MethodTest.Overloaded.__overloads__[int, str](1, "one")
        self.failUnless(value == 1)


    def testOverloadSelectionWithArrayTypes(self):
        """Check overload selection using array types."""
        from Python.Test import ISayHello1, InterfaceTest, ShortEnum
        from System import Array
        inst = InterfaceTest()

        vtype = Array[System.Boolean]
        input = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == True)
        self.failUnless(value[1] == True)        

        vtype = Array[bool]
        input = vtype([True, True])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == True)
        self.failUnless(value[1] == True)        

        vtype = Array[System.Byte]
        input = vtype([0, 255])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 255)        
        
        vtype = Array[System.SByte]
        input = vtype([0, 127])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 127)        

        vtype = Array[System.Char]
        input = vtype([u'A', u'Z'])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == u'A')
        self.failUnless(value[1] == u'Z')

        vtype = Array[System.Char]
        input = vtype([0, 65535])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == unichr(0))
        self.failUnless(value[1] == unichr(65535))        

        vtype = Array[System.Int16]
        input = vtype([0, 32767])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 32767)        

        vtype = Array[System.Int32]
        input = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 2147483647)        

        vtype = Array[int]
        input = vtype([0, 2147483647])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 2147483647)        

        vtype = Array[System.Int64]
        input = vtype([0, 9223372036854775807L])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 9223372036854775807L)        

        vtype = Array[long]
        input = vtype([0, 9223372036854775807L])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 9223372036854775807L)        

        vtype = Array[System.UInt16]
        input = vtype([0, 65000])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 65000)        

        vtype = Array[System.UInt32]
        input = vtype([0, 4294967295L])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 4294967295L)        

        vtype = Array[System.UInt64]
        input = vtype([0, 18446744073709551615L])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0)
        self.failUnless(value[1] == 18446744073709551615L)        

        vtype = Array[System.Single]
        input = vtype([0.0, 3.402823e38])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 3.402823e38)        

        vtype = Array[System.Double]
        input = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 1.7976931348623157e308)        

        vtype = Array[float]
        input = vtype([0.0, 1.7976931348623157e308])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == 0.0)
        self.failUnless(value[1] == 1.7976931348623157e308)        

        vtype = Array[System.Decimal]
        input = vtype([System.Decimal.Zero, System.Decimal.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == System.Decimal.Zero)
        self.failUnless(value[1] == System.Decimal.One)        

        vtype = Array[System.String]
        input = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == "one")
        self.failUnless(value[1] == "two")

        vtype = Array[str]
        input = vtype(["one", "two"])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == "one")
        self.failUnless(value[1] == "two")

        vtype = Array[ShortEnum]
        input = vtype([ShortEnum.Zero, ShortEnum.One])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0] == ShortEnum.Zero)
        self.failUnless(value[1] == ShortEnum.One)

        vtype = Array[System.Object]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        

        vtype = Array[InterfaceTest]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        

        vtype = Array[ISayHello1]
        input = vtype([inst, inst])
        value = MethodTest.Overloaded.__overloads__[vtype](input)
        self.failUnless(value[0].__class__ == inst.__class__)
        self.failUnless(value[1].__class__ == inst.__class__)        


    def testExplicitOverloadSelectionFailure(self):
        """Check that overload selection fails correctly."""
        
        def test():
            value = MethodTest.Overloaded.__overloads__[System.Type](True)

        self.failUnlessRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[int, int](1, 1)

        self.failUnlessRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[str, int, int](
                "", 1, 1
                )

        self.failUnlessRaises(TypeError, test)

        def test():
            value = MethodTest.Overloaded.__overloads__[int, long](1)

        self.failUnlessRaises(TypeError, test)



def test_suite():
    return unittest.makeSuite(MethodTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

