# -*- coding: utf-8 -*-

"""Test clr enum support."""

import pytest
import Python.Test as Test

from ._compat import DictProxyType, long


def test_enum_standard_attrs():
    """Test standard enum attributes."""
    from System import DayOfWeek

    assert DayOfWeek.__name__ == 'DayOfWeek'
    assert DayOfWeek.__module__ == 'System'
    assert isinstance(DayOfWeek.__dict__, DictProxyType)
    assert DayOfWeek.__doc__ is None


def test_enum_get_member():
    """Test access to enum members."""
    from System import DayOfWeek

    assert DayOfWeek.Sunday == 0
    assert DayOfWeek.Monday == 1
    assert DayOfWeek.Tuesday == 2
    assert DayOfWeek.Wednesday == 3
    assert DayOfWeek.Thursday == 4
    assert DayOfWeek.Friday == 5
    assert DayOfWeek.Saturday == 6


def test_byte_enum():
    """Test byte enum."""
    assert Test.ByteEnum.Zero == 0
    assert Test.ByteEnum.One == 1
    assert Test.ByteEnum.Two == 2


def test_sbyte_enum():
    """Test sbyte enum."""
    assert Test.SByteEnum.Zero == 0
    assert Test.SByteEnum.One == 1
    assert Test.SByteEnum.Two == 2


def test_short_enum():
    """Test short enum."""
    assert Test.ShortEnum.Zero == 0
    assert Test.ShortEnum.One == 1
    assert Test.ShortEnum.Two == 2


def test_ushort_enum():
    """Test ushort enum."""
    assert Test.UShortEnum.Zero == 0
    assert Test.UShortEnum.One == 1
    assert Test.UShortEnum.Two == 2


def test_int_enum():
    """Test int enum."""
    assert Test.IntEnum.Zero == 0
    assert Test.IntEnum.One == 1
    assert Test.IntEnum.Two == 2


def test_uint_enum():
    """Test uint enum."""
    assert Test.UIntEnum.Zero == long(0)
    assert Test.UIntEnum.One == long(1)
    assert Test.UIntEnum.Two == long(2)


def test_long_enum():
    """Test long enum."""
    assert Test.LongEnum.Zero == long(0)
    assert Test.LongEnum.One == long(1)
    assert Test.LongEnum.Two == long(2)


def test_ulong_enum():
    """Test ulong enum."""
    assert Test.ULongEnum.Zero == long(0)
    assert Test.ULongEnum.One == long(1)
    assert Test.ULongEnum.Two == long(2)


def test_instantiate_enum_fails():
    """Test that instantiation of an enum class fails."""
    from System import DayOfWeek

    with pytest.raises(TypeError):
        _ = DayOfWeek()


def test_subclass_enum_fails():
    """Test that subclassing of an enumeration fails."""
    from System import DayOfWeek

    with pytest.raises(TypeError):
        class Boom(DayOfWeek):
            pass

        _ = Boom


def test_enum_set_member_fails():
    """Test that setattr operations on enumerations fail."""
    from System import DayOfWeek

    with pytest.raises(TypeError):
        DayOfWeek.Sunday = 13

    with pytest.raises(TypeError):
        del DayOfWeek.Sunday


def test_enum_with_flags_attr_conversion():
    """Test enumeration conversion with FlagsAttribute set."""
    # This works because the FlagsField enum has FlagsAttribute.
    Test.FieldTest().FlagsField = 99

    # This should fail because our test enum doesn't have it.
    with pytest.raises(ValueError):
        Test.FieldTest().EnumField = 99


def test_enum_conversion():
    """Test enumeration conversion."""
    ob = Test.FieldTest()
    assert ob.EnumField == 0

    ob.EnumField = Test.ShortEnum.One
    assert ob.EnumField == 1

    with pytest.raises(ValueError):
        Test.FieldTest().EnumField = 20

    with pytest.raises(OverflowError):
        Test.FieldTest().EnumField = 100000

    with pytest.raises(TypeError):
        Test.FieldTest().EnumField = "str"
