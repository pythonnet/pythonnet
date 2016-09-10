using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyIterTest
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
        public void TestOnPyList()
        {
            PyList list = new PyList();
            list.Append(new PyString("foo"));
            list.Append(new PyString("bar"));
            list.Append(new PyString("baz"));
            List<string> result = new List<string>();
            foreach (PyObject item in list)
                result.Add(item.ToString());
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("foo", result[0]);
            Assert.AreEqual("bar", result[1]);
            Assert.AreEqual("baz", result[2]);
        }
    }
}