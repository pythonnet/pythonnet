using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyTuple
    {
        /// <summary>
        /// Test IsTupleType without having to Initialize a tuple.
        /// PyTuple constructor use IsTupleType. This decouples the tests.
        /// </summary>
        [Test]
        public void TestStringIsTupleType()
        {
            var s = new PyString("foo");
            Assert.False(PyTuple.IsTupleType(s));
        }

        /// <summary>
        /// Test IsTupleType with Tuple.
        /// </summary>
        [Test]
        public void TestPyTupleIsTupleType()
        {
            var t = new PyTuple();
            Assert.True(PyTuple.IsTupleType(t));
        }

        [Test]
        public void TestPyTupleEmpty()
        {
            var t = new PyTuple();
            Assert.That(t.Length(), Is.EqualTo(0));
        }

        [Test]
        public void TestPyTupleBadCtor()
        {
            var i = new PyInt(5);
            PyTuple t = null;

            var ex = Assert.Throws<ArgumentException>(() => t = new PyTuple(i));

            Assert.That(ex.Message, Is.EqualTo("object is not a tuple"));
            Assert.IsNull(t);
        }

        [Test]
        public void TestPyTupleCtorEmptyArray()
        {
            var a = new PyObject[] { };
            var t = new PyTuple(a);

            Assert.That(t.Length(), Is.EqualTo(0));
        }

        [Test]
        public void TestPyTupleCtorArrayPyIntEmpty()
        {
            var a = new PyInt[] { };
            var t = new PyTuple(a);

            Assert.That(t.Length(), Is.EqualTo(0));
        }

        [Test]
        public void TestPyTupleCtorArray()
        {
            var a = new PyObject[] { new PyInt(1), new PyString("Foo") };
            var t = new PyTuple(a);

            Assert.That(t.Length(), Is.EqualTo(2));
        }

        /// <summary>
        /// Test PyTuple.Concat(...) doesn't let invalid appends happen
        /// and throws and exception.
        /// </summary>
        /// <remarks>
        /// Test has second purpose. Currently it generated an Exception
        /// that the GC failed to remove often and caused AppDomain unload
        /// errors at the end of tests. See GH#397 for more info.
        /// </remarks>
        [Test]
        public void TestPyTupleInvalidAppend()
        {
            PyObject s = new PyString("foo");
            var t = new PyTuple();

            var ex = Assert.Throws<PythonException>(() => t.Concat(s));

            StringAssert.StartsWith("can only concatenate tuple", ex.Message);
            Assert.That(t.Length(), Is.EqualTo(0));
            Assert.IsEmpty(t);
        }

        [Test]
        public void TestPyTupleValidAppend()
        {
            var t0 = new PyTuple();
            var t = new PyTuple();
            t.Concat(t0);

            Assert.IsNotNull(t);
            Assert.IsInstanceOf(typeof(PyTuple), t);
        }

        [Test]
        public void TestPyTupleStringConvert()
        {
            PyObject s = new PyString("foo");
            PyTuple t = PyTuple.AsTuple(s);

            Assert.IsNotNull(t);
            Assert.IsInstanceOf(typeof(PyTuple), t);
            Assert.That(t[0].ToString(), Is.EqualTo("f"));
            Assert.That(t[1].ToString(), Is.EqualTo("o"));
            Assert.That(t[2].ToString(), Is.EqualTo("o"));
        }

        [Test]
        public void TestPyTupleValidConvert()
        {
            var l = new PyList();
            PyTuple t = PyTuple.AsTuple(l);

            Assert.IsNotNull(t);
            Assert.IsInstanceOf(typeof(PyTuple), t);
        }

        [Test]
        public void TestNewPyTupleFromPyTuple()
        {
            var t0 = new PyTuple();
            var t = new PyTuple(t0);

            Assert.IsNotNull(t);
            Assert.IsInstanceOf(typeof(PyTuple), t);
        }

        /// <remarks>
        /// TODO: Should this throw ArgumentError instead?
        /// </remarks>
        [Test]
        public void TestInvalidAsTuple()
        {
            var i = new PyInt(5);
            PyTuple t = null;

            var ex = Assert.Throws<PythonException>(() => t = PyTuple.AsTuple(i));

            Assert.That(ex.Message, Is.EqualTo("'int' object is not iterable"));
            Assert.IsNull(t);
        }
    }
}
