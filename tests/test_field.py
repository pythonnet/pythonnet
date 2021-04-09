# -*- coding: utf-8 -*-

"""Test CLR field support."""

import System
import pytest
from Python.Test import FieldTest


def test_public_instance_field():
    """Test public instance fields."""
    ob = FieldTest()
    assert ob.PublicField == 0

    ob.PublicField = 1
    assert ob.PublicField == 1

    with pytest.raises(TypeError):
        del FieldTest().PublicField


def test_public_static_field():
    """Test public static fields."""
    ob = FieldTest()
    assert FieldTest.PublicStaticField == 0

    FieldTest.PublicStaticField = 1
    assert FieldTest.PublicStaticField == 1

    assert ob.PublicStaticField == 1
    ob.PublicStaticField = 0
    assert ob.PublicStaticField == 0

    with pytest.raises(TypeError):
        del FieldTest.PublicStaticField

    with pytest.raises(TypeError):
        del FieldTest().PublicStaticField


def test_protected_instance_field():
    """Test protected instance fields."""
    ob = FieldTest()
    assert ob.ProtectedField == 0

    ob.ProtectedField = 1
    assert ob.ProtectedField == 1

    with pytest.raises(TypeError):
        del FieldTest().ProtectedField


def test_protected_static_field():
    """Test protected static fields."""
    ob = FieldTest()
    assert FieldTest.ProtectedStaticField == 0

    FieldTest.ProtectedStaticField = 1
    assert FieldTest.ProtectedStaticField == 1

    assert ob.ProtectedStaticField == 1
    ob.ProtectedStaticField = 0
    assert ob.ProtectedStaticField == 0

    with pytest.raises(TypeError):
        del FieldTest.ProtectedStaticField

    with pytest.raises(TypeError):
        del FieldTest().ProtectedStaticField


def test_read_only_instance_field():
    """Test readonly instance fields."""
    assert FieldTest().ReadOnlyField == 0

    with pytest.raises(TypeError):
        FieldTest().ReadOnlyField = 1

    with pytest.raises(TypeError):
        del FieldTest().ReadOnlyField


def test_read_only_static_field():
    """Test readonly static fields."""
    ob = FieldTest()

    assert FieldTest.ReadOnlyStaticField == 0
    assert ob.ReadOnlyStaticField == 0

    with pytest.raises(TypeError):
        FieldTest.ReadOnlyStaticField = 1

    with pytest.raises(TypeError):
        FieldTest().ReadOnlyStaticField = 1

    with pytest.raises(TypeError):
        del FieldTest.ReadOnlyStaticField

    with pytest.raises(TypeError):
        del FieldTest().ReadOnlyStaticField


def test_constant_field():
    """Test const fields."""
    ob = FieldTest()

    assert FieldTest.ConstField == 0
    assert ob.ConstField == 0

    with pytest.raises(TypeError):
        FieldTest().ConstField = 1

    with pytest.raises(TypeError):
        FieldTest.ConstField = 1

    with pytest.raises(TypeError):
        del FieldTest().ConstField

    with pytest.raises(TypeError):
        del FieldTest.ConstField


def test_internal_field():
    """Test internal fields."""

    with pytest.raises(AttributeError):
        _ = FieldTest().InternalField

    with pytest.raises(AttributeError):
        _ = FieldTest().InternalStaticField

    with pytest.raises(AttributeError):
        _ = FieldTest.InternalStaticField


def test_private_field():
    """Test private fields."""

    with pytest.raises(AttributeError):
        _ = FieldTest().PrivateField

    with pytest.raises(AttributeError):
        _ = FieldTest().PrivateStaticField

    with pytest.raises(AttributeError):
        _ = FieldTest.PrivateStaticField


def test_field_descriptor_get_set():
    """Test field descriptor get / set."""

    # This test ensures that setting an attribute implemented with
    # a descriptor actually goes through the descriptor (rather than
    # silently replacing the descriptor in the instance or type dict.

    ob = FieldTest()

    assert FieldTest.PublicStaticField == 0
    assert ob.PublicStaticField == 0

    descriptor = FieldTest.__dict__['PublicStaticField']
    assert type(descriptor) != int

    ob.PublicStaticField = 0
    descriptor = FieldTest.__dict__['PublicStaticField']
    assert type(descriptor) != int

    FieldTest.PublicStaticField = 0
    descriptor = FieldTest.__dict__['PublicStaticField']
    assert type(descriptor) != int


def test_field_descriptor_wrong_type():
    """Test setting a field using a value of the wrong type."""

    with pytest.raises(TypeError):
        FieldTest().PublicField = "spam"


def test_field_descriptor_abuse():
    """Test field descriptor abuse."""
    desc = FieldTest.__dict__['PublicField']

    with pytest.raises(TypeError):
        desc.__get__(0, 0)

    with pytest.raises(TypeError):
        desc.__set__(0, 0)


