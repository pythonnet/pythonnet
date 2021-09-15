using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestRuntime
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            // We needs to ensure that no any engines are running.
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
        }

        [Test]
        public static void Py_IsInitializedValue()
        {
            if (Runtime.Runtime.Py_IsInitialized() == 1)
            {
                Runtime.Runtime.PyGILState_Ensure();
            }
            Runtime.Runtime.Py_Finalize();
            Assert.AreEqual(0, Runtime.Runtime.Py_IsInitialized());
            Runtime.Runtime.Py_Initialize();
            Assert.AreEqual(1, Runtime.Runtime.Py_IsInitialized());
            Runtime.Runtime.Py_Finalize();
            Assert.AreEqual(0, Runtime.Runtime.Py_IsInitialized());
        }

        [Test]
        public static void RefCountTest()
        {
            Runtime.Runtime.Py_Initialize();
            IntPtr op = Runtime.Runtime.PyString_FromString("FooBar");

            // New object RefCount should be one
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // Checking refcount didn't change refcount
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // New reference doesn't increase refcount
            IntPtr p = op;
            Assert.AreEqual(1, Runtime.Runtime.Refcount(p));

            // Py_IncRef/Py_DecRef increase and decrease RefCount
            Runtime.Runtime.Py_IncRef(op);
            Assert.AreEqual(2, Runtime.Runtime.Refcount(op));
            Runtime.Runtime.Py_DecRef(op);
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            // XIncref/XDecref increase and decrease RefCount
            Runtime.Runtime.XIncref(op);
            Assert.AreEqual(2, Runtime.Runtime.Refcount(op));
            Runtime.Runtime.XDecref(op);
            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_Test()
        {
            Runtime.Runtime.Py_Initialize();

            Runtime.Native.ABI.Initialize(Runtime.Runtime.PyVersion);

            // Tests that a python list is an iterable, but not an iterator
            using (var pyList = NewReference.DangerousFromPointer(Runtime.Runtime.PyList_New(0)))
            {
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyList));
                Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyList));

                // Tests that a python list iterator is both an iterable and an iterator
                using var pyListIter = Runtime.Runtime.PyObject_GetIter(pyList);
                Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyListIter));
                Assert.IsTrue(Runtime.Runtime.PyIter_Check(pyListIter));
            }

            // Tests that a python float is neither an iterable nor an iterator
            using (var pyFloat = NewReference.DangerousFromPointer(Runtime.Runtime.PyFloat_FromDouble(2.73)))
            {
                Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(pyFloat));
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyFloat));
            }

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_ThreadingLock_Test()
        {
            Runtime.Runtime.Py_Initialize();

            Runtime.Native.ABI.Initialize(Runtime.Runtime.PyVersion);

            try
            {
                // Create an instance of threading.Lock, which is one of the very few types that does not have the
                // TypeFlags.HaveIter set in Python 2. This tests a different code path in PyObject_IsIterable and PyIter_Check.
                using var threading = Runtime.Runtime.PyImport_ImportModule("threading");
                Exceptions.ErrorCheck(threading);
                var threadingDict = Runtime.Runtime.PyModule_GetDict(threading);
                Exceptions.ErrorCheck(threadingDict);
                var lockType = Runtime.Runtime.PyDict_GetItemString(threadingDict, "Lock");
                if (lockType.IsNull)
                    throw PythonException.ThrowLastAsClrException();

                using var args = NewReference.DangerousFromPointer(Runtime.Runtime.PyTuple_New(0));
                using var lockInstance = Runtime.Runtime.PyObject_CallObject(lockType, args);
                Exceptions.ErrorCheck(lockInstance);

                Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(lockInstance));
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(lockInstance));
            }
            finally
            {
                Runtime.Runtime.Py_Finalize();
            }
        }
    }
}
