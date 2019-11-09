#!/usr/bin/env python
# -*- coding: utf-8 -*-
# FIXME: FAIL: testImplicitAssemblyLoad AssertionError: 0 != 1

"""
Run all of the unit tests for this package multiple times in a highly
multi-threaded way to stress the system. This makes it possible to look
for memory leaks and threading issues and provides a good target for a
profiler to accumulate better data.
"""

from __future__ import print_function

import gc
import os
import sys
import threading
import time

from ._compat import range, thread
from .utils import dprint


class StressTest(object):
    def __init__(self):
        self.dirname = os.path.split(__file__)[0]
        sys.path.append(self.dirname)
        gc.set_debug(gc.DEBUG_LEAK)
        import runtests
        self.module = runtests
        self.done = []

    def mark_start(self):
        self._start = time.clock()

    def mark_finish(self):
        self._finish = time.clock()

    def elapsed(self):
        return self._finish - self._start

    def print_gc_report(self):
        for item in gc.get_objects():
            print(item, sys.getrefcount(item))

    def run_thread(self, iterations):
        thread_id = thread.get_ident()
        dprint("thread {0} starting...".format(thread_id))
        time.sleep(0.1)
        for i in range(iterations):
            dprint("thread {0} iter {1} start".format(thread_id, i))
            self.module.main()
            dprint("thread {0} iter {1} end".format(thread_id, i))
        self.done.append(None)
        dprint("thread {0} done".format(thread_id))

    def stress_test(self, iterations=1, threads=1):
        args = (iterations,)
        self.mark_start()
        for _ in range(threads):
            thread = threading.Thread(target=self.run_thread, args=args)
            thread.start()
        while len(self.done) < (iterations * threads):
            dprint(len(self.done))
            time.sleep(0.1)
        self.mark_finish()
        took = self.elapsed()
        self.print_gc_report()


def main():
    test = StressTest()
    test.stress_test(2, 10)


if __name__ == '__main__':
    main()
