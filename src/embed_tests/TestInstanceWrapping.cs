using NUnit.Framework;
using Python.Runtime;
using Python.Runtime.Slots;

namespace Python.EmbeddingTest {
    public class TestInstanceWrapping {
        [Test]
        public void GetAttrCanBeOverriden() {
            var overloaded = new Overloaded();
            using (Py.GIL()) {
                var o = overloaded.ToPython();
                dynamic getNonexistingAttr = PythonEngine.Eval("lambda o: o.non_existing_attr");
                string nonexistentAttrValue = getNonexistingAttr(o);
                Assert.AreEqual(GetAttrFallbackValue, nonexistentAttrValue);
            }
        }

        [Test]
        public void SetAttrCanBeOverriden() {
            var overloaded = new Overloaded();
            using (Py.GIL())
            using (var scope = Py.CreateScope()) {
                var o = overloaded.ToPython();
                scope.Set(nameof(o), o);
                scope.Exec($"{nameof(o)}.non_existing_attr = 42");
                Assert.AreEqual(42, overloaded.Value);
            }
        }

        const string GetAttrFallbackValue = "undefined";

        class Base {}
        class Derived: Base { }

        class Overloaded: Derived, IGetAttr, ISetAttr
        {
            public int Value { get; private set; }

            public bool TryGetAttr(string name, out PyObject value) {
                value = GetAttrFallbackValue.ToPython();
                return true;
            }

            public bool TrySetAttr(string name, PyObject value) {
                this.Value = value.As<int>();
                return true;
            }
        }
    }
}
