# -*- coding: utf-8 -*-

"""Test if interop with recursive type inheritance works."""


def test_recursive_type_creation():
    """Test that a recursive types don't crash with a
    StackOverflowException"""
    from Python.Test import RecursiveInheritance

    test_instance = RecursiveInheritance.SubClass()
    test_instance.SomeMethod()
