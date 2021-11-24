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
            using var op = Runtime.Runtime.PyString_FromString("FooBar");

            // New object RefCount should be one
            Assert.AreEqual(1, Runtime.Runtime.Refcount32(op.BorrowOrThrow()));

            // Checking refcount didn't change refcount
            Assert.AreEqual(1, Runtime.Runtime.Refcount32(op.Borrow()));

            // Borrowing a reference doesn't increase refcount
            BorrowedReference p = op.Borrow();
            Assert.AreEqual(1, Runtime.Runtime.Refcount32(p));

            // Py_IncRef/Py_DecRef increase and decrease RefCount
            Runtime.Runtime.Py_IncRef(op.Borrow());
            Assert.AreEqual(2, Runtime.Runtime.Refcount32(p));
            Runtime.Runtime.Py_DecRef(StolenReference.DangerousFromPointer(op.DangerousGetAddress()));
            Assert.AreEqual(1, Runtime.Runtime.Refcount32(p));

            // XIncref/XDecref increase and decrease RefCount
#pragma warning disable CS0618 // Type or member is obsolete. We are testing corresponding members
            Runtime.Runtime.XIncref(p);
            Assert.AreEqual(2, Runtime.Runtime.Refcount32(p));
            Runtime.Runtime.XDecref(op.Steal());
            Assert.AreEqual(1, Runtime.Runtime.Refcount32(p));
#pragma warning restore CS0618 // Type or member is obsolete

            op.Dispose();

            Runtime.Runtime.Py_Finalize();
        }

        [Test]
        public static void PyCheck_Iter_PyObject_IsIterable_Test()
        {
            Runtime.Runtime.Py_Initialize();

            Runtime.Native.ABI.Initialize(Runtime.Runtime.PyVersion);

            // Tests that a python list is an iterable, but not an iterator
            using (var pyListNew = Runtime.Runtime.PyList_New(0))
            {
                BorrowedReference pyList = pyListNew.BorrowOrThrow();
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyList));
                Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyList));

                // Tests that a python list iterator is both an iterable and an iterator
                using var pyListIter = Runtime.Runtime.PyObject_GetIter(pyList);
                Assert.IsTrue(Runtime.Runtime.PyObject_IsIterable(pyListIter.BorrowOrThrow()));
                Assert.IsTrue(Runtime.Runtime.PyIter_Check(pyListIter.Borrow()));
            }

            // Tests that a python float is neither an iterable nor an iterator
            using (var pyFloat = Runtime.Runtime.PyFloat_FromDouble(2.73))
            {
                Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(pyFloat.BorrowOrThrow()));
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(pyFloat.Borrow()));
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
                BorrowedReference threadingDict = Runtime.Runtime.PyModule_GetDict(threading.BorrowOrThrow());
                Exceptions.ErrorCheck(threadingDict);
                BorrowedReference lockType = Runtime.Runtime.PyDict_GetItemString(threadingDict, "Lock");
                if (lockType.IsNull)
                    throw PythonException.ThrowLastAsClrException();

                using var args = Runtime.Runtime.PyTuple_New(0);
                using var lockInstance = Runtime.Runtime.PyObject_CallObject(lockType, args.Borrow());

                Assert.IsFalse(Runtime.Runtime.PyObject_IsIterable(lockInstance.BorrowOrThrow()));
                Assert.IsFalse(Runtime.Runtime.PyIter_Check(lockInstance.Borrow()));
            }
            finally
            {
                Runtime.Runtime.Py_Finalize();
            }
        }
    }
}
