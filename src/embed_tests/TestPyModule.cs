
using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyModule
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
        public void TestCreate()
        {
            using PyScope scope = Py.CreateScope();

            Assert.IsFalse(PyModule.IsInSysModules("testmod"));

            PyModule testmod = PyModule.Create("testmod");
            testmod.SetAttr("testattr1", "True".ToPython());

            testmod.AddToSysModules();

            using PyObject code = PythonEngine.Compile(
                "import testmod\n" +
                "x = testmod.testattr1"
                );
            scope.Execute(code);

            Assert.IsTrue(scope.TryGet("x", out dynamic x));
            Assert.AreEqual("True", x.ToString());
        }
    }
}
