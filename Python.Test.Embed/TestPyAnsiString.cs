using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyAnsiString
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
        public void TestStringCtor()
        {
            const string expected = "foo";
            var actual = new PyAnsiString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void TestEmptyStringCtor()
        {
            const string expected = "";
            var actual = new PyAnsiString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void TestPyObjectCtor()
        {
            const string expected = "Foo";

            var t = new PyAnsiString(expected);
            var actual = new PyAnsiString(t);

            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void TestBadPyObjectCtor()
        {
            var t = new PyInt(5);
            PyAnsiString actual = null;

            var ex = Assert.Throws<ArgumentException>(() => actual = new PyAnsiString(t));

            StringAssert.StartsWith("object is not a string", ex.Message);
            Assert.IsNull(actual);
        }

        [Test]
        public void TestCtorPtr()
        {
            const string expected = "foo";

            var t = new PyAnsiString(expected);
            Runtime.Runtime.XIncref(t.Handle);
            var actual = new PyAnsiString(t.Handle);

            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void IsStringTrue()
        {
            var t = new PyAnsiString("foo");

            Assert.True(PyAnsiString.IsStringType(t));
        }

        [Test]
        public void IsStringFalse()
        {
            var t = new PyInt(5);

            Assert.False(PyAnsiString.IsStringType(t));
        }

        [Test]
        [Ignore("Ambiguous behavior between PY2/PY3")]
        public void TestUnicode()
        {
            const string expected = "foo\u00e9";
            PyObject actual = new PyAnsiString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }
    }
}
