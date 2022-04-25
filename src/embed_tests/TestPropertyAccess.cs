using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class TestPropertyAccess
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

        public class Fixture
        {
            public string PublicProperty { get; set; } = "Default value";
            protected string ProtectedProperty { get; set; } = "Default value";

            public string PublicReadOnlyProperty { get; } = "Default value";
            protected string ProtectedReadOnlyProperty { get; } = "Default value";

            public static string PublicStaticProperty { get; set; } = "Default value";
            protected static string ProtectedStaticProperty { get; set; } = "Default value";

            public static string PublicStaticReadOnlyProperty { get; } = "Default value";
            protected static string ProtectedStaticReadOnlyProperty { get; } = "Default value";

            public string PublicField = "Default value";
            protected string ProtectedField = "Default value";

            public readonly string PublicReadOnlyField = "Default value";
            protected readonly string ProtectedReadOnlyField = "Default value";

            public static string PublicStaticField = "Default value";
            protected static string ProtectedStaticField = "Default value";

            public static readonly string PublicStaticReadOnlyField = "Default value";
            protected static readonly string ProtectedStaticReadOnlyField = "Default value";

            public static Fixture Create()
            {
                return new Fixture();
            }
        }

        public class NonStaticConstHolder
        {
            public const string USA = "usa";
        }

        public static class StaticConstHolder
        {
            public const string USA = "usa";
        }

        [Test]
        public void TestPublicStaticMethodWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestPublicStaticMethodWorks:
    def GetValue(self):
        return TestPropertyAccess.Fixture.Create()
").GetAttr("TestPublicStaticMethodWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().PublicProperty.ToString());
            }
        }

        [Test]
        public void TestConstWorksInNonStaticClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestConstWorksInNonStaticClass:
    def GetValue(self):
        return TestPropertyAccess.NonStaticConstHolder.USA
").GetAttr("TestConstWorksInNonStaticClass").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("usa", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestConstWorksInStaticClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestConstWorksInStaticClass:
    def GetValue(self):
        return TestPropertyAccess.StaticConstHolder.USA
").GetAttr("TestConstWorksInStaticClass").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("usa", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestGetPublicPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicPropertyWorks:
    def GetValue(self, fixture):
        return fixture.PublicProperty
").GetAttr("TestGetPublicPropertyWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue(fixture).ToString());
            }
        }

        [Test]
        public void TestSetPublicPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicPropertyWorks:
    def SetValue(self, fixture):
        fixture.PublicProperty = 'New value'
").GetAttr("TestSetPublicPropertyWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                model.SetValue(fixture);
                Assert.AreEqual("New value", fixture.PublicProperty);
            }
        }

        [Test]
        public void TestGetPublicPropertyFailsWhenAccessedOnClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicPropertyFailsWhenAccessedOnClass:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicProperty
").GetAttr("TestGetPublicPropertyFailsWhenAccessedOnClass").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.GetValue());
            }
        }

        [Test]
        public void TestGetProtectedPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedPropertyWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return self.ProtectedProperty
").GetAttr("TestGetProtectedPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedPropertyWorks(TestPropertyAccess.Fixture):
    def SetValue(self):
        self.ProtectedProperty = 'New value'

    def GetValue(self):
        return self.ProtectedProperty
").GetAttr("TestSetProtectedPropertyWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestGetPublicReadOnlyPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicReadOnlyPropertyWorks:
    def GetValue(self, fixture):
        return fixture.PublicReadOnlyProperty
").GetAttr("TestGetPublicReadOnlyPropertyWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue(fixture).ToString());
            }
        }

        [Test]
        public void TestSetPublicReadOnlyPropertyFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicReadOnlyPropertyFails:
    def SetValue(self, fixture):
        fixture.PublicReadOnlyProperty = 'New value'
").GetAttr("TestSetPublicReadOnlyPropertyFails").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue(fixture));
            }
        }

        [Test]
        public void TestGetPublicReadOnlyPropertyFailsWhenAccessedOnClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicReadOnlyPropertyFailsWhenAccessedOnClass:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicReadOnlyProperty
").GetAttr("TestGetPublicReadOnlyPropertyFailsWhenAccessedOnClass").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.GetValue());
            }
        }

        [Test]
        public void TestGetProtectedReadOnlyPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedReadOnlyPropertyWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return self.ProtectedReadOnlyProperty
").GetAttr("TestGetProtectedReadOnlyPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedReadOnlyPropertyFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedReadOnlyPropertyFails(TestPropertyAccess.Fixture):
    def SetValue(self):
        self.ProtectedReadOnlyProperty = 'New value'
