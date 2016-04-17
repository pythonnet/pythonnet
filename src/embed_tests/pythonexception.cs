using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
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
            PyList list = new PyList();
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
            PythonException e = new PythonException(); //There is no PyErr to fetch
            Assert.AreEqual("", e.Message);
        }
    }
}