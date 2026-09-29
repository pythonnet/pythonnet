using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class CallableObject
    {
        IPythonBaseTypeProvider BaseTypeProvider;

        [OneTimeSetUp]
        public void SetUp()
        {
            using var locals = new PyDict();
            PythonEngine.Exec(CallViaInheritance.BaseClassSource, locals: locals);
            BaseTypeProvider = new CustomBaseTypeProvider(new PyType(locals[CallViaInheritance.BaseClassName]));
            PythonEngine.InteropConfiguration.PythonBaseTypeProviders.Add(BaseTypeProvider);
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.InteropConfiguration.PythonBaseTypeProviders.Remove(BaseTypeProvider);
        }

        [Test]
        public void CallMethodMakesObjectCallable()
        {
            var doubler = new DerivedDoubler();
            dynamic applyObjectTo21 = PythonEngine.Eval("lambda o: o(21)");
            Assert.That((int)applyObjectTo21(doubler.ToPython()), Is.EqualTo(doubler.__call__(21)));
        }

        [Test]
        public void CallMethodCanBeInheritedFromPython()
        {
            var callViaInheritance = new CallViaInheritance();
            dynamic applyObjectTo14 = PythonEngine.Eval("lambda o: o(14)");
            Assert.That((int)applyObjectTo14(callViaInheritance.ToPython()), Is.EqualTo(callViaInheritance.Call(14)));
        }

        [Test]
        public void CanOverwriteCall()
        {
            var callViaInheritance = new CallViaInheritance();
            using var scope = Py.CreateScope();
            scope.Set("o", callViaInheritance);
            scope.Exec("orig_call = o.Call");
            scope.Exec("o.Call = lambda a: orig_call(a*7)");
            int result = scope.Eval<int>("o.Call(5)");
            Assert.That(result, Is.EqualTo(105));
        }

        class Doubler
        {
            public int __call__(int arg) => 2 * arg;
        }

        class DerivedDoubler : Doubler { }

        class CallViaInheritance
        {
            public const string BaseClassName = "Forwarder";
            public static readonly string BaseClassSource = $@"
class MyCallableBase:
  def __call__(self, val):
    return self.Call(val)

class {BaseClassName}(MyCallableBase): pass
";
            public int Call(int arg) => 3 * arg;
        }

        class CustomBaseTypeProvider(PyType BaseClass) : IPythonBaseTypeProvider
        {
            public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
            {
                Assert.That(BaseClass.Refcount, Is.GreaterThan(0));
                return type != typeof(CallViaInheritance)
                    ? existingBases
                    : [BaseClass];
            }
        }
    }
}
