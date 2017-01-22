#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""Run all of the unit tests for this package."""

from __future__ import print_function

import os
import sys
import unittest

from _compat import input

try:
    import System
except ImportError:
    print("Load clr import hook")
    import clr

    clr.AddReference("Python.Test")
    clr.AddReference("System.Collections")
    clr.AddReference("System.Data")
    clr.AddReference("System.Management")

test_modules = (
    # has to be first test before other module import clr
	'test_sysargv',
	
    # test_module passes on its own, but not here if
    # other test modules that import System.Windows.Forms
    # run first. They must not do module level import/AddReference()
    # of the System.Windows.Forms namespace.
    'test_module',

    'test_suite',
    'test_event',
    'test_constructors',
    'test_enum',
    'test_method',
    'test_exceptions',
    'test_compat',
    'test_generic',
    'test_conversion',
    'test_class',
    'test_interface',
    'test_field',
    'test_property',
    'test_indexer',
    'test_delegate',
    'test_array',
    'test_thread',
    'test_docstring',

    # FIXME: Has tests that are being skipped.
    'test_engine',

    # FIXME: Has tests that are being skipped.
    'test_subclass',
)


def remove_pyc():
    path = os.path.dirname(os.path.abspath(__file__))
    for name in test_modules:
        pyc = os.path.join(path, "{0}.pyc".format(name))
        if os.path.isfile(pyc):
            os.unlink(pyc)


def main(verbosity=1):
    remove_pyc()

    suite = unittest.TestSuite()

    for name in test_modules:
        module = __import__(name)
        suite.addTests((module.test_suite(),))

    result = unittest.TextTestRunner(verbosity=verbosity).run(suite)
    if not result.wasSuccessful():
        raise Exception("Tests failed")


if __name__ == '__main__':
    main()
    if '--pause' in sys.argv:
        print("Press enter to continue")
        input()
