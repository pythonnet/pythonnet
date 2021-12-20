using System;

using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest {
    public class TestCallbacks {
        [OneTimeSetUp]
        public void SetUp() {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose() {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestNoOverloadException() {
            int passed = 0;
            var aFunctionThatCallsIntoPython = new Action<int>(value => passed = value);
            using (Py.GIL()) {
                using dynamic callWith42 = PythonEngine.Eval("lambda f: f([42])");
                using var pyFunc = aFunctionThatCallsIntoPython.ToPython();
                var error =  Assert.Throws<PythonException>(() => callWith42(pyFunc));
                Assert.AreEqual("TypeError", error.Type.Name);
                string expectedArgTypes = "(<class 'list'>)";
                StringAssert.EndsWith(expectedArgTypes, error.Message);
                error.Traceback.Dispose();
            }
        }
    }
}
