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

        [Test]
        public void MarshallerOverride() {
            var obj = new DerivedMarshaling();
            using (Py.GIL()) {
                dynamic callWithInt = PythonEngine.Eval("lambda o: o.CallWithInt({ 'value': 42 })");
                callWithInt(obj.ToPython());
            }
            Assert.AreEqual(expected: 42, actual: obj.LastArgument);
        }
    }

    [PyArgConverter(typeof(CustomArgConverter))]
    class CustomArgMarshaling {
        public object LastArgument { get; protected set; }
        public virtual void CallWithInt(int value) => this.LastArgument = value;
    }

    // this should override original custom marshaling behavior for any new methods
    [PyArgConverter(typeof(CustomArgConverter2))]
    class DerivedMarshaling : CustomArgMarshaling {
        public override void CallWithInt(int value) {
            base.CallWithInt(value);
        }
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

    class CustomArgConverter2 : DefaultPyArgumentConverter {
        public override bool TryConvertArgument(IntPtr pyarg, Type parameterType, bool needsResolution,
            out object arg, out bool isOut) {
            if (parameterType != typeof(int))
                return base.TryConvertArgument(pyarg, parameterType, needsResolution, out arg, out isOut);
            bool isPyObject = base.TryConvertArgument(pyarg, typeof(PyObject), needsResolution,
                out arg, out isOut);
            if (!isPyObject) return false;
            var dict = new PyDict((PyObject)arg);
            int number = (dynamic)dict["value"];
            arg = number;
            return true;
        }
    }
}
