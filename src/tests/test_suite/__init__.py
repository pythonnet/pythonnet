# -*- coding: utf-8 -*-

import unittest

from .test_callback import test_suite as callback_tests
from .test_import import test_suite as import_tests
from .test_recursive_types import test_suite as recursive_types_tests


def test_suite():
    suite = unittest.TestSuite()
    suite.addTests((import_tests(),))
    suite.addTests((callback_tests(),))
    suite.addTests((recursive_types_tests(),))
    return suite
