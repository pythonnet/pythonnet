using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyInt
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
        public void TestCtorInt()
        {
            const int i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorUInt()
        {
            const uint i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorLong()
        {
            const long i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorULong()
        {
            const ulong i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorShort()
        {
            const short i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorUShort()
        {
            const ushort i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorByte()
        {
            const byte i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorSByte()
        {
            const sbyte i = 5;
            var a = new PyInt(i);
            Assert.AreEqual(i, a.ToInt32());
        }

        [Test]
        public void TestCtorPtr()
        {
            var i = new PyInt(5);
            Runtime.Runtime.XIncref(i.Handle);
            var a = new PyInt(i.Handle);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorPyObject()
        {
            var i = new PyInt(5);
            Runtime.Runtime.XIncref(i.Handle);
            var a = new PyInt(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorBadPyObject()
        {
            var i = new PyString("Foo");
            PyInt a = null;

            var ex = Assert.Throws<ArgumentException>(() => a = new PyInt(i));

            StringAssert.StartsWith("object is not an int", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestCtorString()
        {
            const string i = "5";
            var a = new PyInt(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestCtorBadString()
        {
            const string i = "Foo";
            PyInt a = null;

            var ex = Assert.Throws<PythonException>(() => a = new PyInt(i));

            StringAssert.StartsWith("ValueError : invalid literal for int", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestIsIntTypeTrue()
        {
            var i = new PyInt(5);
            Assert.True(PyInt.IsIntType(i));
        }

        [Test]
        public void TestIsIntTypeFalse()
        {
            var s = new PyString("Foo");
            Assert.False(PyInt.IsIntType(s));
        }

        [Test]
        public void TestAsIntGood()
        {
            var i = new PyInt(5);
            var a = PyInt.AsInt(i);
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestAsIntBad()
        {
            var s = new PyString("Foo");
            PyInt a = null;

            var ex = Assert.Throws<PythonException>(() => a = PyInt.AsInt(s));
            StringAssert.StartsWith("ValueError : invalid literal for int", ex.Message);
            Assert.IsNull(a);
        }

        [Test]
        public void TestConvertToInt32()
        {
            var a = new PyInt(5);
            Assert.IsInstanceOf(typeof(int), a.ToInt32());
            Assert.AreEqual(5, a.ToInt32());
        }

        [Test]
        public void TestConvertToInt16()
        {
            var a = new PyInt(5);
            Assert.IsInstanceOf(typeof(short), a.ToInt16());
            Assert.AreEqual(5, a.ToInt16());
        }

        [Test]
        public void TestConvertToInt64()
        {
            var a = new PyInt(5);
            Assert.IsInstanceOf(typeof(long), a.ToInt64());
            Assert.AreEqual(5, a.ToInt64());
        }
    }
}
