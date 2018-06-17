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
                "__class__",
                "__delattr__",
                "__dict__",
#if PYTHON3
                "__dir__",
#endif
                "__doc__",
#if PYTHON3
                "__eq__",
#endif
                "__format__",
#if PYTHON3
                "__ge__",
#endif
                "__getattribute__",
#if PYTHON3
                "__gt__",
#endif
                "__hash__",
                "__init__",
#if PYTHON36
                "__init_subclass__",
#endif
#if PYTHON3
                "__le__",
                "__lt__",
#endif
                "__module__",
#if PYTHON3
                "__ne__",
#endif
                "__new__",
                "__reduce__",
                "__reduce_ex__",
                "__repr__",
                "__setattr__",
                "__sizeof__",
                "__str__",
                "__subclasshook__",
                "__weakref__",
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

            IEnumerable<string> membernames = a.GetDynamicMemberNames();

            Assert.AreEqual(expectedMemberNames.Count, membernames.Count(), "Unexpected number of members.");

            IEnumerable<Tuple<string, string>> results = expectedMemberNames.Zip(membernames, (x, y) => new Tuple<string, string>(x, y));
            foreach (Tuple<string, string> result in results)
            {
                Assert.AreEqual(result.Item1, result.Item2, "Unexpected member name.");
            }
        }
    }
}
