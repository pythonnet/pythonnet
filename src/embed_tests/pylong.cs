using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyLongTest
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
        public void TestToInt64()
        {
            long largeNumber = 8L * 1024L * 1024L * 1024L; // 8 GB
            PyLong pyLargeNumber = new PyLong(largeNumber);
            Assert.AreEqual(largeNumber, pyLargeNumber.ToInt64());
        }
    }
}
