using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyString
    {
        [Test]
        public void TestStringCtor()
        {
            const string expected = "foo";
            var actual = new PyString(expected);
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestEmptyStringCtor()
        {
            const string expected = "";
            var actual = new PyString(expected);
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [Ignore("Ambiguous behavior between PY2/PY3. Needs remapping")]
        public void TestPyObjectCtor()
        {
            const string expected = "Foo";

            var t = new PyString(expected);
            var actual = new PyString(t);

            Assert.That(actual.ToString(), Is.EqualTo(expected));
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
        public void TestCtorBorrowed()
        {
            const string expected = "foo";

            var t = new PyString(expected);
            var actual = new PyString(t.Reference);

            Assert.That(actual.ToString(), Is.EqualTo(expected));
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
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUnicodeSurrogateToString()
        {
            var expected = "foo\ud83d\udc3c";
            var actual = PythonEngine.Eval("'foo\ud83d\udc3c'");
            Assert.That(actual.Length(), Is.EqualTo(4));
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUnicodeSurrogate()
        {
            const string expected = "foo\ud83d\udc3c"; // "foo🐼"
            PyObject actual = new PyString(expected);
            // python treats "foo🐼" as 4 characters, dotnet as 5
            Assert.That(actual.Length(), Is.EqualTo(4));
            Assert.That(actual.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void CompareTo()
        {
            var a = new PyString("foo");

            Assert.That(a.CompareTo("foo"), Is.EqualTo(0));
            Assert.That(a.CompareTo("bar"), Is.EqualTo("foo".CompareTo("bar")));
            Assert.That(a.CompareTo("foz"), Is.EqualTo("foo".CompareTo("foz")));
        }

        [Test]
        public void Equals()
        {
            var a = new PyString("foo");

            Assert.True(a.Equals("foo"));
            Assert.False(a.Equals("bar"));
        }
    }
}
