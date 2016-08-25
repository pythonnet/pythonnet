import unittest

__all__ = ['test_suite']

from .test_import import test_suite as import_tests
from .test_callback import test_suite as callback_tests
from .test_recursiveTypes import test_suite as recursiveTypes_tests

def test_suite():
    suite = unittest.TestSuite()
    suite.addTests((import_tests(),))
    suite.addTests((callback_tests(),))
    suite.addTests((recursiveTypes_tests(),))
    return suite