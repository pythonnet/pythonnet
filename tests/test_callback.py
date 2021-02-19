# -*- coding: utf-8 -*-

"""Test that callbacks from C# into python work."""


def simpleDefaultArg(arg='test'):
    return arg


def test_default_for_null():
    """Test that C# can use null for an optional python argument"""
    from Python.Test import CallbackTest

    test_instance = CallbackTest()
    ret_val = test_instance.Call_simpleDefaultArg_WithNull(__name__)
    python_ret_val = simpleDefaultArg(None)
    assert ret_val == python_ret_val


def test_default_for_none():
    """Test that C# can use no argument for an optional python argument"""
    from Python.Test import CallbackTest

    test_instance = CallbackTest()
    ret_val = test_instance.Call_simpleDefaultArg_WithEmptyArgs(__name__)
    python_ret_val = simpleDefaultArg()
    assert ret_val == python_ret_val
