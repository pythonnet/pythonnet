import unittest, sys
import clr

this_module = sys.modules[__name__]
clr.AddReference("Python.Test")
class RecursiveTypesTests(unittest.TestCase):
    """Test if interop with recursive type inheritance works."""

    def testRecursiveTypeCreation(self):
        """Test that a recursive types don't crash with a StackOverflowException"""
        import Python.Test as Test
        from Python.Test import RecursiveInheritance
        test_instance = RecursiveInheritance.SubClass()
        test_instance.SomeMethod()
        pass


def test_suite():
    return unittest.makeSuite(RecursiveTypesTests)

