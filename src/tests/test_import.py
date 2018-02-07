# -*- coding: utf-8 -*-

"""Test the import statement."""

import pytest


def test_relative_missing_import():
    """Test that a relative missing import doesn't crash.
    Some modules use this to check if a package is installed.
    Relative import in the site-packages folder"""
    with pytest.raises(ImportError):
        from . import _missing_import


def test_private_and_public_types_import():
    """Tests that importing a type where private and public 
    versions exist imports the public version. In .Net Core 
    2.0 there are public and private versions of the same 
    types in different assemblies. For example, there is a 
    private version of System.Environment in mscorlib.dll, 
    and a public version in System.Runtime.Extensions.dll."""
    from System import Environment
    assert len(Environment.MachineName) > 0
