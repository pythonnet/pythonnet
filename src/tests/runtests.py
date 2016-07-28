"""Run all of the unit tests for this package."""

import os
import sys
import unittest
import warnfilter

warnfilter.addClrWarnfilter()

try:
    import System
except ImportError:
    print("Load clr import hook")
    import clr

test_modules = (
    'test_module',  # Passes on its own, but not here if
    # other test modules that import System.Windows.Forms
    # run first. They must not do module level import/AddReference()
    # of the System.Windows.Forms namespace.
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
    'test_thread'
)


def removePyc():
    path = os.path.dirname(os.path.abspath(__file__))
    for name in test_modules:
        pyc = os.path.join(path, "%s.pyc" % name)
        if os.path.isfile(pyc):
            os.unlink(pyc)


def main(verbosity=1):
    removePyc()

    suite = unittest.TestSuite()

    for name in test_modules:
        module = __import__(name)
        suite.addTests((module.test_suite(),))

    result = unittest.TextTestRunner(verbosity=verbosity).run(suite)
    if not result.wasSuccessful():
        raise Exception("Tests failed")


if __name__ == '__main__':
    main(1)
    if '--pause' in sys.argv:
        print("Press enter to continue")
        raw_input()
