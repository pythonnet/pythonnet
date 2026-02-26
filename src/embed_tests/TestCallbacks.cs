using System;

using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest {
    public class TestCallbacks {
        [Test]
        public void TestNoOverloadException() {
            int passed = 0;
            var aFunctionThatCallsIntoPython = new Action<int>(value => passed = value);
            using (Py.GIL()) {
                using dynamic callWith42 = PythonEngine.Eval("lambda f: f([42])");
                using var pyFunc = aFunctionThatCallsIntoPython.ToPython();
                var error =  Assert.Throws<PythonException>(() => callWith42(pyFunc));
                Assert.That(error.Type.Name, Is.EqualTo("TypeError"));
                string expectedArgTypes = "(<class 'list'>)";
                StringAssert.EndsWith(expectedArgTypes, error.Message);
                error.Traceback.Dispose();
            }
        }
    }
}
