using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;


namespace Python.EmbeddingTest
{
    public class PySetHomeSet
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestSetHome()
        {
            string homePath = @"C:\Python27\";
            PythonEngine.PythonHome = homePath;
            PythonEngine.Initialize();
            Assert.AreEqual(PythonEngine.PythonHome, homePath);
        }
    }
}
