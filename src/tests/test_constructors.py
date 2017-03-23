# -*- coding: utf-8 -*-

"""Test CLR class constructor support."""

import System


def test_enum_constructor():
    """Test enum constructor args"""
    from System import TypeCode
    from Python.Test import EnumConstructorTest

    ob = EnumConstructorTest(TypeCode.Int32)
    assert ob.value == TypeCode.Int32


def test_flags_constructor():
    """Test flags constructor args"""
    from Python.Test import FlagsConstructorTest
    from System.IO import FileAccess

    flags = FileAccess.Read | FileAccess.Write
    ob = FlagsConstructorTest(flags)
    assert ob.value == flags


def test_struct_constructor():
    """Test struct constructor args"""
    from System import Guid
    from Python.Test import StructConstructorTest

    guid = Guid.NewGuid()
    ob = StructConstructorTest(guid)
    assert ob.value == guid


def test_subclass_constructor():
    """Test subclass constructor args"""
    from Python.Test import SubclassConstructorTest

    class Sub(System.Exception):
        pass

    instance = Sub()
    ob = SubclassConstructorTest(instance)
    assert isinstance(ob.value, System.Exception)
