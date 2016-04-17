using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyObjectTest
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
        public void TestUnicode()
        {
            PyObject s = new PyString("foo\u00e9");
            Assert.AreEqual("foo\u00e9", s.ToString());
        }
    }
}