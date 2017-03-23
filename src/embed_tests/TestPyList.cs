using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyList
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

            Assert.AreEqual("TypeError : 'int' object is not iterable", ex.Message);
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
            Assert.AreEqual(0, s.Length());
        }

        [Test]
        public void TestPyObjectArrayCtor()
        {
            var ai = new PyObject[] {new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);

            Assert.IsInstanceOf(typeof(PyList), s);
            Assert.AreEqual(3, s.Length());
            Assert.AreEqual("3", s[0].ToString());
            Assert.AreEqual("2", s[1].ToString());
            Assert.AreEqual("1", s[2].ToString());
        }

        [Test]
        public void TestPyObjectCtor()
        {
            var a = new PyList();
            var s = new PyList(a);

            Assert.IsInstanceOf(typeof(PyList), s);
            Assert.AreEqual(0, s.Length());
        }

        [Test]
        public void TestBadPyObjectCtor()
        {
            var i = new PyInt(5);
            PyList t = null;

            var ex = Assert.Throws<ArgumentException>(() => t = new PyList(i));

            Assert.AreEqual("object is not a list", ex.Message);
            Assert.IsNull(t);
        }

        [Test]
        public void TestAppend()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);
            s.Append(new PyInt(4));

            Assert.AreEqual(4, s.Length());
            Assert.AreEqual("4", s[3].ToString());
        }

        [Test]
        public void TestInsert()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
            var s = new PyList(ai);
            s.Insert(0, new PyInt(4));

            Assert.AreEqual(4, s.Length());
            Assert.AreEqual("4", s[0].ToString());
        }

        [Test]
        public void TestReverse()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
            var s = new PyList(ai);

            s.Reverse();

            Assert.AreEqual(3, s.Length());
            Assert.AreEqual("2", s[0].ToString());
            Assert.AreEqual("1", s[1].ToString());
            Assert.AreEqual("3", s[2].ToString());
        }

        [Test]
        public void TestSort()
        {
            var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
            var s = new PyList(ai);

            s.Sort();

            Assert.AreEqual(3, s.Length());
            Assert.AreEqual("1", s[0].ToString());
            Assert.AreEqual("2", s[1].ToString());
            Assert.AreEqual("3", s[2].ToString());
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

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("foo", result[0]);
            Assert.AreEqual("bar", result[1]);
            Assert.AreEqual("baz", result[2]);
        }
    }
}
