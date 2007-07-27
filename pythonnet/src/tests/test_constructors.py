# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

import sys, os, string, unittest, types
import Python.Test as Test
import System


class ConstructorTests(unittest.TestCase):
    """Test CLR class constructor support."""

    def testEnumConstructor(self):
        """Test enum constructor args"""
        from System import TypeCode
        from Python.Test import EnumConstructorTest

        ob = EnumConstructorTest(TypeCode.Int32)
        self.failUnless(ob.value == TypeCode.Int32)


    def testFlagsConstructor(self):
        """Test flags constructor args"""
        from Python.Test import FlagsConstructorTest
        from System.IO import FileAccess
        
        flags = FileAccess.Read | FileAccess.Write
        ob = FlagsConstructorTest(flags)
        self.failUnless(ob.value == flags)


    def testStructConstructor(self):
        """Test struct constructor args"""
        from System import Guid
        from Python.Test import StructConstructorTest

        guid = Guid.NewGuid()
        ob = StructConstructorTest(guid)
        self.failUnless(ob.value == guid)


    def testSubclassConstructor(self):
        """Test subclass constructor args"""
        from Python.Test import SubclassConstructorTest
        from System.Windows.Forms import Form, Control

        class sub(Form):
            pass

        form = sub()
        ob = SubclassConstructorTest(form)
        self.failUnless(isinstance(ob.value, Control))



def test_suite():
    return unittest.makeSuite(ConstructorTests)

def main():
    unittest.TextTestRunner().run(test_suite())

if __name__ == '__main__':
    main()

