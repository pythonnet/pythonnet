# -*- coding: utf-8 -*-

import sys
import unittest

import System
from Python.Runtime import PythonEngine


class EngineTests(unittest.TestCase):
    """Test PythonEngine embedding APIs."""

    def test_multiple_calls_to_initialize(self):
        """Test that multiple initialize calls are harmless."""
        try:
            PythonEngine.Initialize()
            PythonEngine.Initialize()
            PythonEngine.Initialize()
        except BaseException:
            self.fail("Initialize() raise an exception.")

    @unittest.skip(reason="FIXME: test crashes")
    def test_import_module(self):
        """Test module import."""
        m = PythonEngine.ImportModule("sys")
        n = m.GetAttr("__name__")
        self.assertTrue(n.AsManagedObject(System.String) == "sys")

    @unittest.skip(reason="FIXME: test freezes")
    def test_run_string(self):
        """Test the RunString method."""
        PythonEngine.AcquireLock()

        code = "import sys; sys.singleline_worked = 1"
        PythonEngine.RunString(code)
        self.assertTrue(sys.singleline_worked == 1)

        code = "import sys\nsys.multiline_worked = 1"
        PythonEngine.RunString(code)
        self.assertTrue(sys.multiline_worked == 1)

        PythonEngine.ReleaseLock()


def test_suite():
    return unittest.makeSuite(EngineTests)
