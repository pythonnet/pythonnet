using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    class RuntimeState
    {
        public static bool ShouldRestoreObjects { get; set; } = false;
        public static bool UseDummyGC { get; set; } = false;

        public static void Save()
        {
            if (!PySys_GetObject("dummy_gc").IsNull)
            {
                throw new Exception("Runtime State set already");
            }

            IntPtr objs = IntPtr.Zero;
            if (ShouldRestoreObjects)
            {
                objs = PySet_New(IntPtr.Zero);
                foreach (var obj in PyGCGetObjects())
                {
                    AddObjPtrToSet(objs, obj);
                }
            }

            var modules = PySet_New(IntPtr.Zero);
            foreach (var name in GetModuleNames())
            {
                int res = PySet_Add(modules, name);
                PythonException.ThrowIfIsNotZero(res);
            }


            var dummyGCHead = PyMem_Malloc(Marshal.SizeOf(typeof(PyGC_Head)));
            unsafe
            {
                var head = (PyGC_Head*)dummyGCHead;
                head->gc.gc_next = dummyGCHead;
                head->gc.gc_prev = dummyGCHead;
                head->gc.gc_refs = IntPtr.Zero;
            }
            {
                var pyDummyGC = PyLong_FromVoidPtr(dummyGCHead);
                int res = PySys_SetObject("dummy_gc", pyDummyGC);
                PythonException.ThrowIfIsNotZero(res);
                XDecref(pyDummyGC);

                try
                {
                    res = PySys_SetObject("initial_modules", modules);
                    PythonException.ThrowIfIsNotZero(res);
                }
                finally
                {
                    XDecref(modules);
                }

                if (ShouldRestoreObjects)
                {
                    AddObjPtrToSet(objs, modules);
                    try
                    {
                        res = PySys_SetObject("initial_objs", objs);
                        PythonException.ThrowIfIsNotZero(res);
                    }
                    finally
                    {
                        XDecref(objs);
                    }
                }
            }
        }

        public static void Restore()
        {
            var dummyGCAddr = PySys_GetObject("dummy_gc").DangerousGetAddress();
            if (dummyGCAddr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Runtime state have not set");
            }
            var dummyGC = PyLong_AsVoidPtr(dummyGCAddr);
            ResotreModules(dummyGC);
            if (ShouldRestoreObjects)
            {
                RestoreObjects(dummyGC);
            }
        }

        private static void ResotreModules(IntPtr dummyGC)
        {
            var intialModules = PySys_GetObject("initial_modules");
            Debug.Assert(!intialModules.IsNull);
            var modules = PyImport_GetModuleDict();
            foreach (var name in GetModuleNames())
            {
                if (PySet_Contains(intialModules.DangerousGetAddress(), name) == 1)
                {
                    continue;
                }
                var module = PyDict_GetItem(modules, name);

                if (UseDummyGC && _PyObject_GC_IS_TRACKED(module))
                {
                    ExchangeGCChain(module, dummyGC);
                }
                if (PyDict_DelItem(modules, name) != 0)
                {
                    PyErr_Print();
                }
            }
        }

        private static void RestoreObjects(IntPtr dummyGC)
        {
            if (!UseDummyGC)
            {
                throw new Exception("To prevent crash by _PyObject_GC_UNTRACK in Python internal, UseDummyGC should be enabled when using ResotreObjects");
            }
            IntPtr intialObjs = PySys_GetObject("initial_objs").DangerousGetAddress();
            Debug.Assert(intialObjs != IntPtr.Zero);
            foreach (var obj in PyGCGetObjects())
            {
                var p = PyLong_FromVoidPtr(obj);
                try
                {
                    if (PySet_Contains(intialObjs, p) == 1)
                    {
                        continue;
                    }
                }
                finally
                {
                    XDecref(p);
                }
                Debug.Assert(_PyObject_GC_IS_TRACKED(obj), "A GC object must be tracked");
                ExchangeGCChain(obj, dummyGC);
            }
        }

        public static IEnumerable<IntPtr> PyGCGetObjects()
        {
            var gc = PyImport_ImportModule("gc");
            PythonException.ThrowIfIsNull(gc);
            var get_objects = PyObject_GetAttrString(gc, "get_objects");
            var objs = PyObject_CallObject(get_objects, IntPtr.Zero);
            var length = PyList_Size(new BorrowedReference(objs));
            for (long i = 0; i < length; i++)
            {
                var obj = PyList_GetItem(new BorrowedReference(objs), i);
                yield return obj.DangerousGetAddress();
            }
            XDecref(objs);
            XDecref(gc);
        }

        public static IEnumerable<IntPtr> GetModuleNames()
        {
            var modules = PyImport_GetModuleDict();
            var names = PyDict_Keys(modules);
            var length = PyList_Size(new BorrowedReference(names));
            for (int i = 0; i < length; i++)
            {
                var name = PyList_GetItem(new BorrowedReference(names), i);
                yield return name.DangerousGetAddress();
            }
            XDecref(names);
        }

        private static void AddObjPtrToSet(IntPtr set, IntPtr obj)
        {
            var p = PyLong_FromVoidPtr(obj);
            XIncref(obj);
            try
            {
                int res = PySet_Add(set, p);
                PythonException.ThrowIfIsNotZero(res);
            }
            finally
            {
                XDecref(p);
            }
        }
        /// <summary>
        /// Exchange gc to a dummy gc prevent nullptr error in _PyObject_GC_UnTrack macro.
        /// </summary>
        private static void ExchangeGCChain(IntPtr obj, IntPtr gc)
        {
            var head = _Py_AS_GC(obj);
            if ((long)_PyGCHead_REFS(head) == _PyGC_REFS_UNTRACKED)
            {
                throw new ArgumentException("GC object untracked");
            }
            unsafe
            {
                var g = (PyGC_Head*)head;
                var newGCGen = (PyGC_Head*)gc;

                ((PyGC_Head*)g->gc.gc_prev)->gc.gc_next = g->gc.gc_next;
                ((PyGC_Head*)g->gc.gc_next)->gc.gc_prev = g->gc.gc_prev;

                g->gc.gc_next = gc;
                g->gc.gc_prev = newGCGen->gc.gc_prev;
                ((PyGC_Head*)g->gc.gc_prev)->gc.gc_next = head;
                newGCGen->gc.gc_prev = head;
            }
        }

        private static IEnumerable<IntPtr> IterGCNodes(IntPtr gc)
        {
            var node = GetNextGCNode(gc);
            while (node != gc)
            {
                var next = GetNextGCNode(node);
                yield return node;
                node = next;
            }
        }

        private static IEnumerable<IntPtr> IterObjects(IntPtr gc)
        {
            foreach (var node in IterGCNodes(gc))
            {
                yield return _Py_FROM_GC(node);
            }
        }

        private static unsafe IntPtr GetNextGCNode(IntPtr node)
        {
            return ((PyGC_Head*)node)->gc.gc_next;
        }
    }
}
