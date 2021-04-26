
using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    class TestPyModule
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
            PyModule testmod = PyModule.Create("testmod");
            testmod.SetAttr("testattr1", "True".ToPython());

            using PyObject code = PythonEngine.Compile(
                "import testmod\n" +
                "x = testmod.testattr1"
                );
            scope.Execute(code);

            Assert.IsTrue(scope.TryGet("x", out dynamic x));
            Assert.AreEqual("True", x.ToString());
        }

        [Test]
        public void TestCreateExisting()
        {
            using PyScope scope = Py.CreateScope();
            PyModule sysmod = PyModule.Create("sys");
            sysmod.SetAttr("testattr1", "Hello, Python".ToPython());

            dynamic sys = Py.Import("sys");
            Assert.AreEqual("Hello, Python", sys.testattr1.ToString());
        }
    }
}
