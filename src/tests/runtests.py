#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""Run all of the unit tests for this package."""

from __future__ import print_function

import sys
import pytest

from ._compat import input

try:
    import System
except ImportError:
    print("Load clr import hook")
    import clr

    clr.AddReference("Python.Test")
    clr.AddReference("System.Collections")
    clr.AddReference("System.Data")
    clr.AddReference("System.Management")


def main(verbosity=1):
    # test_module passes on its own, but not here if
    # other test modules that import System.Windows.Forms
    # run first. They must not do module level import/AddReference()
    # of the System.Windows.Forms namespace.

    # FIXME: test_engine has tests that are being skipped.
    # FIXME: test_subclass has tests that are being skipped.
    pytest.main()


if __name__ == '__main__':
    main()
    if '--pause' in sys.argv:
        print("Press enter to continue")
        input()
