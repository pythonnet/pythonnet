using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

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
        public void TestCtorPyObject()
        {
            var i = new PyInt(5);
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

            StringAssert.StartsWith("invalid literal for int", ex.Message);
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
            StringAssert.StartsWith("invalid literal for int", ex.Message);
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
            long val = 5 + (long)int.MaxValue;
            var a = new PyInt(val);
            Assert.IsInstanceOf(typeof(long), a.ToInt64());
            Assert.AreEqual(val, a.ToInt64());
        }

        [Test]
        public void ToBigInteger()
        {
            int[] simpleValues =
            {
                0, 1, 2,
                0x10,
                0x79,
                0x80,
                0x81,
                0xFF,
                0x123,
                0x8000,
                0x1234,
                0x8001,
                0x4000,
                0xFF,
            };
            simpleValues = simpleValues.Concat(simpleValues.Select(v => -v)).ToArray();

            var expected = simpleValues.Select(v => new BigInteger(v)).ToArray();
            var actual = simpleValues.Select(v => new PyInt(v).ToBigInteger()).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void CompareTo()
        {
            var v = new PyInt(42);

            #region Signed
            Assert.AreEqual(0, v.CompareTo(42L));
            Assert.AreEqual(0, v.CompareTo(42));
            Assert.AreEqual(0, v.CompareTo((short)42));
            Assert.AreEqual(0, v.CompareTo((sbyte)42));

            Assert.AreEqual(1, v.CompareTo(41L));
            Assert.AreEqual(1, v.CompareTo(41));
            Assert.AreEqual(1, v.CompareTo((short)41));
            Assert.AreEqual(1, v.CompareTo((sbyte)41));

            Assert.AreEqual(-1, v.CompareTo(43L));
            Assert.AreEqual(-1, v.CompareTo(43));
            Assert.AreEqual(-1, v.CompareTo((short)43));
            Assert.AreEqual(-1, v.CompareTo((sbyte)43));
            #endregion Signed

            #region Unsigned
            Assert.AreEqual(0, v.CompareTo(42UL));
            Assert.AreEqual(0, v.CompareTo(42U));
            Assert.AreEqual(0, v.CompareTo((ushort)42));
            Assert.AreEqual(0, v.CompareTo((byte)42));

            Assert.AreEqual(1, v.CompareTo(41UL));
            Assert.AreEqual(1, v.CompareTo(41U));
            Assert.AreEqual(1, v.CompareTo((ushort)41));
            Assert.AreEqual(1, v.CompareTo((byte)41));

            Assert.AreEqual(-1, v.CompareTo(43UL));
            Assert.AreEqual(-1, v.CompareTo(43U));
            Assert.AreEqual(-1, v.CompareTo((ushort)43));
            Assert.AreEqual(-1, v.CompareTo((byte)43));
            #endregion Unsigned
        }

        [Test]
        public void Equals()
        {
            var v = new PyInt(42);

            #region Signed
            Assert.True(v.Equals(42L));
            Assert.True(v.Equals(42));
            Assert.True(v.Equals((short)42));
            Assert.True(v.Equals((sbyte)42));

            Assert.False(v.Equals(41L));
            Assert.False(v.Equals(41));
            Assert.False(v.Equals((short)41));
            Assert.False(v.Equals((sbyte)41));
            #endregion Signed

            #region Unsigned
            Assert.True(v.Equals(42UL));
            Assert.True(v.Equals(42U));
            Assert.True(v.Equals((ushort)42));
            Assert.True(v.Equals((byte)42));

            Assert.False(v.Equals(41UL));
            Assert.False(v.Equals(41U));
            Assert.False(v.Equals((ushort)41));
            Assert.False(v.Equals((byte)41));
            #endregion Unsigned
        }

        [Test]
        public void ToBigIntegerLarge()
        {
            BigInteger val = BigInteger.Pow(2, 1024) + 3;
            var pyInt = new PyInt(val);
            Assert.AreEqual(val, pyInt.ToBigInteger());
            val = -val;
            pyInt = new PyInt(val);
            Assert.AreEqual(val, pyInt.ToBigInteger());
        }
    }
}
