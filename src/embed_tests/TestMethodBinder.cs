using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestMethodBinder
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
        public void ImplicitConversionToString()
        {
            // create instance of python model
            dynamic pyMethodCall = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    def MethodCall(self):
        return self.OnlyString(TestMethodBinder.TestImplicitConversion())
").GetAttr("PythonModel").Invoke();

            using (Py.GIL())
            {
                var data = (string)pyMethodCall.MethodCall();
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyString impl: implicit to string", data);
            }
        }

        [Test]
        public void ImplicitConversionToClass()
        {
            // create instance of python model
            dynamic pyMethodCall = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    def MethodCall(self):
        return self.OnlyClass('input string')
").GetAttr("PythonModel").Invoke();

            using (Py.GIL())
            {
                var data = (string)pyMethodCall.MethodCall();
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyClass impl", data);
            }
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_String()
        {
            // create instance of python model
            dynamic pyMethodCall = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    def MethodCall(self):
        return self.InvokeModel('input string')
").GetAttr("PythonModel").Invoke();

            using (Py.GIL())
            {
                var data = (string)pyMethodCall.MethodCall();
                // we assert no implicit conversion took place
                Assert.AreEqual("string impl: input string", data);
            }
        }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_Class()
        {
            // create instance of python model
            dynamic pyMethodCall = PythonEngine.ModuleFromString("module", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")

from Python.EmbeddingTest import *

class PythonModel(TestMethodBinder.CSharpModel):
    def MethodCall(self):
        return self.InvokeModel(TestMethodBinder.TestImplicitConversion())
").GetAttr("PythonModel").Invoke();

            using (Py.GIL())
            {
                var data = (string)pyMethodCall.MethodCall();
                // we assert no implicit conversion took place
                Assert.AreEqual("TestImplicitConversion impl", data);
            }
        }

        public class CSharpModel
        {
            public virtual string OnlyClass(TestImplicitConversion data)
            {
                return "OnlyClass impl";
            }

            public virtual string OnlyString(string data)
            {
                return "OnlyString impl: " + data;
            }

            public virtual string InvokeModel(string data)
            {
                return "string impl: " + data;
            }

            public virtual string InvokeModel(TestImplicitConversion data)
            {
                return "TestImplicitConversion impl";
            }
        }

        public class TestImplicitConversion
        {
            public static implicit operator string(TestImplicitConversion symbol)
            {
                return "implicit to string";
            }
            public static implicit operator TestImplicitConversion(string symbol)
            {
                return new TestImplicitConversion();
            }
        }
    }
}
