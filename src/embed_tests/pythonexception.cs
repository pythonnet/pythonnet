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
    [TestFixture]
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
        public void TearDown()
        {
            PythonEngine.ReleaseLock(gs);
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestMessage()
        {
            var list = new PyList();
            try
            {
                PyObject junk = list[0];
            }
            catch (PythonException e)
            {
                Assert.AreEqual("IndexError : list index out of range", e.Message);
            }
        }

        [Test]
        public void TestNoError()
        {
            var e = new PythonException(); // There is no PyErr to fetch
            Assert.AreEqual("", e.Message);
        }
    }
}
