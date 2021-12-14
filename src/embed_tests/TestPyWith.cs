using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyWith
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
        /// Test that exception is raised in context manager that ignores it.
        /// </summary>
        [Test]
        public void TestWithPositive()
        {
            var locals = new PyDict();

            PythonEngine.Exec(@"
class CmTest:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        # Exception not handled, return will be False
        pass
    def fail(self):
        return 5 / 0

a = CmTest()
", null, locals);

            var a = locals.GetItem("a");

            try
            {
                Py.With(a, cmTest =>
                {
                    cmTest.fail();
                });
            }
            catch (PythonException e)
            {
                TestContext.Out.WriteLine(e.Message);
                Assert.IsTrue(e.Type.Name == "ZeroDivisionError");
            }
        }


        /// <summary>
        /// Test that exception is not raised in context manager that handles it
        /// </summary>
        [Test]
        public void TestWithNegative()
        {
            var locals = new PyDict();

            PythonEngine.Exec(@"
class CmTest:
    def __enter__(self):
        print('Enter')
        return self
    def __exit__(self, t, v, tb):
        # Signal exception is handled by returning true
        return True
    def fail(self):
        return 5 / 0

a = CmTest()
", null, locals);

            var a = locals.GetItem("a");
            Py.With(a, cmTest =>
            {
                cmTest.fail();
            });
        }
    }
}
