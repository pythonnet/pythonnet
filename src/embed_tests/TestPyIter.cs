using System.Linq;
using System.Text;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    class TestPyIter
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
        public void KeepOldObjects()
        {
            using (Py.GIL())
            using (var testString = new PyString("hello world! !$%&/()=?"))
            {
                PyObject[] chars = testString.ToArray();
                Assert.IsTrue(chars.Length > 1);
                string reconstructed = string.Concat(chars.Select(c => c.As<string>()));
                Assert.AreEqual(testString.As<string>(), reconstructed);
            }
        }
    }
}
