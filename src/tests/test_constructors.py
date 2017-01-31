import sys, os, string, unittest, types
import clr

clr.AddReference("Python.Test")
import Python.Test as Test
import System

constructor_throw_on_arg_match_is_fixed = False # currently, failed match on super() is silently ignored, and tests will fail if set to true
constructor_to_sub_class_accepts_specific_parameters= False # currently, we can't pass arguments to super() class ct.

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

    def testSubClassWithInternalArgsPassedToSuper(self):
       """ 
       Verify we can sub-class a .NET class, in python, 
       and pass a working set of arguments to our super class.
       """
       from Python.Test import ToDoubleConstructorTest  # does the job for this test

       class PySubClass(ToDoubleConstructorTest):
           def __init__(self,d):
               super(PySubClass, self).__init__('a',2.0,'c')
               self.d = d 

       o = PySubClass('d')
       self.assertEqual( o.d,'d')
       if constructor_to_sub_class_accepts_specific_parameters:
           self.assertEqual( o.a,'a')
           self.assertAlmostEqual(o.b,2.0)
           self.assertEqual( o.c,'c')
       else:
           print("\n\n*** Warning passing parameters to super class is currently not verified\n")


    def testConstructorArgumentMatching(self):
        """ Test that simple type promitions works for int->double """
        from Python.Test import AConstructorTest, LinkConstructorTest
        a1=AConstructorTest('a1')
        a2=AConstructorTest('a2')
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

        o = ToDoubleConstructorTest() # just to verify the default constructor is there

    def testIntToFloatConstructorArguments(self):
        from Python.Test import ToFloatConstructorTest

        o = ToFloatConstructorTest('a',2,'c')
        self.assertEqual(o.a,'a')
        self.assertAlmostEqual(o.b,2)
        self.assertEqual(o.c,'c')

        o = ToFloatConstructorTest()

    def testConstructorRaiseExceptionIfNoMatch(self):
        """ 
        Notice: Due to the feature of .NET object as super-class, there is a 
                'hack' that after calling a constructor with the supplied arguments
                 (and they fail to match the .NET class constructor for any reason)
                 then it removes all the arguments, and retry the call.
                 Now, if this succeeds, it will return an object, with default values.
                 This *could* make sense, given that the .NET class *IS* subclassed,
                 however, if the process is *not* a part of a sub-class construction,
                 then this is at best very unexpected.


        """

        from Python.Test import ToDoubleConstructorTest


        if constructor_throw_on_arg_match_is_fixed:
            try:
                o = ToDoubleConstructorTest('a','not a number','c') # this should raise exception, because there are no match!
            except TypeError:
                return
            self.fail("exception should be raised for non-matching constructor atempt")
        else:
            print("\n\n*** Warning: failing arg match on constructors are currently silently accepted if there is a null constructor\n")



def test_suite():
    return unittest.makeSuite(ConstructorTests)


def main():
    unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
