# -*- coding: utf-8 -*-

"""Test CLR bridge threading and GIL handling."""

import threading
import time

from ._compat import range, thread
from .utils import dprint


def test_simple_callback_to_python():
    """Test a call to managed code that then calls back into Python."""
    from Python.Test import ThreadTest

    dprint("thread %s SimpleCallBack" % thread.get_ident())
    result = ThreadTest.CallEchoString("spam")
    assert result == "spam"
    dprint("thread %s SimpleCallBack ret" % thread.get_ident())


def test_double_callback_to_python():
    """Test a call to managed code that then calls back into Python
       that then calls managed code that then calls Python again."""
    from Python.Test import ThreadTest

    dprint("thread %s DoubleCallBack" % thread.get_ident())
    result = ThreadTest.CallEchoString2("spam")
    assert result == "spam"
    dprint("thread %s DoubleCallBack ret" % thread.get_ident())


def test_python_thread_calls_to_clr():
    """Test calls by Python-spawned threads into managed code."""
    # This test is very likely to hang if something is wrong ;)
    import System

    done = []

    def run_thread():
        for i in range(10):
            time.sleep(0.1)
            dprint("thread %s %d" % (thread.get_ident(), i))
            mstr = System.String("thread %s %d" % (thread.get_ident(), i))
            dprint(mstr.ToString())
            done.append(None)
            dprint("thread %s %d done" % (thread.get_ident(), i))

    def start_threads(count):
        for _ in range(count):
            thread_ = threading.Thread(target=run_thread)
            thread_.start()

    start_threads(5)

    while len(done) < 50:
        dprint(len(done))
        time.sleep(0.1)
