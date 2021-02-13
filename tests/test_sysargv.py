"""Test sys.argv state."""

import sys
from subprocess import check_output
from ast import literal_eval


def test_sys_argv_state(filepath):
    """Test sys.argv state doesn't change after clr import.
    To better control the arguments being passed, test on a fresh python
    instance with specific arguments"""

    script = filepath("argv-fixture.py")
    out = check_output([sys.executable, script, "foo", "bar"])
    out = literal_eval(out.decode("ascii"))
    assert out[-2:] == ["foo", "bar"]
