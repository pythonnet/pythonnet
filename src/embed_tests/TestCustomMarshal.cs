using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestCustomMarshal
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
        public static void GetManagedStringTwice()
        {
            const string expected = "FooBar";

            IntPtr op = Runtime.Runtime.PyUnicode_FromString(expected);
            string s1 = Runtime.Runtime.GetManagedString(op);
            string s2 = Runtime.Runtime.GetManagedString(op);

            Assert.AreEqual(1, Runtime.Runtime.Refcount(op));
            Assert.AreEqual(expected, s1);
            Assert.AreEqual(expected, s2);
        }
    }
}
