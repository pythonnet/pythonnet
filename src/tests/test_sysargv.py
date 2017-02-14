# -*- coding: utf-8 -*-

"""Test sys.argv state."""

import sys


def test_sys_argv_state():
    """Test sys.argv state doesn't change after clr import."""
    argv = sys.argv
    import clr
    assert argv == sys.argv
