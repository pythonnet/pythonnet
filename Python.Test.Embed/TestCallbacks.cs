using System;

using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest {
    using Runtime = Python.Runtime.Runtime;

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
                dynamic callWith42 = PythonEngine.Eval("lambda f: f([42])");
                var error =  Assert.Throws<PythonException>(() => callWith42(aFunctionThatCallsIntoPython.ToPython()));
                Assert.AreEqual("TypeError", error.PythonTypeName);
                string expectedArgTypes = Runtime.IsPython2
                    ? "(<type 'list'>)"
                    : "(<class 'list'>)";
                StringAssert.EndsWith(expectedArgTypes, error.Message);
            }
        }
    }
}
