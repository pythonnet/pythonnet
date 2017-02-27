using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// Test Python Exceptions
    /// </summary>
    /// <remarks>
    /// Keeping this in the old-style SetUp/TearDown
    /// to ensure that setup still works.
    /// </remarks>
    public class PythonExceptionTest
    {
        private IntPtr gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            gs = PythonEngine.AcquireLock();
        }

        [TearDown]
        public void Dispose()
        {
            PythonEngine.ReleaseLock(gs);
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestMessage()
        {
            var list = new PyList();
            PyObject foo = null;

            var ex = Assert.Throws<PythonException>(() => foo = list[0]);

            Assert.AreEqual("IndexError : list index out of range", ex.Message);
            Assert.IsNull(foo);
        }

        [Test]
        public void TestNoError()
        {
            var e = new PythonException(); // There is no PyErr to fetch
            Assert.AreEqual("", e.Message);
        }
    }
}
