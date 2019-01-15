using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestNamedArguments
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

        /// <summary>
        /// Test named arguments support through Py.kw method
        /// </summary>
        [Test]
        public void TestKeywordArgs()
        {
            dynamic a = CreateTestClass();
            var result = (int)a.Test3(2, Py.kw("a4", 8));

            Assert.AreEqual(12, result);
        }


        /// <summary>
        /// Test keyword arguments with .net named arguments
        /// </summary>
        [Test]
        public void TestNamedArgs()
        {
            dynamic a = CreateTestClass();
            var result = (int)a.Test3(2, a4: 8);

            Assert.AreEqual(12, result);
        }



        private static PyObject CreateTestClass()
        {
            var locals = new PyDict();

            PythonEngine.Exec(@"
class cmTest3:
    def Test3(self, a1 = 1, a2 = 1, a3 = 1, a4 = 1):
        return a1 + a2 + a3 + a4

a = cmTest3()
", null, locals.Handle);

            return locals.GetItem("a");
        }

    }
}
