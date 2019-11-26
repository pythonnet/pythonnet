using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyLong
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
        public void TestToInt64()
        {
            long largeNumber = 8L * 1024L * 1024L * 1024L; // 8 GB
            var pyLargeNumber = new PyLong(largeNumber);
            Assert.AreEqual(largeNumber, pyLargeNumber.ToInt64());
        }

        [Test]
        public void TestCtorInt()
        {
            const int i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorUInt()
        {
            const uint i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorLong()
        {
            const long i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorULong()
        {
            const ulong i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorShort()
        {
            const short i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorUShort()
        {
            const ushort i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorByte()
        {
            const byte i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorSByte()
        {
            const sbyte i = 5;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorDouble()
        {
            double i = 5.0;
            var a = new PyLong(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorPtr()
        {
            var i = new PyLong(5);
            Runtime.Runtime.XIncref(i.Handle);
            var a = new PyLong(i.Handle);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorPyObject()
        {
            var i = new PyLong(5);
            Runtime.Runtime.XIncref(i.Handle);
            var a = new PyLong(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorBadPyObject()
        {
            var i = new PyString("Foo");
            PyLong a = null;

            var ex = Assert.Throws<ArgumentException>(() => a = new PyLong(i));

            StringAssert.StartsWith("object is not a long", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestCtorString()
        {
            const string i = "5";
            var a = new PyLong(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorBadString()
        {
            const string i = "Foo";
            PyLong a = null;

            var ex = Assert.Throws<PythonException>(() => a = new PyLong(i));

            StringAssert.StartsWith("ValueError : invalid literal", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestIsIntTypeTrue()
        {
            var i = new PyLong(5);
            Assert.True(PyLong.IsLongType(i));
        }

        [Test]
        public void TestIsLongTypeFalse()
        {
            var s = new PyString("Foo");
            Assert.False(PyLong.IsLongType(s));
        }

        [Test]
        public void TestAsLongGood()
        {
            var i = new PyLong(5);
            var a = PyLong.AsLong(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestAsLongBad()
        {
            var s = new PyString("Foo");
            PyLong a = null;

            var ex = Assert.Throws<PythonException>(() => a = PyLong.AsLong(s));
            StringAssert.StartsWith("ValueError : invalid literal", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestConvertToInt32()
        {
            var a = new PyLong(5);
            Assert.IsInstanceOf(typeof(int), a.ToInt32());
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestConvertToInt16()
        {
            var a = new PyLong(5);
            Assert.IsInstanceOf(typeof(short), a.ToInt16());
            Assert.AreEqual(5, a.ToInt16());
        }

        [Test]
        public void TestConvertToInt64()
        {
            var a = new PyLong(5);
            Assert.IsInstanceOf(typeof(long), a.ToInt64());
            Assert.AreEqual(5, a.ToInt64());
        }
    }
}
