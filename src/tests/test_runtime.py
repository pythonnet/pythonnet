# -*- coding: utf-8 -*-

"""Test clrmethod and clrproperty support for calling methods and getting/setting python properties from CLR."""

import Python.Test as Test
import System
import pytest
import clr
import Python.Runtime
import threading

def test_pyobject_isiterable_on_list():
    """Tests that Runtime.PyObject_IsIterable is true for lists ."""
    assy=clr.AddReference("Python.Runtime")
    x = []
    ip = System.IntPtr.op_Explicit(System.Int64(id(x)))
    m=assy.GetType("Python.Runtime.Runtime").GetMethod("PyObject_IsIterable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    assert m.Invoke(None, [ip]) == True

def test_pyiter_check_on_list():
    """Tests that Runtime.PyIter_Check is false for lists."""
    assy=clr.AddReference("Python.Runtime")
    x = []
    ip = System.IntPtr.op_Explicit(System.Int64(id(x)))
    m=assy.GetType("Python.Runtime.Runtime").GetMethod("PyIter_Check", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    assert m.Invoke(None, [ip]) == False

def test_pyiter_check_on_listiterator():
    """Tests that Runtime.PyIter_Check is true for list iterators."""
    assy=clr.AddReference("Python.Runtime")
    x = [].__iter__()
    ip = System.IntPtr.op_Explicit(System.Int64(id(x)))
    m=assy.GetType("Python.Runtime.Runtime").GetMethod("PyIter_Check", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    assert m.Invoke(None, [ip]) == True

def test_pyiter_check_on_threadlock():
    """Tests that Runtime.PyIter_Check is false for threading.Lock, which uses a different code path in PyIter_Check."""
    assy=clr.AddReference("Python.Runtime")
    x = threading.Lock()
    ip = System.IntPtr.op_Explicit(System.Int64(id(x)))
    m=assy.GetType("Python.Runtime.Runtime").GetMethod("PyIter_Check", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    assert m.Invoke(None, [ip]) == False

def test_pyobject_isiterable_on_threadlock():
    """Tests that Runtime.PyObject_IsIterable is false for threading.Lock, which uses a different code path in PyObject_IsIterable."""
    assy=clr.AddReference("Python.Runtime")
    x = threading.Lock()
    ip = System.IntPtr.op_Explicit(System.Int64(id(x)))
    m=assy.GetType("Python.Runtime.Runtime").GetMethod("PyObject_IsIterable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    assert m.Invoke(None, [ip]) == False
