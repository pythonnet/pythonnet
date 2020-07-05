using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestExample
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
        public void TestReadme()
        {
            dynamic np;
            try
            {
                np = Py.Import("numpy");
            }
            catch (PythonException)
            {
                Assert.Inconclusive("Numpy or dependency not installed");
                return;
            }
            
            Assert.AreEqual("1.0", np.cos(np.pi * 2).ToString());

            dynamic sin = np.sin;
            StringAssert.StartsWith("-0.95892", sin(5).ToString());

            double c = np.cos(5) + sin(5);
            Assert.AreEqual(-0.675262, c, 0.01);

            dynamic a = np.array(new List<float> { 1, 2, 3 });
            Assert.AreEqual("float64", a.dtype.ToString());

            dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
            Assert.AreEqual("int32", b.dtype.ToString());

            Assert.AreEqual("[ 6. 10. 12.]", (a * b).ToString().Replace("  ", " "));
        }
    }
}
