using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyString
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
            var actual = new PyString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void TestEmptyStringCtor()
        {
            const string expected = "";
            var actual = new PyString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        [Ignore("Ambiguous behavior between PY2/PY3. Needs remapping")]
        public void TestPyObjectCtor()
        {
            const string expected = "Foo";

            var t = new PyString(expected);
            var actual = new PyString(t);

            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        public void TestBadPyObjectCtor()
        {
            var t = new PyInt(5);
            PyString actual = null;

            var ex = Assert.Throws<ArgumentException>(() => actual = new PyString(t));

            StringAssert.StartsWith("object is not a string", ex.Message);
            Assert.IsNull(actual);
        }

        [Test]
        public void TestCtorPtr()
        {
            const string expected = "foo";

            var t = new PyString(expected);
            Runtime.Runtime.XIncref(t.Handle);
            var actual = new PyString(t.Handle);

            Assert.AreEqual(expected, actual.ToString());
        }

        [Test]
        [Ignore("Ambiguous behavior between PY2/PY3. Needs remapping")]
        public void IsStringTrue()
        {
            var t = new PyString("foo");

            Assert.True(PyString.IsStringType(t));
        }

        [Test]
        public void IsStringFalse()
        {
            var t = new PyInt(5);

            Assert.False(PyString.IsStringType(t));
        }

        [Test]
        public void TestUnicode()
        {
            const string expected = "foo\u00e9";
            PyObject actual = new PyString(expected);
            Assert.AreEqual(expected, actual.ToString());
        }
    }
}
