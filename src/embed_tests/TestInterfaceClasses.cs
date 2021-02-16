using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestInterfaceClasses
    {
        public string testCode = @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

testModule = TestInterfaceClasses.GetInstance()
print(testModule.Child.ChildBool)

";

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
        public void TestInterfaceDerivedClassMembers()
        {
            // This test gets an instance of the CSharpTestModule in Python
            // and then attempts to access it's member "Child"'s bool that is
            // not defined in the interface.
            PythonEngine.Exec(testCode);
        }

        public interface IInterface
        {
            bool InterfaceBool { get; set; }
        }

        public class Parent : IInterface
        {
            public bool InterfaceBool { get; set; }
            public bool ParentBool { get; set; }
        }

        public class Child : Parent
        {
            public bool ChildBool { get; set; }
        }

        public class CSharpTestModule
        {
            public IInterface Child;

            public CSharpTestModule()
            {
                Child = new Child
                {
                    ChildBool = true,
                    ParentBool = true,
                    InterfaceBool = true
                };
            }
        }

        public static CSharpTestModule GetInstance()
        {
            return new CSharpTestModule();
        }
    }
}
