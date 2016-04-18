import sys, os, string, unittest, types
from Python.Test import ThreadTest
import six

if six.PY3:
    import _thread as thread
else:
    import thread


def dprint(msg):
    # Debugging helper to trace thread-related tests.
    if 0: print(msg)


class ThreadTests(unittest.TestCase):
    """Test CLR bridge threading and GIL handling."""

    def testSimpleCallbackToPython(self):
        """Test a call to managed code that then calls back into Python."""
        dprint("thread %s SimpleCallBack" % thread.get_ident())
        result = ThreadTest.CallEchoString("spam")
        self.assertTrue(result == "spam")
        dprint("thread %s SimpleCallBack ret" % thread.get_ident())

    def testDoubleCallbackToPython(self):
        """Test a call to managed code that then calls back into Python
           that then calls managed code that then calls Python again."""
        dprint("thread %s DoubleCallBack" % thread.get_ident())
        result = ThreadTest.CallEchoString2("spam")
        self.assertTrue(result == "spam")
        dprint("thread %s DoubleCallBack ret" % thread.get_ident())

    def testPythonThreadCallsToCLR(self):
        """Test calls by Python-spawned threads into managed code."""
        # This test is very likely to hang if something is wrong ;)
        import threading, time
        from System import String

        done = []

        def run_thread():
            for i in range(10):
                time.sleep(0.1)
                dprint("thread %s %d" % (thread.get_ident(), i))
                mstr = String("thread %s %d" % (thread.get_ident(), i))
                pstr = mstr.ToString()
                done.append(None)
                dprint("thread %s %d done" % (thread.get_ident(), i))

        def start_threads(count):
            for i in range(count):
                thread = threading.Thread(target=run_thread)
                thread.start()

        start_threads(5)

        while len(done) < 50:
            dprint(len(done))
            time.sleep(0.1)

        return


def test_suite():
    return unittest.makeSuite(ThreadTests)


def main():
    for i in range(50):
        unittest.TextTestRunner().run(test_suite())


if __name__ == '__main__':
    main()
