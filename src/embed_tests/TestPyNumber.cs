using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyNumber
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void IsNumberTypeTrue()
        {
            var i = new PyInt(1);
            Assert.IsTrue(PyNumber.IsNumberType(i));
        }

        [Test]
        public void IsNumberTypeFalse()
        {
            var s = new PyString("Foo");
            Assert.IsFalse(PyNumber.IsNumberType(s));
        }
    }
}
