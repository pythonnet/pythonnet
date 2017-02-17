# -*- coding: utf-8 -*-
# TODO: move tests one out of src to project root.
# TODO: travis has numpy on their workers. Maybe add tests?

"""Helpers for testing."""

import ctypes
import sys
import sysconfig

import clr

# Add path for Python.Test & Add References
sys.path.append('C:/testdir/')
clr.AddReference("Python.Test")
clr.AddReference("System.Collections")
clr.AddReference("System.Data")


def pytest_report_header(config):
    """Generate extra report headers"""
    # FIXME: https://github.com/pytest-dev/pytest/issues/2257
    is_64bits = sys.maxsize > 2**32
    arch = "x64" if is_64bits else "x86"
    ucs = ctypes.sizeof(ctypes.c_wchar)
    libdir = sysconfig.get_config_var("LIBDIR")
    shared = bool(sysconfig.get_config_var("Py_ENABLE_SHARED"))

    header = ("Arch: {arch}, UCS: {ucs}, LIBDIR: {libdir}, "
              "Py_ENABLE_SHARED: {shared}".format(**locals()))
    return header
