#!/usr/bin/env python
# -*- coding: utf-8 -*-
# FIXME: FAIL: testImplicitAssemblyLoad AssertionError: 0 != 1

"""Basic stress test."""

from __future__ import print_function

import gc
import time
import unittest
# import pdb

from ._compat import range

try:
    import System
except ImportError:
    print("Load clr import hook")
    import clr
    clr.AddReference("Python.Test")
    clr.AddReference("System.Collections")
    clr.AddReference("System.Data")
    clr.AddReference("System.Management")


def main():
    start = time.clock()

    for i in range(2000):
        print(i)
        for name in (
            'test_module',
            'test_conversion',
            # 'test_class',
            'test_interface',
            'test_enum',
            'test_field',
            'test_property',
            'test_indexer',
            'test_event',
            'test_method',
            # 'test_delegate',
            'test_array',
        ):
            module = __import__(name)
            unittest.TextTestRunner().run(module.test_suite())

    # pdb.set_trace()

    stop = time.clock()
    took = str(stop - start)
    print('Total Time: {0}'.format(took))

    for i in gc.get_objects():
        print(i)


if __name__ == '__main__':
    main()
