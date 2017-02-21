using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyTupleTest
    {
        /// <summary>
        /// Test IsTupleType without having to Initialize a tuple.
        /// PyTuple constructor use IsTupleType. This decouples the tests.
        /// </summary>
        [Test]
        public void TestStringIsTupleType()
        {
            using (Py.GIL())
            {
                var s = new PyString("foo");
                Assert.IsFalse(PyTuple.IsTupleType(s));
            }
        }

        /// <summary>
        /// Test IsTupleType with Tuple.
        /// </summary>
        [Test]
        public void TestPyTupleIsTupleType()
        {
            using (Py.GIL())
            {
                var t = new PyTuple();
                Assert.IsTrue(PyTuple.IsTupleType(t));
            }
        }

        [Test]
        public void TestPyTupleEmpty()
        {
            using (Py.GIL())
            {
                var t = new PyTuple();
                Assert.AreEqual(0, t.Length());
            }
        }

        /// <remarks>
        /// FIXME: Unable to unload AppDomain, Unload thread timed out.
        /// Seen on Travis/AppVeyor on both PY2 and PY3. Causes Embedded_Tests
        /// to hang after they are finished for ~40 seconds until nunit3 forces
        /// a timeout on unloading tests. Doesn't fail the tests though but
        /// greatly slows down CI. nunit2 silently has this issue.
        /// </remarks>
        [Test]
        [Ignore("GH#397: Travis/AppVeyor: Unable to unload AppDomain, Unload thread timed out")]
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

        /// <remarks>
        /// FIXME: Possible source of intermittent AppVeyor PY27: Unable to unload AppDomain.
        /// </remarks>
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
