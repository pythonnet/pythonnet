# -*- coding: utf-8 -*-

import unittest

import System


class ConstructorTests(unittest.TestCase):
    """Test CLR class constructor support."""

    def test_enum_constructor(self):
        """Test enum constructor args"""
        from System import TypeCode
        from Python.Test import EnumConstructorTest

        ob = EnumConstructorTest(TypeCode.Int32)
        self.assertTrue(ob.value == TypeCode.Int32)

    def test_flags_constructor(self):
        """Test flags constructor args"""
        from Python.Test import FlagsConstructorTest
        from System.IO import FileAccess

        flags = FileAccess.Read | FileAccess.Write
        ob = FlagsConstructorTest(flags)
        self.assertTrue(ob.value == flags)

    def test_struct_constructor(self):
        """Test struct constructor args"""
        from System import Guid
        from Python.Test import StructConstructorTest

        guid = Guid.NewGuid()
        ob = StructConstructorTest(guid)
        self.assertTrue(ob.value == guid)

    def test_subclass_constructor(self):
        """Test subclass constructor args"""
        from Python.Test import SubclassConstructorTest

        class Sub(System.Exception):
            pass

        instance = Sub()
        ob = SubclassConstructorTest(instance)
        self.assertTrue(isinstance(ob.value, System.Exception))


def test_suite():
    return unittest.makeSuite(ConstructorTests)
