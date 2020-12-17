using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestList
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
        public void MissingGenericTypeTest()
        {
            Assert.Throws<PythonException>(() =>
            PythonEngine.Exec($@"
from System.Collections import IList
IList[bool]
"));
        }
    }
}
