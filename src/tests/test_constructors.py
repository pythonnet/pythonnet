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


    def testConstructorArgumentMatching(self):
        """ Test that simple type promitions works for int->double """
        from Python.Test import AConstrucorTest, LinkConstructorTest
        a1=AConstrucorTest('a1')
        a2=AConstrucorTest('a2')
        self.assertEqual(a1.name,'a1')
        self.assertEqual(a2.name, 'a2')
        l1=LinkConstructorTest(a1,3000,a2)
        #l2=LinkConstructorTest(a1,3000.0,a2)
        self.assertEqual(l1.a1.name, a1.name)
        self.assertEqual(l1.a2.name, a2.name)
        self.assertAlmostEqual(3000.0,l1.MatchMe)

    def testIntToDoubleConstructorArguments(self):
        from Python.Test import ToDoubleConstructorTest

        o = ToDoubleConstructorTest('a',2,'c')
        self.assertEqual(o.a,'a')
        self.assertAlmostEqual(o.b,2)
        self.assertEqual(o.c,'c')

        o = ToDoubleConstructorTest()

    def testIntToFloatConstructorArguments(self):
        from Python.Test import ToFloatConstructorTest

        o = ToFloatConstructorTest('a',2,'c')
        self.assertEqual(o.a,'a')
        self.assertAlmostEqual(o.b,2)
        self.assertEqual(o.c,'c')

        o = ToFloatConstructorTest()

def test_suite():
    return unittest.makeSuite(ConstructorTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
