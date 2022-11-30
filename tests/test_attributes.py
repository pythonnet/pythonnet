# -*- coding: utf-8 -*-
# TODO: Add tests for ClassicClass, NewStyleClass?

"""Test CLR class support."""

import clr
import Python.Test as Test
from Python.Test import TestAttribute, TestAttributeAttribute, IntEnum
import System
import pytest

from .utils import DictProxyType
def test_class_attributes():
    class TestObjectWithAttr(System.Object):
        __namespace__ = "test_clr_attributes"
        __clr_attributes__ = [TestAttribute(1, "2", Arg3 = 3, Arg4 = IntEnum.Four)]
    class TestObjectWithAttr2(System.Object):
            __namespace__ = "test_clr_attributes"
            __clr_attributes__ = [TestAttribute(Arg3 = 3, Arg4 = IntEnum.Four)]
    v = TestObjectWithAttr()
    v2 = TestObjectWithAttr2()
    TestAttributeAttribute.Verify(v)
    TestAttributeAttribute.Verify(v2, 0, None)