").GetAttr("TestSetProtectedReadOnlyPropertyFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Test]
        public void TestGetPublicStaticPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicStaticPropertyWorks:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicStaticProperty
").GetAttr("TestGetPublicStaticPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetPublicStaticPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicStaticPropertyWorks:
    def SetValue(self):
        TestPropertyAccess.Fixture.PublicStaticProperty = 'New value'
").GetAttr("TestSetPublicStaticPropertyWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", Fixture.PublicStaticProperty);
            }
        }

        [Test]
        public void TestGetProtectedStaticPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedStaticPropertyWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticProperty
").GetAttr("TestGetProtectedStaticPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedStaticPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedStaticPropertyWorks(TestPropertyAccess.Fixture):
    def SetValue(self):
        TestPropertyAccess.Fixture.ProtectedStaticProperty = 'New value'

    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticProperty
").GetAttr("TestSetProtectedStaticPropertyWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestGetPublicStaticReadOnlyPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicStaticReadOnlyPropertyWorks:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicStaticReadOnlyProperty
").GetAttr("TestGetPublicStaticReadOnlyPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetPublicStaticReadOnlyPropertyFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicStaticReadOnlyPropertyFails:
    def SetValue(self):
        TestPropertyAccess.Fixture.PublicReadOnlyProperty = 'New value'
").GetAttr("TestSetPublicStaticReadOnlyPropertyFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Test]
        public void TestGetProtectedStaticReadOnlyPropertyWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedStaticReadOnlyPropertyWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticReadOnlyProperty
").GetAttr("TestGetProtectedStaticReadOnlyPropertyWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedStaticReadOnlyPropertyFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedStaticReadOnlyPropertyFails(TestPropertyAccess.Fixture):
    def SetValue(self):
        TestPropertyAccess.Fixture.ProtectedStaticReadOnlyProperty = 'New value'
").GetAttr("TestSetProtectedStaticReadOnlyPropertyFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Test]
        public void TestGetPublicFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicFieldWorks:
    def GetValue(self, fixture):
        return fixture.PublicField
").GetAttr("TestGetPublicFieldWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue(fixture).ToString());
            }
        }

        [Test]
        public void TestSetPublicFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicFieldWorks:
    def SetValue(self, fixture):
        fixture.PublicField = 'New value'
").GetAttr("TestSetPublicFieldWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                model.SetValue(fixture);
                Assert.AreEqual("New value", fixture.PublicField);
            }
        }

        [Test]
        public void TestGetPublicFieldFailsWhenAccessedOnClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicFieldFailsWhenAccessedOnClass:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicField
").GetAttr("TestGetPublicFieldFailsWhenAccessedOnClass").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.GetValue());
            }
        }

        [Test]
        public void TestGetProtectedFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedFieldWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return self.ProtectedField
").GetAttr("TestGetProtectedFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedPropertyWorks(TestPropertyAccess.Fixture):
    def SetValue(self):
        self.ProtectedField = 'New value'

    def GetValue(self):
        return self.ProtectedField
").GetAttr("TestSetProtectedPropertyWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestGetPublicReadOnlyFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicReadOnlyFieldWorks:
    def GetValue(self, fixture):
        return fixture.PublicReadOnlyField
").GetAttr("TestGetPublicReadOnlyFieldWorks").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue(fixture).ToString());
            }
        }

        [Test]
        public void TestSetPublicReadOnlyFieldFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicReadOnlyFieldFails:
    def SetValue(self, fixture):
        fixture.PublicReadOnlyField = 'New value'
").GetAttr("TestSetPublicReadOnlyFieldFails").Invoke();

            var fixture = new Fixture();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue(fixture));
            }
        }

        [Test]
        public void TestGetPublicReadOnlyFieldFailsWhenAccessedOnClass()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicReadOnlyFieldFailsWhenAccessedOnClass:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicReadOnlyField
").GetAttr("TestGetPublicReadOnlyFieldFailsWhenAccessedOnClass").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.GetValue());
            }
        }

        [Test]
        public void TestGetProtectedReadOnlyFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedReadOnlyFieldWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return self.ProtectedReadOnlyField
").GetAttr("TestGetProtectedReadOnlyFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedReadOnlyFieldFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedReadOnlyFieldFails(TestPropertyAccess.Fixture):
    def SetValue(self):
        self.ProtectedReadOnlyField = 'New value'
