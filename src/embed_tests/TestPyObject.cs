using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyObject
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
        public void TestGetDynamicMemberNames()
        {
            List<string> expectedMemberNames = new List<string>
            {
                "add",
                "getNumber",
                "member1",
                "member2"
            };

            PyDict locals = new PyDict();

            PythonEngine.Exec(@"
class MemberNamesTest(object):
    def __init__(self):
        self.member1 = 123
        self.member2 = 'Test string'

    def getNumber(self):
        return 123

    def add(self, x, y):
        return x + y

a = MemberNamesTest()
", null, locals);

            PyObject a = locals.GetItem("a");

            IEnumerable<string> memberNames = a.GetDynamicMemberNames();

            foreach (string expectedName in expectedMemberNames)
            {
                Assert.IsTrue(memberNames.Contains(expectedName), "Could not find member '{0}'.", expectedName);
            }
        }

        [Test]
        public void InvokeNull()
        {
            var list = PythonEngine.Eval("list");
            Assert.Throws<ArgumentNullException>(() => list.Invoke(new PyObject[] {null}));
        }

        [Test]
        public void AsManagedObjectInvalidCast()
        {
            var list = PythonEngine.Eval("list");
            Assert.Throws<InvalidCastException>(() => list.AsManagedObject(typeof(int)));
        }

        [Test]
        public void UnaryMinus_ThrowsOnBadType()
        {
            dynamic list = new PyList();
            var error = Assert.Throws<PythonException>(() => list = -list);
            Assert.AreEqual("TypeError", error.Type.Name);
        }

        [Test]
        [Obsolete]
        public void GetAttrDefault_IgnoresAttributeErrorOnly()
        {
            var ob = new PyObjectTestMethods().ToPython();
            using var fallback = new PyList();
            var attrErrResult = ob.GetAttr(nameof(PyObjectTestMethods.RaisesAttributeError), fallback);
            Assert.IsTrue(PythonReferenceComparer.Instance.Equals(fallback, attrErrResult));

            var typeErrResult = Assert.Throws<PythonException>(
                () => ob.GetAttr(nameof(PyObjectTestMethods.RaisesTypeError), fallback)
            );
            Assert.AreEqual(Exceptions.TypeError, typeErrResult.Type);
        }

        // regression test from https://github.com/pythonnet/pythonnet/issues/1642
        [Test]
        public void InheritedMethodsAutoacquireGIL()
        {
            PythonEngine.Exec("from System import String\nString.Format('{0},{1}', 1, 2)");
        }
    }

    public class PyObjectTestMethods
    {
        public string RaisesAttributeError => throw new PythonException(new PyType(Exceptions.AttributeError), value: null, traceback: null);
        public string RaisesTypeError => throw new PythonException(new PyType(Exceptions.TypeError), value: null, traceback: null);
    }
}
