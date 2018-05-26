using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPythonException
    {
        private IntPtr _gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            _gs = PythonEngine.AcquireLock();
        }

        [TearDown]
        public void Dispose()
        {
            PythonEngine.ReleaseLock(_gs);
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

        [Test]
        public void TestPythonErrorTypeName()
        {
            try
            {
                var module = PythonEngine.ImportModule("really____unknown___module");
                Assert.Fail("Unknown module should not be loaded");
            }
            catch (PythonException ex)
            {
                Assert.That(ex.PythonTypeName, Is.EqualTo("ModuleNotFoundError").Or.EqualTo("ImportError"));
            }
        }
    }
}
