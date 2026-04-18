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
