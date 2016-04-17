"""
Run all of the unit tests for this package multiple times in a highly
multithreaded way to stress the system. This makes it possible to look
for memory leaks and threading issues and provides a good target for a
profiler to accumulate better data.
"""
from __future__ import print_function

import sys, os, gc, time, threading, thread


class StressTest:
    def __init__(self):
        self.dirname = os.path.split(__file__)[0]
        sys.path.append(self.dirname)
        gc.set_debug(gc.DEBUG_LEAK)
        import runtests
        self.module = runtests
        self.done = []

    def dprint(self, msg):
        # Debugging helper to trace thread-related tests.
        if 1: print(msg)

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
        self.dprint("thread %s starting..." % thread_id)
        time.sleep(0.1)
        for i in range(iterations):
            self.dprint("thread %s iter %d start" % (thread_id, i))
            self.module.main()
            self.dprint("thread %s iter %d end" % (thread_id, i))
        self.done.append(None)
        self.dprint("thread %s done" % thread_id)

    def stressTest(self, iterations=1, threads=1):
        args = (iterations,)
        self.markStart()
        for i in range(threads):
            thread = threading.Thread(target=self.runThread, args=args)
            thread.start()
        while len(self.done) < (iterations * threads):
            self.dprint(len(self.done))
            time.sleep(0.1)
        self.markFinish()
        took = self.elapsed()
        self.printGCReport()


def main():
    test = StressTest()
    test.stressTest(2, 10)


if __name__ == '__main__':
    main()
    sys.exit(0)
