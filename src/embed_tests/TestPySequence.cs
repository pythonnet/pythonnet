using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPySequence
    {
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
            Assert.That(s.ToString(), Is.EqualTo("Foo"));

            PyObject s2 = t.GetSlice(3, 6);
            Assert.That(s2.ToString(), Is.EqualTo("Bar"));

            PyObject s3 = t.GetSlice(0, 6);
            Assert.That(s3.ToString(), Is.EqualTo("FooBar"));

            PyObject s4 = t.GetSlice(0, 12);
            Assert.That(s4.ToString(), Is.EqualTo("FooBar"));
        }

        [Test]
        public void TestConcat()
        {
            var t1 = new PyString("Foo");
            var t2 = new PyString("Bar");

            PyObject actual = t1.Concat(t2);

            Assert.That(actual.ToString(), Is.EqualTo("FooBar"));
        }

        [Test]
        public void TestRepeat()
        {
            var t1 = new PyString("Foo");

            PyObject actual = t1.Repeat(3);
            Assert.That(actual.ToString(), Is.EqualTo("FooFooFoo"));

            actual = t1.Repeat(-3);
            Assert.That(actual.ToString(), Is.EqualTo(""));
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

            Assert.That(t1.Index32(new PyString("a")), Is.EqualTo(4));
            Assert.That(t1.Index64(new PyString("r")), Is.EqualTo(5L));
            Assert.That(t1.Index(new PyString("z")), Is.EqualTo(-(nint)1));
        }
    }
}
