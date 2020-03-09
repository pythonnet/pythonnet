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

        private class Callables
        {
            internal object CallFunction0(Func<object> func)
            {
                return func();
            }

            internal object CallFunction1(Func<object[], object> func, object arg)
            {
                return func(new[] { arg});
            }

            internal void CallAction0(Action func)
            {
                func();
            }

            internal void CallAction1(Action<object[]> func, object arg)
            {
                func(new[] { arg });
            }
        }

        [Test]
        public void TestPythonFunctionPassedIntoCLRMethod()
        {
            var locals = new PyDict();
            PythonEngine.Exec(@"
def ret_1():
    return 1
def str_len(a):
    return len(a)
", null, locals.Handle);

            var ret1 = locals.GetItem("ret_1");
            var strLen = locals.GetItem("str_len");

            var callables = new Callables();

            Python.Runtime.Codecs.FunctionCodec.Register();

            //ret1.  A function with no arguments that returns an integer
            //it must be convertible to Action or Func<object> and not to Func<object, object>
            {
                Assert.IsTrue(Converter.ToManaged(ret1.Handle, typeof(Action), out var result1, false));
                Assert.IsTrue(Converter.ToManaged(ret1.Handle, typeof(Func<object>), out var result2, false));

                Assert.DoesNotThrow(() => { callables.CallAction0((Action)result1); });
                object ret2 = null;
                Assert.DoesNotThrow(() => { ret2 = callables.CallFunction0((Func<object>)result2); });
                Assert.AreEqual(ret2, 1);
            }

            //strLen.  A function that takes something with a __len__ and returns the result of that function
            //It must be convertible to an Action<object[]> and Func<object[], object>) and not to an  Action or Func<object>
            {
                Assert.IsTrue(Converter.ToManaged(strLen.Handle, typeof(Action<object[]>), out var result3, false));
                Assert.IsTrue(Converter.ToManaged(strLen.Handle, typeof(Func<object[], object>), out var result4, false));

                //try using both func and action to show you can get __len__ of a string but not an integer
                Assert.Throws<PythonException>(() => { callables.CallAction1((Action<object[]>)result3, 2); });
                Assert.DoesNotThrow(() => { callables.CallAction1((Action<object[]>)result3, "hello"); });
                Assert.Throws<PythonException>(() => { callables.CallFunction1((Func<object[], object>)result4, 2); });

                object ret2 = null;
                Assert.DoesNotThrow(() => { ret2 = callables.CallFunction1((Func<object[], object>)result4, "hello"); });
                Assert.AreEqual(ret2, 5);
            }

            PyObjectConversions.Reset();
        }
    }
}
