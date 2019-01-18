# -*- coding: utf-8 -*-

"""Test the import statement."""

import pytest


def test_relative_missing_import():
    """Test that a relative missing import doesn't crash.
    Some modules use this to check if a package is installed.
    Relative import in the site-packages folder"""
    with pytest.raises(ImportError):
        from . import _missing_import
