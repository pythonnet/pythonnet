using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class Inspect
    {
        [Test]
        public void InstancePropertiesVisibleOnClass()
        {
            var uri = new Uri("http://example.org").ToPython();
            var uriClass = uri.GetPythonType();
            var property = uriClass.GetAttr(nameof(Uri.AbsoluteUri));
            var pyProp = (PropertyObject)ManagedType.GetManagedObject(property.Reference);
            Assert.That(pyProp.info.Value.Name, Is.EqualTo(nameof(Uri.AbsoluteUri)));
        }

        [Test]
        public void BoundMethodsAreInspectable()
        {
            using var scope = Py.CreateScope();
            try
            {
                scope.Import("inspect");
            }
            catch (PythonException)
            {
                Assert.Inconclusive("Python build does not include inspect module");
                return;
            }

            var obj = new Class();
            scope.Set(nameof(obj), obj);
            using var spec = scope.Eval($"inspect.getfullargspec({nameof(obj)}.{nameof(Class.Method)})");
        }

        class Class
        {
            public void Method(int a, int b = 10) { }
            public void Method(int a, object b) { }
        }
    }
}
