# Copyright (c) 2001, 2002 Zope Corporation and Contributors.
#
# All Rights Reserved.
#
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.

import sys, os, string, unittest, types
from CLR.Python.Test import MethodTest
from CLR.Python.Test import MethodTestSub


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
        from CLR.System import TypeCode

        object = MethodTest()
        r = object.TestEnumConversion(TypeCode.Int32)
        self.failUnless(r == TypeCode.Int32)


    def testMethodCallFlagsConversion(self):
        """Test flags conversion in method call."""
        from CLR.System.IO import FileAccess

        object = MethodTest()
        flags = FileAccess.Read | FileAccess.Write
        r = object.TestFlagsConversion(flags)
        self.failUnless(r == flags)


    def testMethodCallStructConversion(self):
        """Test struct conversion in method call."""
        from CLR.System import Guid

        object = MethodTest()
        guid = Guid.NewGuid()
        temp = guid.ToString()
        r = object.TestStructConversion(guid)
        self.failUnless(r.ToString() == temp)


    def testSubclassInstanceConversion(self):
        """Test subclass instance conversion in method call."""
        from CLR.System.Windows.Forms import Form, Control

        class sub(Form):
            pass

        object = MethodTest()
        form = sub()
        result = object.TestSubclassConversion(form)
        self.failUnless(isinstance(result, Control))


    def testNullArrayConversion(self):
        """Test null array conversion in method call."""
        from CLR.System import Type

        object = MethodTest()
        r = object.TestNullArrayConversion(None)
        self.failUnless(r == None)


    def testStringOutParams(self):
        """Test use of string out-parameters."""
        result = MethodTest.TestStringOutParams("hi", "there")
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringOutParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testStringRefParams(self):
        """Test use of string byref parameters."""
        result = MethodTest.TestStringRefParams("hi", "there")
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")

        result = MethodTest.TestStringRefParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == "output string")


    def testValueOutParams(self):
        """Test use of value type out-parameters."""
        result = MethodTest.TestValueOutParams("hi", 1)
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
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
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(result[1] == 42)

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like int, long, etc.
        self.failUnlessRaises(TypeError, test)


    def testObjectOutParams(self):
        """Test use of object out-parameters."""
        import CLR
        result = MethodTest.TestObjectOutParams("hi", MethodTest())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Exception))

        result = MethodTest.TestObjectOutParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Exception))


    def testObjectRefParams(self):
        """Test use of object byref parameters."""
        import CLR
        result = MethodTest.TestObjectRefParams("hi", MethodTest())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Exception))

        result = MethodTest.TestObjectRefParams("hi", None)
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Exception))


    def testStructOutParams(self):
        """Test use of struct out-parameters."""
        import CLR
        result = MethodTest.TestStructOutParams("hi",CLR.System.Guid.NewGuid())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.failUnlessRaises(TypeError, test)


    def testStructRefParams(self):
        """Test use of struct byref parameters."""
        import CLR
        result = MethodTest.TestStructRefParams("hi",CLR.System.Guid.NewGuid())
        self.failUnless(type(result) == type(()))
        self.failUnless(len(result) == 2)
        self.failUnless(result[0] == True)
        self.failUnless(isinstance(result[1], CLR.System.Guid))

        def test():
            MethodTest.TestValueRefParams("hi", None)

        # None cannot be converted to a value type like a struct
        self.failUnlessRaises(TypeError, test)


    def testVoidSingleOutParam(self):
        """Test void method with single out-parameter."""
        import CLR
        result = MethodTest.TestVoidSingleOutParam(9)
        self.failUnless(result == 42)

        def test():
            MethodTest.TestVoidSingleOutParam(None)

        # None cannot be converted to a value type
        self.failUnlessRaises(TypeError, test)


    def testVoidSingleRefParam(self):
        """Test void method with single ref-parameter."""
        import CLR
        result = MethodTest.TestVoidSingleRefParam(9)
        self.failUnless(result == 42)

        def test():
            MethodTest.TestVoidSingleRefParam(None)

        # None cannot be converted to a value type
        self.failUnlessRaises(TypeError, test)




def test_suite():
    return unittest.makeSuite(MethodTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    testcase.setup()
    main()

