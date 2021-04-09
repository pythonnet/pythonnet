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

            NewReference objs = default;
            if (ShouldRestoreObjects)
            {
                objs = PySet_New(default);
                foreach (var objRaw in PyGCGetObjects())
                {
                    AddObjPtrToSet(objs, new BorrowedReference(objRaw));
                }
            }

            var modules = PySet_New(default);
            foreach (var name in GetModuleNames())
            {
                int res = PySet_Add(modules, new BorrowedReference(name));
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
                using var pyDummyGC = PyLong_FromVoidPtr(dummyGCHead);
                int res = PySys_SetObject("dummy_gc", pyDummyGC);
                PythonException.ThrowIfIsNotZero(res);

                try
                {
                    res = PySys_SetObject("initial_modules", modules);
                    PythonException.ThrowIfIsNotZero(res);
                }
                finally
                {
                    modules.Dispose();
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
                        objs.Dispose();
                    }
                }
            }
        }

        public static void Restore()
        {
            var dummyGCAddr = PySys_GetObject("dummy_gc");
            if (dummyGCAddr.IsNull)
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
            foreach (var nameRaw in GetModuleNames())
            {
                var name = new BorrowedReference(nameRaw);
                if (PySet_Contains(intialModules, name) == 1)
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
            BorrowedReference intialObjs = PySys_GetObject("initial_objs");
            Debug.Assert(@intialObjs.IsNull);
            foreach (var objRaw in PyGCGetObjects())
            {
                using var p = PyLong_FromVoidPtr(objRaw);
                var obj = new BorrowedReference(objRaw);
                if (PySet_Contains(intialObjs, p) == 1)
                {
                    continue;
                }
                Debug.Assert(_PyObject_GC_IS_TRACKED(obj), "A GC object must be tracked");
                ExchangeGCChain(obj, dummyGC);
            }
        }

        public static IEnumerable<IntPtr> PyGCGetObjects()
        {
            using var gc = PyModule.Import("gc");
            using var get_objects = gc.GetAttr("get_objects");
            var objs = PyObject_CallObject(get_objects.Handle, IntPtr.Zero);
            var length = PyList_Size(new BorrowedReference(objs));
            for (long i = 0; i < length; i++)
            {
                var obj = PyList_GetItem(new BorrowedReference(objs), i);
                yield return obj.DangerousGetAddress();
            }
        }

        public static IEnumerable<IntPtr> GetModuleNames()
        {
            var modules = PyImport_GetModuleDict();
            using var names = PyDict_Keys(modules);
            var length = PyList_Size(names);
            var result = new IntPtr[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = PyList_GetItem(names, i).DangerousGetAddress();
            }
            return result;
        }

        private static void AddObjPtrToSet(BorrowedReference set, BorrowedReference obj)
        {
            IntPtr objRaw = obj.DangerousGetAddress();
            using var p = PyLong_FromVoidPtr(objRaw);
            XIncref(objRaw);
            int res = PySet_Add(set, p);
            PythonException.ThrowIfIsNotZero(res);
        }
        /// <summary>
        /// Exchange gc to a dummy gc prevent nullptr error in _PyObject_GC_UnTrack macro.
        /// </summary>
        private static void ExchangeGCChain(BorrowedReference obj, IntPtr gc)
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
