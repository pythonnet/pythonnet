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

from _compat import range, thread


def dprint(msg):
    # Debugging helper to trace thread-related tests.
    if 1:
        print(msg)


class StressTest(object):
    def __init__(self):
        self.dirname = os.path.split(__file__)[0]
        sys.path.append(self.dirname)
        gc.set_debug(gc.DEBUG_LEAK)
        import runtests
        self.module = runtests
        self.done = []

    def markStart(self):
        self._start = time.clock()

    def markFinish(self):
        self._finish = time.clock()

    def elapsed(self):
        return self._finish - self._start

    def printGCReport(self):
        for item in gc.get_objects():
            print(item, sys.getrefcount(item))

    def runThread(self, iterations):
        thread_id = thread.get_ident()
        dprint("thread %s starting..." % thread_id)
        time.sleep(0.1)
        for i in range(iterations):
            dprint("thread %s iter %d start" % (thread_id, i))
            self.module.main()
            dprint("thread %s iter %d end" % (thread_id, i))
        self.done.append(None)
        dprint("thread %s done" % thread_id)

    def stressTest(self, iterations=1, threads=1):
        args = (iterations,)
        self.markStart()
        for _ in range(threads):
            thread = threading.Thread(target=self.runThread, args=args)
            thread.start()
        while len(self.done) < (iterations * threads):
            dprint(len(self.done))
            time.sleep(0.1)
        self.markFinish()
        took = self.elapsed()
        self.printGCReport()


def main():
    test = StressTest()
    test.stressTest(2, 10)


if __name__ == '__main__':
    main()
