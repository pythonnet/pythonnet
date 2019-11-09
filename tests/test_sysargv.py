# -*- coding: utf-8 -*-

"""Test sys.argv state."""

import sys

from ._compat import check_output


def test_sys_argv_state(filepath):
    """Test sys.argv state doesn't change after clr import.
    To better control the arguments being passed, test on a fresh python
    instance with specific arguments"""

    script = filepath("argv-fixture.py")
    out = check_output([sys.executable, script, "foo", "bar"])
    assert "foo" in out
    assert "bar" in out
