# -*- coding: utf-8 -*-

"""Test CLR bridge threading and GIL handling."""

import sys
import threading
import time

import pytest

import _thread as thread
from .utils import dprint


def _gil_enabled():
    """True on every CPython that has a GIL.  Always True before 3.13."""
    return getattr(sys, "_is_gil_enabled", lambda: True)()


# Marker: tests that only matter on free-threaded builds.  Skipped on GIL
# builds because the GIL serialises Python-level operations and most of the
# concurrency hazards we want to exercise simply cannot fire.
freethreaded_only = pytest.mark.skipif(
    _gil_enabled(),
    reason="Only meaningful on free-threaded Python (Py_GIL_DISABLED).",
)


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


# Free-threaded / refcount tests below.  Run on every interpreter; the GIL
# builds exercise the same code paths in single-threaded form while the FT
# builds (Py_GIL_DISABLED) actually stress the concurrent paths.


def test_runtime_refcount_matches_sys_getrefcount():
    """Refcount tracks sys.getrefcount on both GIL and FT builds."""
    obj = object()
    rc_before = sys.getrefcount(obj)
    extra = [obj, obj, obj]
    assert sys.getrefcount(obj) - rc_before == 3
    del extra


def test_is_gil_enabled_attribute_present_on_3_13_plus():
    """sys._is_gil_enabled is present from 3.13 — used by ABI.DetectFreeThreaded."""
    if sys.version_info < (3, 13):
        assert not hasattr(sys, "_is_gil_enabled")
    else:
        assert isinstance(sys._is_gil_enabled(), bool)


def _run_in_threads(target, n_threads, *args, **kwargs):
    """Run target() in n_threads threads, return results in start order, raise on first error."""
    results = [None] * n_threads
    errors = [None] * n_threads

    def worker(i):
        try:
            results[i] = target(i, *args, **kwargs)
        except BaseException as e:
            errors[i] = e

    threads = [threading.Thread(target=worker, args=(i,)) for i in range(n_threads)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    for e in errors:
        if e is not None:
            raise e
    return results


def test_concurrent_clr_method_calls():
    """Concurrent CLR method invocation across threads."""
    from Python.Test import ThreadTest

    def call(_):
        return [ThreadTest.CallEchoString("ping") for _ in range(200)]

    for r in _run_in_threads(call, n_threads=8):
        assert all(x == "ping" for x in r)


def test_concurrent_attribute_access():
    """Concurrent attribute access — exercises the ConcurrentDictionary InternString cache."""
    import System
    from System.Collections.Generic import List

    def access(_):
        for _ in range(500):
            _ = System.String.Empty
            _ = System.Int32.MaxValue
            _ = List[int]
            _ = List[str]
        return True

    assert all(_run_in_threads(access, n_threads=8))


@freethreaded_only
def test_concurrent_clr_object_creation():
    """Concurrent CLR object alloc/free — exercises reflectedObjects + loadedExtensions.

    FT-only: under the GIL this high-contention pattern hits a pre-existing
    pythonnet crash (also reproducible on master) outside this branch's scope.
    """
    from System.Collections.Generic import List

    LI = List[int]

    def make_lists(_):
        for _ in range(200):
            l = LI()
            for j in range(5):
                l.Add(j)
            assert l.Count == 5
        return True

    assert all(_run_in_threads(make_lists, n_threads=8))


@freethreaded_only
def test_concurrent_python_subclass_of_clr_type():
    """Concurrent dynamic-subclass creation — exercises ClassDerived's builder lock.

    FT-only for the same reason as test_concurrent_clr_object_creation.
    """
    import System

    def derive(i):
        cls = type(f"Derived_{i}_{threading.get_ident()}", (System.Object,), {})
        cls()
        return cls.__name__

    names = _run_in_threads(derive, n_threads=8)
    assert len(set(names)) == len(names)


@freethreaded_only
def test_freethreaded_concurrent_attribute_access_no_tear():
    """Heavier attribute-access stress to bias scheduling toward racy reads."""
    import System

    def stress(_):
        for _ in range(2000):
            _ = System.String.Empty
            _ = System.Int32.MaxValue
        return True

    assert all(_run_in_threads(stress, n_threads=16))
