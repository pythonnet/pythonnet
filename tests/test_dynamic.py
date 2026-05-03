# -*- coding: utf-8 -*-

import pytest
from System.Collections.Generic import Dictionary
from System.Dynamic import ExpandoObject

from Python.Test import DynamicMappingObject


def _mro_names(obj):
    return [f"{t.__module__}.{t.__name__}" for t in type(obj).__mro__]


@pytest.mark.parametrize(
    "obj, expected",
    [
        (DynamicMappingObject(), True),
        (ExpandoObject(), True),
        (Dictionary[str, int](), False),
    ],
)
def test_dlr_mixin_presence(obj, expected):
    has_mixin = "clr._extras.dlr.DynamicMetaObjectProviderMixin" in _mro_names(obj)
    assert has_mixin is expected


@pytest.mark.parametrize("obj", [DynamicMappingObject(), ExpandoObject()])
def test_dynamic_binder(obj):
    assert "answer" not in dir(obj)
    assert "wrong_answer" not in dir(obj)
	
    setattr(obj, "answer", 42)
    obj.wrong_answer = 54

    assert obj.answer == 42
    assert obj.wrong_answer == 54

    assert "answer" in dir(obj)
    assert "wrong_answer" in dir(obj)


def test_native_members_are_accessible_and_keep_priority():
    obj = DynamicMappingObject()
    setattr(obj, "answer", 42)
    obj.SetDynamicValue("Multiplier", 999)

    # Native field
    assert obj.Label == "default"
    obj.Label = "changed"
    assert obj.Label == "changed"

    # Native property takes precedence over dynamic fallback
    assert obj.Multiplier == 1
    obj.Multiplier = 7
    assert obj.Multiplier == 7

    # Native method
    obj.Multiplier = 3
    assert obj.Multiply(5) == 15

def test_dynamic_and_native_members_coexist():
    obj = DynamicMappingObject()
    setattr(obj, "answer", 42)
    obj.Multiplier = 2
    assert obj.answer == 42
    assert obj.Multiplier == 2
    assert obj.Multiply(10) == 20


@pytest.mark.parametrize("obj", [DynamicMappingObject(), ExpandoObject()])
def test_set_and_get_dynamic_property(obj):
    """Test that setting and getting dynamic properties goes through DLR binder."""
    # Get initial value (should be None for non-existent property)
    assert not hasattr(obj, "MyProp")
    
    # Set a dynamic property to a value
    obj.MyProp = 42
    assert obj.MyProp == 42
    
    # Set to None and verify it stays None through DLR
    obj.MyProp = None
    assert obj.MyProp is None
    
    # Set to another value and verify
    obj.MyProp = "hello"
    assert obj.MyProp == "hello"


def test_update_dynamic_value():
    """Setting from Python must update the backing dynamic store in C#."""
    obj = DynamicMappingObject()
    obj.SetDynamicValue("TestProp", "initial")
    assert obj.TestProp == "initial"

    obj.TestProp = None

    assert obj.TestProp is None
    assert obj.GetDynamicValue("TestProp") is None


def test_derive_from_dynamic_class():
    class MyMappingObject(DynamicMappingObject):
        __namespace__ = "PythonNetTest"

        def __init__(self):
            self._custom = 0

        @property
        def custom_property(self):
            return self._custom
        
        @custom_property.setter
        def custom_property(self, i):
            self._custom += i


    obj = MyMappingObject()
    with pytest.raises(AttributeError):
        x = obj.unknown_property

    assert obj.custom_property == 0

    obj.custom_property = 5
    assert obj.custom_property == 5

    obj.custom_property = 5
    assert obj.custom_property == 10

    obj.other_property = None
    assert obj.other_property is None