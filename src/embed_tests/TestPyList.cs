using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyList
    {
        [Test]
        public void TestStringIsListType()
        {
            var s = new PyString("foo");
            Assert.False(PyList.IsListType(s));
        }

        [Test]
        public void TestListIsListType()
        {
            var s = new PyList();
            Assert.True(PyList.IsListType(s));
        }

        [Test]
        public void TestStringAsListType()
        {
            var i = new PyInt(5);
            PyList t = null;

            var ex = Assert.Throws<PythonException>(() => t = PyList.AsList(i));

            Assert.That(ex.Message, Is.EqualTo("'int' object is not iterable"));
            Assert.IsNull(t);
        }

        [Test]
        public void TestListAsListType()
        {
            var l = new PyList();
            PyList t = PyList.AsList(l);

            Assert.IsNotNull(t);
            Assert.IsInstanceOf(typeof(PyList), t);
        }

        [Test]
        public void TestEmptyCtor()
        {
            var s = new PyList();

            Assert.IsInstanceOf(typeof(PyList), s);
            Assert.That(s.Length(), Is.EqualTo(0));
        }

        [Test]
        public void TestPyObjectArrayCtor()
        {
            var ai = new PyObject[] {new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);

            Assert.IsInstanceOf(typeof(PyList), s);
            Assert.That(s.Length(), Is.EqualTo(3));
            Assert.That(s[0].ToString(), Is.EqualTo("3"));
            Assert.That(s[1].ToString(), Is.EqualTo("2"));
            Assert.That(s[2].ToString(), Is.EqualTo("1"));
        }

        [Test]
        public void TestPyObjectCtor()
        {
            var a = new PyList();
            var s = new PyList(a);

            Assert.IsInstanceOf(typeof(PyList), s);
            Assert.That(s.Length(), Is.EqualTo(0));
        }

        [Test]
        public void TestBadPyObjectCtor()
        {
            var i = new PyInt(5);
            PyList t = null;

            var ex = Assert.Throws<ArgumentException>(() => t = new PyList(i));

            Assert.That(ex.Message, Is.EqualTo("object is not a list"));
            Assert.IsNull(t);
        }

        [Test]
        public void TestAppend()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);
            s.Append(new PyInt(4));

            Assert.That(s.Length(), Is.EqualTo(4));
            Assert.That(s[3].ToString(), Is.EqualTo("4"));
        }

        [Test]
        public void TestInsert()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);
            s.Insert(0, new PyInt(4));

            Assert.That(s.Length(), Is.EqualTo(4));
            Assert.That(s[0].ToString(), Is.EqualTo("4"));
        }

        [Test]
        public void TestReverse()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
            var s = new PyList(ai);

            s.Reverse();

            Assert.That(s.Length(), Is.EqualTo(3));
            Assert.That(s[0].ToString(), Is.EqualTo("2"));
            Assert.That(s[1].ToString(), Is.EqualTo("1"));
            Assert.That(s[2].ToString(), Is.EqualTo("3"));
        }

        [Test]
        public void TestSort()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
            var s = new PyList(ai);

            s.Sort();

            Assert.That(s.Length(), Is.EqualTo(3));
            Assert.That(s[0].ToString(), Is.EqualTo("1"));
            Assert.That(s[1].ToString(), Is.EqualTo("2"));
            Assert.That(s[2].ToString(), Is.EqualTo("3"));
        }

        [Test]
        public void TestOnPyList()
        {
            var list = new PyList();

            list.Append(new PyString("foo"));
            list.Append(new PyString("bar"));
            list.Append(new PyString("baz"));
            var result = new List<string>();
            foreach (PyObject item in list)
            {
                result.Add(item.ToString());
            }

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0], Is.EqualTo("foo"));
            Assert.That(result[1], Is.EqualTo("bar"));
            Assert.That(result[2], Is.EqualTo("baz"));
        }
    }
}
