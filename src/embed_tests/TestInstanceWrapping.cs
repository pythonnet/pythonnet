using System;
using System.Globalization;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestInstanceWrapping
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

        // regression test for https://github.com/pythonnet/pythonnet/issues/811
        [Test]
        public void OverloadResolution_UnknownToObject()
        {
            var overloaded = new Overloaded();
            using (Py.GIL())
            {
                var o = overloaded.ToPython();

                dynamic callWithSelf = PythonEngine.Eval("lambda o: o.ObjOrClass(object())");
                callWithSelf(o);
                Assert.AreEqual(Overloaded.Object, overloaded.Value);
            }
        }

        [Test]
        public void WeakRefIsNone_AfterObjectIsGone()
        {
            using var makeref = Py.Import("weakref").GetAttr("ref");
            var ub = new UriBuilder().ToPython();
            using var weakref = makeref.Invoke(ub);
            ub.Dispose();
            Assert.IsTrue(weakref.Invoke().IsNone());
        }

        class Base {}
        class Derived: Base { }

        class Overloaded: Derived
        {
            public int Value { get; set; }
            public void IntOrStr(int arg) => this.Value = arg;
            public void IntOrStr(string arg) =>
                this.Value = int.Parse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture);

            public const int Object = 1;
            public const int ConcreteClass = 2;
            public void ObjOrClass(object _) => this.Value = Object;
            public void ObjOrClass(Overloaded _) => this.Value = ConcreteClass;

            public const int Base = ConcreteClass + 1;
            public const int Derived = Base + 1;
            public void BaseOrDerived(Base _) => this.Value = Base;
            public void BaseOrDerived(Derived _) => this.Value = Derived;
        }
    }
}
