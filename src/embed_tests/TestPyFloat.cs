using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <remarks>
    /// PyFloat implementation isn't complete, thus tests aren't complete.
    /// </remarks>
    public class TestPyFloat
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
        public void FloatCtor()
        {
            const float a = 4.5F;
            var i = new PyFloat(a);
            Assert.True(PyFloat.IsFloatType(i));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void PyObjectCtorGood()
        {
            var i = new PyFloat(5);
            var a = new PyFloat(i);
            Assert.True(PyFloat.IsFloatType(a));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void PyObjectCtorBad()
        {
            var i = new PyString("Foo");
            PyFloat a = null;

            var ex = Assert.Throws<ArgumentException>(() => a = new PyFloat(i));

            StringAssert.StartsWith("object is not a float", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void DoubleCtor()
        {
            const double a = 4.5;
            var i = new PyFloat(a);
            Assert.True(PyFloat.IsFloatType(i));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void StringIntCtor()
        {
            const string a = "5";
            var i = new PyFloat(a);
            Assert.True(PyFloat.IsFloatType(i));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void StringDoubleCtor()
        {
            const string a = "4.5";
            var i = new PyFloat(a);
            Assert.True(PyFloat.IsFloatType(i));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void StringBadCtor()
        {
            const string i = "Foo";
            PyFloat a = null;

            var ex = Assert.Throws<PythonException>(() => a = new PyFloat(i));

            StringAssert.StartsWith("could not convert string to float", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void IsFloatTrue()
        {
            const double a = 4.5;
            var i = new PyFloat(a);
            Assert.True(PyFloat.IsFloatType(i));
        }

        [Test]
        public void IsFloatFalse()
        {
            var i = new PyString("Foo");
            Assert.False(PyFloat.IsFloatType(i));
        }

        [Test]
        public void AsFloatGood()
        {
            const double a = 4.5;
            var i = new PyFloat(a);
            PyFloat s = PyFloat.AsFloat(i);

            Assert.True(PyFloat.IsFloatType(s));
            // Assert.Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void AsFloatBad()
        {
            var s = new PyString("Foo");
            PyFloat a = null;

            var ex = Assert.Throws<PythonException>(() => a = PyFloat.AsFloat(s));
            StringAssert.StartsWith("could not convert string to float", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void CompareTo()
        {
            var v = new PyFloat(42);

            Assert.AreEqual(0, v.CompareTo(42f));
            Assert.AreEqual(0, v.CompareTo(42d));

            Assert.AreEqual(1, v.CompareTo(41f));
            Assert.AreEqual(1, v.CompareTo(41d));

            Assert.AreEqual(-1, v.CompareTo(43f));
            Assert.AreEqual(-1, v.CompareTo(43d));
        }

        [Test]
        public void Equals()
        {
            var v = new PyFloat(42);

            Assert.IsTrue(v.Equals(42f));
            Assert.IsTrue(v.Equals(42d));

            Assert.IsFalse(v.Equals(41f));
            Assert.IsFalse(v.Equals(41d));
        }
    }
}