").GetAttr("TestSetProtectedReadOnlyFieldFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Test]
        public void TestGetPublicStaticFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicStaticFieldWorks:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicStaticField
").GetAttr("TestGetPublicStaticFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetPublicStaticFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicStaticFieldWorks:
    def SetValue(self):
        TestPropertyAccess.Fixture.PublicStaticField = 'New value'
").GetAttr("TestSetPublicStaticFieldWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", Fixture.PublicStaticField);
            }
        }

        [Test]
        public void TestGetProtectedStaticFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedStaticFieldWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticField
").GetAttr("TestGetProtectedStaticFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedStaticFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedStaticFieldWorks(TestPropertyAccess.Fixture):
    def SetValue(self):
        TestPropertyAccess.Fixture.ProtectedStaticField = 'New value'

    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticField
").GetAttr("TestSetProtectedStaticFieldWorks").Invoke();

            using (Py.GIL())
            {
                model.SetValue();
                Assert.AreEqual("New value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestGetPublicStaticReadOnlyFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetPublicStaticReadOnlyFieldWorks:
    def GetValue(self):
        return TestPropertyAccess.Fixture.PublicStaticReadOnlyField
").GetAttr("TestGetPublicStaticReadOnlyFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetPublicStaticReadOnlyFieldFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetPublicStaticReadOnlyFieldFails:
    def SetValue(self):
        TestPropertyAccess.Fixture.PublicReadOnlyField = 'New value'
").GetAttr("TestSetPublicStaticReadOnlyFieldFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Test]
        public void TestGetProtectedStaticReadOnlyFieldWorks()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestGetProtectedStaticReadOnlyFieldWorks(TestPropertyAccess.Fixture):
    def GetValue(self):
        return TestPropertyAccess.Fixture.ProtectedStaticReadOnlyField
").GetAttr("TestGetProtectedStaticReadOnlyFieldWorks").Invoke();

            using (Py.GIL())
            {
                Assert.AreEqual("Default value", model.GetValue().ToString());
            }
        }

        [Test]
        public void TestSetProtectedStaticReadOnlyFieldFails()
        {
            dynamic model = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class TestSetProtectedStaticReadOnlyFieldFails(TestPropertyAccess.Fixture):
    def __init__(self):
        self._my_value = True

    def SetValue(self):
        self._my_value = False
        TestPropertyAccess.Fixture.ProtectedStaticReadOnlyField = 'New value'
").GetAttr("TestSetProtectedStaticReadOnlyFieldFails").Invoke();

            using (Py.GIL())
            {
                Assert.Throws<PythonException>(() => model.SetValue());
            }
        }

        [Explicit]
        [TestCase(true, TestName = "CSharpGetPropertyPerformance")]
        [TestCase(false, TestName = "PythonGetPropertyPerformance")]
        public void TestGetPropertyPerformance(bool useCSharp)
        {
            IModel model;
            if (useCSharp)
            {
                model = new CSharpModel();
            }
            else
            {
                var pyModel = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestPropertyAccess.IModel):
    __namespace__ = ""Python.EmbeddingTest""

    def __init__(self):
        self._indicator = TestPropertyAccess.Indicator()

    def InvokeModel(self):
        value = self._indicator.Current.Value
").GetAttr("PythonModel").Invoke();

                model = new ModelPythonWrapper(pyModel);
            }

            // jit
            model.InvokeModel();

            const int iterations = 5000000;
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                model.InvokeModel();
            }

            stopwatch.Stop();
            var thousandInvocationsPerSecond = iterations / 1000d / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"Elapsed: {stopwatch.Elapsed.TotalMilliseconds}ms for {iterations} iterations. {thousandInvocationsPerSecond} KIPS");
        }

        public interface IModel
        {
            void InvokeModel();
        }

        public class IndicatorValue
        {
            public int Value => 42;
        }

        public class Indicator
        {
            public IndicatorValue Current { get; } = new IndicatorValue();
        }

        public class CSharpModel : IModel
        {
            private readonly Indicator _indicator = new Indicator();

            public virtual void InvokeModel()
            {
                var value = _indicator.Current.Value;
            }
        }

        public class ModelPythonWrapper : IModel
        {
            private readonly dynamic _invokeModel;

            public ModelPythonWrapper(PyObject impl)
            {
                _invokeModel = impl.GetAttr("InvokeModel");
            }

            public virtual void InvokeModel()
            {
                using (Py.GIL())
                {
                    _invokeModel();
                }
            }
        }
    }
}
