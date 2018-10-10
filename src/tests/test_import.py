# -*- coding: utf-8 -*-

"""Test the import statement."""

import pytest
import sys

def test_relative_missing_import():
    """Test that a relative missing import doesn't crash.
    Some modules use this to check if a package is installed.
    Relative import in the site-packages folder"""
    with pytest.raises(ImportError):
        from . import _missing_import


def test_import_all_on_second_time():
    from . import importtest
    del sys.modules[importtest.__name__]
    
