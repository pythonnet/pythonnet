# -*- coding: utf-8 -*-

import unittest

import Python.Test as Test

from _compat import DictProxyType, long


class EnumTests(unittest.TestCase):
    """Test CLR enum support."""

    def test_enum_standard_attrs(self):
        """Test standard enum attributes."""
        from System import DayOfWeek

        self.assertTrue(DayOfWeek.__name__ == 'DayOfWeek')
        self.assertTrue(DayOfWeek.__module__ == 'System')
        self.assertTrue(isinstance(DayOfWeek.__dict__, DictProxyType))
        self.assertTrue(DayOfWeek.__doc__ is None)

    def test_enum_get_member(self):
        """Test access to enum members."""
        from System import DayOfWeek

        self.assertTrue(DayOfWeek.Sunday == 0)
        self.assertTrue(DayOfWeek.Monday == 1)
        self.assertTrue(DayOfWeek.Tuesday == 2)
        self.assertTrue(DayOfWeek.Wednesday == 3)
        self.assertTrue(DayOfWeek.Thursday == 4)
        self.assertTrue(DayOfWeek.Friday == 5)
        self.assertTrue(DayOfWeek.Saturday == 6)

    def test_byte_enum(self):
        """Test byte enum."""
        self.assertTrue(Test.ByteEnum.Zero == 0)
        self.assertTrue(Test.ByteEnum.One == 1)
        self.assertTrue(Test.ByteEnum.Two == 2)

    def test_sbyte_enum(self):
        """Test sbyte enum."""
        self.assertTrue(Test.SByteEnum.Zero == 0)
        self.assertTrue(Test.SByteEnum.One == 1)
        self.assertTrue(Test.SByteEnum.Two == 2)

    def test_short_enum(self):
        """Test short enum."""
        self.assertTrue(Test.ShortEnum.Zero == 0)
        self.assertTrue(Test.ShortEnum.One == 1)
        self.assertTrue(Test.ShortEnum.Two == 2)

    def test_ushort_enum(self):
        """Test ushort enum."""
        self.assertTrue(Test.UShortEnum.Zero == 0)
        self.assertTrue(Test.UShortEnum.One == 1)
        self.assertTrue(Test.UShortEnum.Two == 2)

    def test_int_enum(self):
        """Test int enum."""
        self.assertTrue(Test.IntEnum.Zero == 0)
        self.assertTrue(Test.IntEnum.One == 1)
        self.assertTrue(Test.IntEnum.Two == 2)

    def test_uint_enum(self):
        """Test uint enum."""
        self.assertTrue(Test.UIntEnum.Zero == long(0))
        self.assertTrue(Test.UIntEnum.One == long(1))
        self.assertTrue(Test.UIntEnum.Two == long(2))

    def test_long_enum(self):
        """Test long enum."""
        self.assertTrue(Test.LongEnum.Zero == long(0))
        self.assertTrue(Test.LongEnum.One == long(1))
        self.assertTrue(Test.LongEnum.Two == long(2))

    def test_ulong_enum(self):
        """Test ulong enum."""
        self.assertTrue(Test.ULongEnum.Zero == long(0))
        self.assertTrue(Test.ULongEnum.One == long(1))
        self.assertTrue(Test.ULongEnum.Two == long(2))

    def test_instantiate_enum_fails(self):
        """Test that instantiation of an enum class fails."""
        from System import DayOfWeek

        with self.assertRaises(TypeError):
            _ = DayOfWeek()

    def test_subclass_enum_fails(self):
        """Test that subclassing of an enumeration fails."""
        from System import DayOfWeek

        with self.assertRaises(TypeError):
            class Boom(DayOfWeek):
                pass
            _ = Boom

    def test_enum_set_member_fails(self):
        """Test that setattr operations on enumerations fail."""
        from System import DayOfWeek

        with self.assertRaises(TypeError):
            DayOfWeek.Sunday = 13

        with self.assertRaises(TypeError):
            del DayOfWeek.Sunday

    def test_enum_with_flags_attr_conversion(self):
        """Test enumeration conversion with FlagsAttribute set."""
        # This works because the FlagsField enum has FlagsAttribute.
        Test.FieldTest().FlagsField = 99

        # This should fail because our test enum doesn't have it.
        with self.assertRaises(ValueError):
            Test.FieldTest().EnumField = 99

    def test_enum_conversion(self):
        """Test enumeration conversion."""
        ob = Test.FieldTest()
        self.assertTrue(ob.EnumField == 0)

        ob.EnumField = Test.ShortEnum.One
        self.assertTrue(ob.EnumField == 1)

        with self.assertRaises(ValueError):
            Test.FieldTest().EnumField = 20

        with self.assertRaises(OverflowError):
            Test.FieldTest().EnumField = 100000

        with self.assertRaises(TypeError):
            Test.FieldTest().EnumField = "str"


def test_suite():
    return unittest.makeSuite(EnumTests)
