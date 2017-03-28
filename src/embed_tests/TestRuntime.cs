using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestRuntime
    {
        [Test]
        public static void Py_IsInitializedValue()
        {
            Runtime.Runtime.Py_Finalize(); // In case another test left it on.
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
    }
}
