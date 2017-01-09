import sys, os, string, unittest, types
import clr

clr.AddReference("Python.Test")
import Python.Test as Test
import System


class ConstructorTests(unittest.TestCase):
    """Test CLR class constructor support."""

    def testEnumConstructor(self):
        """Test enum constructor args"""
        from System import TypeCode
        from Python.Test import EnumConstructorTest

        ob = EnumConstructorTest(TypeCode.Int32)
        self.assertTrue(ob.value == TypeCode.Int32)

    def testFlagsConstructor(self):
        """Test flags constructor args"""
        from Python.Test import FlagsConstructorTest
        from System.IO import FileAccess

        flags = FileAccess.Read | FileAccess.Write
        ob = FlagsConstructorTest(flags)
        self.assertTrue(ob.value == flags)

    def testStructConstructor(self):
        """Test struct constructor args"""
        from System import Guid
        from Python.Test import StructConstructorTest

        guid = Guid.NewGuid()
        ob = StructConstructorTest(guid)
        self.assertTrue(ob.value == guid)

    def testSubclassConstructor(self):
        """Test subclass constructor args"""
        from Python.Test import SubclassConstructorTest

        class sub(System.Exception):
            pass

        instance = sub()
        ob = SubclassConstructorTest(instance)
        self.assertTrue(isinstance(ob.value, System.Exception))


def test_suite():
    return unittest.makeSuite(ConstructorTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