def test_boolean_field():
    """Test boolean fields."""
    # change this to true / false later for Python 2.3?
    ob = FieldTest()
    assert ob.BooleanField is False

    ob.BooleanField = True
    assert ob.BooleanField is True

    ob.BooleanField = False
    assert ob.BooleanField is False

    ob.BooleanField = 1
    assert ob.BooleanField is True

    ob.BooleanField = 0
    assert ob.BooleanField is False


def test_sbyte_field():
    """Test sbyte fields."""
    ob = FieldTest()
    assert ob.SByteField == 0

    ob.SByteField = 1
    assert ob.SByteField == 1


def test_byte_field():
    """Test byte fields."""
    ob = FieldTest()
    assert ob.ByteField == 0

    ob.ByteField = 1
    assert ob.ByteField == 1


def test_char_field():
    """Test char fields."""
    ob = FieldTest()
    assert ob.CharField == u'A'
    assert ob.CharField == 'A'

    ob.CharField = 'B'
    assert ob.CharField == u'B'
    assert ob.CharField == 'B'

    ob.CharField = u'C'
    assert ob.CharField == u'C'
    assert ob.CharField == 'C'


def test_int16_field():
    """Test int16 fields."""
    ob = FieldTest()
    assert ob.Int16Field == 0

    ob.Int16Field = 1
    assert ob.Int16Field == 1


def test_int32_field():
    """Test int32 fields."""
    ob = FieldTest()
    assert ob.Int32Field == 0

    ob.Int32Field = 1
    assert ob.Int32Field == 1


def test_int64_field():
    """Test int64 fields."""
    ob = FieldTest()
    assert ob.Int64Field == 0

    ob.Int64Field = 1
    assert ob.Int64Field == 1


def test_uint16_field():
    """Test uint16 fields."""
    ob = FieldTest()
    assert ob.UInt16Field == 0

    ob.UInt16Field = 1
    assert ob.UInt16Field == 1


def test_uint32_field():
    """Test uint32 fields."""
    ob = FieldTest()
    assert ob.UInt32Field == 0

    ob.UInt32Field = 1
    assert ob.UInt32Field == 1


def test_uint64_field():
    """Test uint64 fields."""
    ob = FieldTest()
    assert ob.UInt64Field == 0

    ob.UInt64Field = 1
    assert ob.UInt64Field == 1


def test_single_field():
    """Test single fields."""
    ob = FieldTest()
    assert ob.SingleField == 0.0

    ob.SingleField = 1.1
    assert ob.SingleField == 1.1


def test_double_field():
    """Test double fields."""
    ob = FieldTest()
    assert ob.DoubleField == 0.0

    ob.DoubleField = 1.1
    assert ob.DoubleField == 1.1


def test_decimal_field():
    """Test decimal fields."""
    ob = FieldTest()
    assert ob.DecimalField == System.Decimal(0)

    ob.DecimalField = System.Decimal(1)
    assert ob.DecimalField == System.Decimal(1)


def test_string_field():
    """Test string fields."""
    ob = FieldTest()
    assert ob.StringField == "spam"

    ob.StringField = "eggs"
    assert ob.StringField == "eggs"


def test_interface_field():
    """Test interface fields."""
    from Python.Test import Spam, ISpam

    ob = FieldTest()

    assert ISpam(ob.SpamField).GetValue() == "spam"
    assert ob.SpamField.GetValue() == "spam"

    ob.SpamField = Spam("eggs")
    assert ISpam(ob.SpamField).GetValue() == "eggs"
    assert ob.SpamField.GetValue() == "eggs"


def test_object_field():
    """Test ob fields."""
    ob = FieldTest()
    assert ob.ObjectField is None

    ob.ObjectField = System.String("spam")
    assert ob.ObjectField == "spam"

    ob.ObjectField = System.Int32(1)
    assert ob.ObjectField == 1

    ob.ObjectField = None
    assert ob.ObjectField is None


def test_enum_field():
    """Test enum fields."""
    from Python.Test import ShortEnum

    ob = FieldTest()
    assert ob.EnumField == ShortEnum.Zero

    ob.EnumField = ShortEnum.One
    assert ob.EnumField == ShortEnum.One


def test_nullable_field():
    """Test nullable fields."""
    ob = FieldTest()

    ob.StringField = None
    assert ob.StringField is None

    ob.ObjectField = None
    assert ob.ObjectField is None

    ob.SpamField = None
    assert ob.SpamField is None

    # Primitive types and enums should not be set to null.

    with pytest.raises(TypeError):
        FieldTest().Int32Field = None

    with pytest.raises(TypeError):
        FieldTest().EnumField = None
