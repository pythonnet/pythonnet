using System;
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

        /// <summary>
        /// Test the cache of the information from the platform module.
        ///
        /// Test fails on platforms we haven't implemented yet.
        /// </summary>
        [Test]
        public static void PlatformCache()
        {
            Runtime.Runtime.Initialize();

            Assert.That(Runtime.Runtime.Machine, Is.Not.EqualTo(Runtime.Runtime.MachineType.Other));
            Assert.That(!string.IsNullOrEmpty(Runtime.Runtime.MachineName));

            Assert.That(Runtime.Runtime.OperatingSystem, Is.Not.EqualTo(Runtime.Runtime.OperatingSystemType.Other));
            Assert.That(!string.IsNullOrEmpty(Runtime.Runtime.OperatingSystemName));

            // Don't shut down the runtime: if the python engine was initialized
            // but not shut down by another test, we'd end up in a bad state.
	}

        [Test]
        public static void Py_IsInitializedValue()
        {
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
            IntPtr op = Runtime.Runtime.PyUnicode_FromString("FooBar");

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

            // Tests that a python list is an iterable, but not an iterator
            var pyList = Runtime.Runtime.PyList_New(0);
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyList));
            Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyList));

            // Tests that a python list iterator is both an iterable and an iterator
            var pyListIter = Runtime.Runtime.PyObject_GetIter(pyList);
            Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyListIter));
            Assert.IsTrue(Runtime.Runtime.PyIter_Check(pyListIter));

            // Tests that a python float is neither an iterable nor an iterator
            var pyFloat = Runtime.Runtime.PyFloat_FromDouble(2.73);
            Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(pyFloat));
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyFloat));

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_ThreadingLock_Test()
        {
            Runtime.Runtime.Py_Initialize();

            // Create an instance of threading.Lock, which is one of the very few types that does not have the
            // TypeFlags.HaveIter set in Python 2. This tests a different code path in PyObject_IsIterable and PyIter_Check.
            var threading = Runtime.Runtime.PyImport_ImportModule("threading");
            var threadingDict = Runtime.Runtime.PyModule_GetDict(threading);
            var lockType = Runtime.Runtime.PyDict_GetItemString(threadingDict, "Lock");
            var lockInstance = Runtime.Runtime.PyObject_CallObject(lockType, Runtime.Runtime.PyTuple_New(0));

            Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(lockInstance));
            Assert.IsFalse(Runtime.Runtime.PyIter_Check(lockInstance));

            Runtime.Runtime.Py_Finalize();
        }
    }
}
