using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    class TestCustomArgMarshal
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
        public void CustomArgMarshaller()
        {
            var obj = new CustomArgMarshaling();
            using (Py.GIL()) {
                dynamic callWithInt = PythonEngine.Eval("lambda o: o.CallWithInt('42')");
                callWithInt(obj.ToPython());
            }
            Assert.AreEqual(expected: 42, actual: obj.LastArgument);
        }
    }

[PyArgConverter(typeof(CustomArgConverter))]
class CustomArgMarshaling {
    public object LastArgument { get; private set; }
    public void CallWithInt(int value) => this.LastArgument = value;
}

    class CustomArgConverter : DefaultPyArgumentConverter {
        public override bool TryConvertArgument(IntPtr pyarg, Type parameterType, bool needsResolution,
                                                out object arg, out bool isOut) {
            if (parameterType != typeof(int))
                return base.TryConvertArgument(pyarg, parameterType, needsResolution, out arg, out isOut);

            bool isString = base.TryConvertArgument(pyarg, typeof(string), needsResolution,
                out arg, out isOut);
            if (!isString) return false;

            int number;
            if (!int.TryParse((string)arg, out number)) return false;
            arg = number;
            return true;
        }
    }
}
