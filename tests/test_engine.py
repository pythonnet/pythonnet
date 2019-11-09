# -*- coding: utf-8 -*-

"""Test PythonEngine embedding APIs."""

import sys

import System
import pytest
from Python.Runtime import PythonEngine


def test_multiple_calls_to_initialize():
    """Test that multiple initialize calls are harmless."""
    try:
        PythonEngine.Initialize()
        PythonEngine.Initialize()
        PythonEngine.Initialize()
    except Exception:
        assert False  # Initialize() raise an exception.


@pytest.mark.skip(reason="FIXME: test crashes")
def test_import_module():
    """Test module import."""
    m = PythonEngine.ImportModule("sys")
    n = m.GetAttr("__name__")
    assert n.AsManagedObject(System.String) == "sys"


@pytest.mark.skip(reason="FIXME: test freezes")
def test_run_string():
    """Test the RunString method."""
    PythonEngine.AcquireLock()

    code = "import sys; sys.singleline_worked = 1"
    PythonEngine.RunString(code)
    assert sys.singleline_worked == 1

    code = "import sys\nsys.multiline_worked = 1"
    PythonEngine.RunString(code)
    assert sys.multiline_worked == 1

    PythonEngine.ReleaseLock()
