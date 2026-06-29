# -*- coding: utf-8 -*-

import pytest
from System.Collections.Generic import Dictionary
from System.Dynamic import ExpandoObject

from Python.Test import DynamicMappingObject
from Python.Test import RejectingDeleteDynamicObject
from Python.Test import RejectingSetDynamicObject
from Python.Test import ThrowingDeleteDynamicObject
from Python.Test import ThrowingGetDynamicObject
from Python.Test import ThrowingSetDynamicObject


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
    """Dynamic-only members should use DLR get/set/modify/delete end-to-end."""
    obj = DynamicMappingObject()
    assert not hasattr(obj, "DynamicOnly")

    # Initial set should create a dynamic member
    obj.DynamicOnly = "initial"
    assert obj.DynamicOnly == "initial"

    # Modify flows through TrySetMember
    obj.DynamicOnly = "updated"
    assert obj.DynamicOnly == "updated"

    # Setting None keeps a present member with None value
    obj.DynamicOnly = None
    assert obj.DynamicOnly is None

    # Delete flows through TryDeleteMember
    del obj.DynamicOnly
    assert "DynamicOnly" not in dir(obj)
    assert not hasattr(obj, "DynamicOnly")


def test_dynamic_set_none_updates_managed_store_after_get():
    """Regression: get->set(None)->get must route through DLR and update managed storage."""
    obj = DynamicMappingObject()
    obj.SetDynamicValue("MyProp", "initial")

    x = obj.MyProp
    assert x == "initial"

    obj.MyProp = None

    y = obj.MyProp
    assert y is None
    assert obj.GetDynamicValue("MyProp") is None


@pytest.mark.parametrize("obj", [DynamicMappingObject(), ExpandoObject()])
def test_dynamic_member_lifecycle(obj):
    """Dynamic members should support set/modify/get/delete via the DLR binder."""
    name = "LifecycleMember"

    assert not hasattr(obj, name)

    setattr(obj, name, 1)
    assert getattr(obj, name) == 1

    setattr(obj, name, 2)
    assert getattr(obj, name) == 2

    delattr(obj, name)
    assert not hasattr(obj, name)


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


def test_trysetmember_false_raises_attributeerror_instead_of_silent_python_setattr():
    obj = RejectingSetDynamicObject()

    with pytest.raises(AttributeError):
        obj.typoed_name = 42

    assert not hasattr(obj, "typoed_name")


def test_trygetmember_exception_is_raised_in_python():
    obj = ThrowingGetDynamicObject()
    obj.AddDynamicMember("any_key", 1)

    with pytest.raises(Exception, match="TryGetMember failed for 'any_key'"):
        _ = obj.any_key


def test_trysetmember_exception_is_raised_in_python():
    obj = ThrowingSetDynamicObject()

    with pytest.raises(Exception, match="TrySetMember failed for 'bad_name'"):
        obj.bad_name = 42


def test_trydeletemember_false_raises_attributeerror():
    obj = RejectingDeleteDynamicObject()
    obj.AddDynamicMember("existing_name", 42)

    with pytest.raises(AttributeError):
        del obj.missing_name


def test_trydeletemember_exception_is_raised_in_python():
    obj = ThrowingDeleteDynamicObject()
    obj.bad_name = 42

    with pytest.raises(Exception, match="TryDeleteMember failed for 'bad_name'"):
        del obj.bad_name