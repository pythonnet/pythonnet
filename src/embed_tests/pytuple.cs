using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyTupleTest
    {
        [Test]
        public void TestPyTupleEmpty()
        {
            using (Py.GIL())
            {
                var t = new PyTuple();
                Assert.AreEqual(0, t.Length());
            }
        }

        [Test]
        public void TestPyTupleInvalidAppend()
        {
            using (Py.GIL())
            {
                PyObject s = new PyString("foo");
                var t = new PyTuple();
                Assert.Throws<PythonException>(() => t.Concat(s));
            }
        }

        [Test]
        public void TestPyTupleValidAppend()
        {
            using (Py.GIL())
            {
                var t0 = new PyTuple();
                var t = new PyTuple();
                t.Concat(t0);
                Assert.IsNotNull(t);
                Assert.IsInstanceOf(typeof(PyTuple), t);
            }
        }

        [Test]
        public void TestPyTupleIsTupleType()
        {
            using (Py.GIL())
            {
                var s = new PyString("foo");
                var t = new PyTuple();
                Assert.IsTrue(PyTuple.IsTupleType(t));
                Assert.IsFalse(PyTuple.IsTupleType(s));
            }
        }

        [Test]
        public void TestPyTupleStringConvert()
        {
            using (Py.GIL())
            {
                PyObject s = new PyString("foo");
                PyTuple t = PyTuple.AsTuple(s);
                Assert.IsNotNull(t);
                Assert.IsInstanceOf(typeof(PyTuple), t);
                Assert.AreEqual("f", t[0].ToString());
                Assert.AreEqual("o", t[1].ToString());
                Assert.AreEqual("o", t[2].ToString());
            }
        }

        [Test]
        public void TestPyTupleValidConvert()
        {
            using (Py.GIL())
            {
                var l = new PyList();
                PyTuple t = PyTuple.AsTuple(l);
                Assert.IsNotNull(t);
                Assert.IsInstanceOf(typeof(PyTuple), t);
            }
        }

        [Test]
        public void TestNewPyTupleFromPyTuple()
        {
            using (Py.GIL())
            {
                var t0 = new PyTuple();
                var t = new PyTuple(t0);
                Assert.IsNotNull(t);
                Assert.IsInstanceOf(typeof(PyTuple), t);
            }
        }
    }
}
