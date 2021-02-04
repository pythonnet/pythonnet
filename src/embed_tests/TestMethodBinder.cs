using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestMethodBinder
    {
        private static dynamic module;
        private static string testModule = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *
class PythonModel(TestMethodBinder.CSharpModel):
    def TestA(self):
        return self.OnlyString(TestMethodBinder.TestImplicitConversion())
    def TestB(self):
            return self.OnlyClass('input string')
    def TestC(self):
        return self.InvokeModel('input string')
    def TestD(self):
        return self.InvokeModel(TestMethodBinder.TestImplicitConversion())
    def TestE(self, array):
        return array.Length == 2";


        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            module = PythonEngine.ModuleFromString("module", testModule).GetAttr("PythonModel").Invoke();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void ImplicitConversionToString()
        {
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyString impl: implicit to string", data);
            }

        [Test]
        public void ImplicitConversionToClass()
        {
            var data = (string)module.TestB();
                // we assert implicit conversion took place
                Assert.AreEqual("OnlyClass impl", data);
            }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_String()
        {
            var data = (string)module.TestC();
                // we assert no implicit conversion took place
                Assert.AreEqual("string impl: input string", data);
            }

        [Test]
        public void WillAvoidUsingImplicitConversionIfPossible_Class()
        {
            var data = (string)module.TestD();
                // we assert no implicit conversion took place
                Assert.AreEqual("TestImplicitConversion impl", data);
            
        }

        [Test]
        public void ArrayLength()
        {
                var array = new[] { "pepe", "pinocho" };
            var data = (bool)module.TestE(array);

            // Assert it is true
            Assert.AreEqual(true, data);
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
