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
", null, locals.Handle);

            PyObject a = locals.GetItem("a");

            IEnumerable<string> memberNames = a.GetDynamicMemberNames();

            foreach (string expectedName in expectedMemberNames)
            {
                Assert.IsTrue(memberNames.Contains(expectedName), "Could not find member '{0}'.", expectedName);
            }
        }
    }
}
