Threading
=========

This page explains how Python.NET interacts with the Python Global Interpreter
Lock (GIL) and with managed threads, and what guarantees the runtime makes
when your code is multi-threaded.  It covers both classic CPython builds and
the free-threaded build introduced in CPython 3.13 (``Py_GIL_DISABLED``).

The model in one paragraph
--------------------------

Python.NET embeds CPython, so every interaction with a Python object —
including reading a ``PyObject``'s attributes, calling a Python callable,
constructing a Python value, or letting a ``PyObject`` go out of scope — must
happen while the calling thread is *attached* to the interpreter.  On a
classic (GIL-enabled) CPython build "attached" means "holds the GIL"; on a
free-threaded build it means "has an active thread state".  In both cases the
attachment API is the same: ``Py.GIL()`` on the C# side and
``threading.Thread`` / ``_thread`` on the Python side.  Forgetting to attach
will crash the process or corrupt memory.

Acquiring the GIL from C#
-------------------------

When .NET code calls into Python it must hold the GIL.  Use the ``Py.GIL()``
disposable to acquire and release it::

    using (Py.GIL())
    {
        dynamic np = Py.Import("numpy");
        var arr = np.array(new[] { 1, 2, 3 });
        // ... interact with arr ...
    }

``Py.GIL()`` is re-entrant: nesting calls on the same thread is harmless and
cheap.  Always pair acquisition with disposal — the ``using`` form does this
automatically, and you must release the GIL on the same thread that acquired
it.

If you need a Python object to outlive the ``using`` block, copy what you
need (e.g. ``.As<int[]>()`` or ``new PyObject(value)``) before releasing the
GIL.

Releasing the GIL for long-running .NET work
--------------------------------------------

If a managed call holds the GIL but then does long-running work that does not
touch Python (heavy CPU, blocking I/O, native interop), release the GIL so
other Python threads can run::

    IntPtr threadState = PythonEngine.BeginAllowThreads();
    try
    {
        DoCpuHeavyWork();          // safe: no Python C API calls
    }
    finally
    {
        PythonEngine.EndAllowThreads(threadState);
    }

Inside the ``BeginAllowThreads``/``EndAllowThreads`` block you must not touch
any Python object.  If you need to talk to Python from worker threads spawned
in this region, those threads must acquire the GIL themselves with
``Py.GIL()``.

Calling .NET from Python threads
--------------------------------

Calling a managed method from a Python ``threading.Thread`` works
transparently — Python.NET handles GIL acquisition/release around the
managed call.  The managed code sees the GIL held on entry and is free to
release it via ``BeginAllowThreads`` if it does its own blocking work.

Calling Python from CLR threads
-------------------------------

A CLR thread that was *not* spawned by Python (a thread-pool task, a
``Thread`` started in C#, an ``async`` continuation that resumed on a
different thread, etc.) must acquire the GIL before touching any
``PyObject``::

    Task.Run(() =>
    {
        using (Py.GIL())
        {
            // safe to use PyObjects here
        }
    });

Forgetting this is the most common pythonnet threading bug.  Symptoms range
from immediate segfaults to subtle refcount corruption that crashes much
later.

Reference counting and finalizers
---------------------------------

``PyObject`` follows the .NET ``IDisposable`` pattern.  ``Dispose()`` (or the
end of a ``using`` block) drops the underlying Python reference; the GC
finalizer queues the same release for the next time Python.NET is on the GIL.

Two practical consequences:

* **Don't share a single ``PyObject`` instance across threads without
  serialising access.**  ``PyObject`` is not internally locked.  If multiple
  threads concurrently dispose the same instance, the underlying refcount can
  go negative.

* **Don't rely on the GC finalizer running promptly.**  The PyObject is only
  freed when a Python.NET API later reacquires the GIL.  If your application
  shuts down without that happening, finalizable PyObjects can be reported as
  leaked.

Free-threaded Python (PEP 703)
------------------------------

Starting with the free-threaded CPython 3.13+ build (``Py_GIL_DISABLED``),
the GIL is no longer the serialisation point for Python C API calls.
Python.NET is tested against the ``3.14t`` (free-threaded) interpreter and
behaves as follows under that build:

* ``Py.GIL()`` still acquires a thread state.  It is functionally a no-op
  for mutual exclusion but is still required for thread-state attachment.
  Existing code that uses ``using (Py.GIL())`` continues to work without
  changes.
* ``PythonEngine.BeginAllowThreads`` / ``EndAllowThreads`` similarly
  manage the thread state and are still needed if you want the GC and
  other Python threads to run while you're in long-running unmanaged code.
* Internal Python.NET caches (the reflection cache, generic-type binding
  cache, dynamic-dispatch cache, module attribute cache, the interned-
  string table, etc.) are thread-safe.  You may read and call CLR types
  concurrently from any number of threads without external locking.
* The reference-counting protocol uses CPython's ``Py_REFCNT`` symbol on
  3.14+, which returns the merged biased + shared refcount; values you read
  from ``PyObject.Refcount`` are correct under free-threading.

Behaviour that is *unchanged* between GIL and free-threaded builds:

* A managed object exposed to Python (e.g. via ``System.Object`` or a
  Python subclass of a CLR type) is still owned by a single CLR side: you
  must not mutate its plain CLR fields from multiple threads without your
  own locking.  Python.NET only protects its own bookkeeping, not your
  domain data.
* Operations on a single ``PyObject`` instance still require external
  serialisation — see "Reference counting" above.

Patterns
--------

Concurrent CLR access from Python
"""""""""""""""""""""""""""""""""

Hammering CLR attributes / generic types from many threads is supported::

    from threading import Thread
    import System
    from System.Collections.Generic import List

    def worker():
        for _ in range(1000):
            _ = System.String.Empty
            _ = List[int]()

    threads = [Thread(target=worker) for _ in range(8)]
    for t in threads: t.start()
    for t in threads: t.join()

This works on both GIL and free-threaded builds.

Python callback invoked from a managed thread
"""""""""""""""""""""""""""""""""""""""""""""

If a managed component calls back into a Python delegate from a thread it
spawned, that callback path acquires the GIL internally — you do not need to
add ``Py.GIL()`` around the Python code in the delegate.

Spawning a managed thread from inside ``Py.GIL()``
""""""""""""""""""""""""""""""""""""""""""""""""""

If you start a managed thread while holding the GIL and the thread needs to
call back into Python, release the GIL first so the new thread can acquire
it::

    using (Py.GIL())
    {
        var pyCallback = scope.Get("on_done");
        PythonEngine.BeginAllowThreads();   // let workers acquire the GIL
        try
        {
            // spawn workers, wait for them...
        }
        finally
        {
            PythonEngine.EndAllowThreads(...);
        }
    }

Without the ``BeginAllowThreads`` the spawned thread blocks forever waiting
for the GIL the parent thread is still holding.

Common pitfalls
---------------

* Holding ``Py.GIL()`` across ``Task.Run`` / ``await`` boundaries.  Async
  continuations can resume on a different thread; the GIL handle is
  thread-bound and must be released on the same thread that acquired it.
* Passing a ``PyObject`` to a managed worker without taking ownership.  If
  the producer disposes its handle while the consumer is still using it,
  the worker will operate on a freed object.  Wrap the producer's
  ``PyObject`` with ``new PyObject(value)`` before handing it off, or use
  ``NewReference()``.
* Calling a Python callable that does CPU-bound work without releasing the
  GIL.  Other Python threads cannot make progress in that case, even on a
  free-threaded build where the GIL is otherwise a no-op (the callable
  itself may still touch contended Python state).
