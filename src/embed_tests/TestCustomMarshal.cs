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
            string path = @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38;";
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONHOME", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PYTHONPATH ", @"C:\Users\Sofiane\AppData\Local\Programs\Python\Python38\DLLs", EnvironmentVariableTarget.Process);

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
