using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPySequence
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
        public void TestIsSequenceTrue()
        {
            var t = new PyString("FooBar");
            Assert.True(PySequence.IsSequenceType(t));
        }

        [Test]
        public void TestIsSequenceFalse()
        {
            var t = new PyInt(5);
            Assert.False(PySequence.IsSequenceType(t));
        }

        [Test]
        public void TestGetSlice()
        {
            var t = new PyString("FooBar");

            PyObject s = t.GetSlice(0, 3);
            Assert.AreEqual("Foo", s.ToString());

            PyObject s2 = t.GetSlice(3, 6);
            Assert.AreEqual("Bar", s2.ToString());

            PyObject s3 = t.GetSlice(0, 6);
            Assert.AreEqual("FooBar", s3.ToString());

            PyObject s4 = t.GetSlice(0, 12);
            Assert.AreEqual("FooBar", s4.ToString());
        }

        [Test]
        public void TestConcat()
        {
            var t1 = new PyString("Foo");
            var t2 = new PyString("Bar");

            PyObject actual = t1.Concat(t2);

            Assert.AreEqual("FooBar", actual.ToString());
        }

        [Test]
        public void TestRepeat()
        {
            var t1 = new PyString("Foo");

            PyObject actual = t1.Repeat(3);
            Assert.AreEqual("FooFooFoo", actual.ToString());

            actual = t1.Repeat(-3);
            Assert.AreEqual("", actual.ToString());
        }

        [Test]
        public void TestContains()
        {
            var t1 = new PyString("FooBar");

            Assert.True(t1.Contains(new PyString("a")));
            Assert.False(t1.Contains(new PyString("z")));
        }

        [Test]
        public void TestIndex()
        {
            var t1 = new PyString("FooBar");

            Assert.AreEqual(4, t1.Index(new PyString("a")));
            Assert.AreEqual(5, t1.Index(new PyString("r")));
            Assert.AreEqual(-1, t1.Index(new PyString("z")));
        }
    }
}
