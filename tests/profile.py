#!/usr/bin/env python
# -*- coding: utf-8 -*-
# FIXME: FAIL: testImplicitAssemblyLoad AssertionError: 0 != 1

"""Run all of the unit tests for this package over and over,
   in order to provide for better profiling.
"""

from __future__ import print_function

import gc
import os
import sys
import time

import runtests
from ._compat import range


def main():
    dirname = os.path.split(__file__)
    sys.path.append(dirname)

    gc.set_debug(gc.DEBUG_LEAK)

    start = time.clock()

    for i in range(50):
        print('iteration: {0:d}'.format(i))
        runtests.main()

    stop = time.clock()
    took = str(stop - start)
    print('Total Time: {0}'.format(took))

    for item in gc.get_objects():
        print(item, sys.getrefcount(item))


if __name__ == '__main__':
    main()
