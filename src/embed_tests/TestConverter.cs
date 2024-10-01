using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using NUnit.Framework;

using Python.Runtime;

using PyRuntime = Python.Runtime.Runtime;

namespace Python.EmbeddingTest
{
    public class TestConverter
    {
        static readonly Type[] _numTypes = new Type[]
        {
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong)
        };

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
        public void TestConvertSingleToManaged(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.MinValue, float.MaxValue, float.NaN,
                float.Epsilon)] float testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat, typeof(float), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.IsTrue(((float) convertedValue).Equals(testValue));
        }

        [Test]
        public void TestConvertDoubleToManaged(
            [Values(double.PositiveInfinity, double.NegativeInfinity, double.MinValue, double.MaxValue, double.NaN,
                double.Epsilon)] double testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat, typeof(double), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.IsTrue(((double) convertedValue).Equals(testValue));
        }

        [Test]
        public void CovertTypeError()
        {
            Type[] floatTypes = new Type[]
            {
                typeof(float),
                typeof(double)
            };
            using (var s = new PyString("abc"))
            {
                foreach (var type in _numTypes.Union(floatTypes))
                {
                    object value;
                    try
                    {
                        bool res = Converter.ToManaged(s, type, out value, true);
                        Assert.IsFalse(res);
                        var bo = Exceptions.ExceptionMatches(Exceptions.TypeError);
                        Assert.IsTrue(Exceptions.ExceptionMatches(Exceptions.TypeError)
                            || Exceptions.ExceptionMatches(Exceptions.ValueError));
                    }
                    finally
                    {
                        Exceptions.Clear();
                    }
                }
            }
        }

        [Test]
        public void ConvertOverflow()
        {
            using (var num = new PyInt(ulong.MaxValue))
            {
                using var largeNum = PyRuntime.PyNumber_Add(num, num);
                try
                {
                    object value;
                    foreach (var type in _numTypes)
                    {
                        bool res = Converter.ToManaged(largeNum.BorrowOrThrow(), type, out value, true);
                        Assert.IsFalse(res);
                        Assert.IsTrue(Exceptions.ExceptionMatches(Exceptions.OverflowError));
                        Exceptions.Clear();
                    }
                }
                finally
                {
                    Exceptions.Clear();
                }
            }
        }

        [Test]
        public void NoImplicitConversionToBool()
        {
            var pyObj = new PyList(items: new[] { 1.ToPython(), 2.ToPython() }).ToPython();
            Assert.Throws<InvalidCastException>(() => pyObj.As<bool>());
        }

        [Test]
        public void ToNullable()
        {
            const int Const = 42;
            var i = new PyInt(Const);
            var ni = i.As<int?>();
            Assert.AreEqual(Const, ni);
        }

        [Test]
        public void BigIntExplicit()
        {
            BigInteger val = 42;
            var i = new PyInt(val);
            var ni = i.As<BigInteger>();
            Assert.AreEqual(val, ni);
            var nullable = i.As<BigInteger?>();
            Assert.AreEqual(val, nullable);
        }

        [Test]
        public void PyIntImplicit()
        {
            var i = new PyInt(1);
            var ni = (PyObject)i.As<object>();
            Assert.IsTrue(PythonReferenceComparer.Instance.Equals(i, ni));
        }

        [Test]
        public void ToPyList()
        {
            var list = new PyList();
            list.Append("hello".ToPython());
            list.Append("world".ToPython());
            var back = list.ToPython().As<PyList>();
            Assert.AreEqual(list.Length(), back.Length());
        }

        [Test]
        public void RawListProxy()
        {
            var list = new List<string> {"hello", "world"};
            var listProxy = PyObject.FromManagedObject(list);
            var clrObject = (CLRObject)ManagedType.GetManagedObject(listProxy);
            Assert.AreSame(list, clrObject.inst);
        }

        [Test]
        public void RawPyObjectProxy()
        {
            var pyObject = "hello world!".ToPython();
            var pyObjectProxy = PyObject.FromManagedObject(pyObject);
            var clrObject = (CLRObject)ManagedType.GetManagedObject(pyObjectProxy);
            Assert.AreSame(pyObject, clrObject.inst);

#pragma warning disable CS0612 // Type or member is obsolete
            const string handlePropertyName = nameof(PyObject.Handle);
#pragma warning restore CS0612 // Type or member is obsolete
            var proxiedHandle = pyObjectProxy.GetAttr(handlePropertyName).As<IntPtr>();
            Assert.AreEqual(pyObject.DangerousGetAddressOrNull(), proxiedHandle);
        }

        [Test]
        public void GenericToPython()
        {
            int i = 42;
            var pyObject = i.ToPythonAs<IConvertible>();
            var type = pyObject.GetPythonType();
            Assert.AreEqual(nameof(IConvertible), type.Name);
        }

        // regression for https://github.com/pythonnet/pythonnet/issues/451
        [Test]
        public void CanGetListFromDerivedClass()
        {
            using var scope = Py.CreateScope();
            scope.Import(typeof(GetListImpl).Namespace, asname: "test");
            scope.Exec(@"
class PyGetListImpl(test.GetListImpl):
    pass
    ");
            var pyImpl = scope.Get("PyGetListImpl");
            dynamic inst = pyImpl.Invoke();
            List<string> result = inst.GetList();
            CollectionAssert.AreEqual(new[] { "testing" }, result);
        }

        [Test]
        public void TestConvertNumpyFloat32ArrayToManaged()
        {
            var testValue = new float[] { 0, 1, 2, 3 };
            var nparr = np.arange(4, dtype: np.float32);

            object convertedValue;
            var converted = Converter.ToManaged(nparr, typeof(float[]), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(testValue, convertedValue);
        }

        [Test]
        public void TestConvertNumpyFloat64_2DArrayToManaged()
        {
            var testValue = new double[,] {{ 0, 1, 2, 3,}, { 4, 5, 6, 7 }, { 8, 9, 10, 11 }};
            var shape = new PyTuple(new[] {new PyInt(3), new PyInt(4)});
            var nparr = np.arange(12, dtype: np.float64).reshape(shape);

            object convertedValue;
            var converted = Converter.ToManaged(nparr, typeof(double[,]), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(testValue, convertedValue);
        }

        [Test]
        public void TestConvertBytearrayToManaged()
        {
            var testValue = Encoding.ASCII.GetBytes("test");
            using var str = PythonEngine.Eval("'test'.encode('ascii')");

            object convertedValue;
            var converted = Converter.ToManaged(str, typeof(byte[]), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(testValue, convertedValue);
        }

        [Test]
        [TestCaseSource(typeof(Arrays))]
        public void TestConvertArrayToManaged(string arrayType, Type t, object expected)
        {
            object convertedValue;
            var arr = array.array(arrayType.ToPython(), expected.ToPython());
            var converted = Converter.ToManaged(arr, t, out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(expected, convertedValue);
        }

        public class Arrays : System.Collections.IEnumerable
        {
            public System.Collections.IEnumerator GetEnumerator()
            {
                yield return new object[] { "b", typeof(byte[]), new byte[] { 0, 1, 2, 3, 4 } };
                yield return new object[] { "B", typeof(byte[]), new byte[] { 0, 1, 2, 3, 4 } };
                yield return new object[] { "u", typeof(char[]), new char[] { 'a', 'b', 'c', 'd', 'e' } };
                yield return new object[] { "h", typeof(short[]), new short[] { -2, -1, 0, 1, 2, 3, 4 } };
                yield return new object[] { "H", typeof(ushort[]), new ushort[] { 0, 1, 2, 3, 4 } };
                yield return new object[] { "i", typeof(int[]), new int[] { -2, -1, 0, 1, 2, 3, 4 } };
                yield return new object[] { "I", typeof(uint[]), new uint[] { 0, 1, 2, 3, 4 } };
                yield return new object[] { "q", typeof(long[]), new long[] { -2, -1, 0, 1, 2, 3, 4 } };
                yield return new object[] { "q", typeof(ulong[]), new ulong[] { 0, 1, 2, 3, 4 } };
                yield return new object[] { "f", typeof(float[]), new float[] { -2, -1, 0, 1, 2, 3, 4 } };
                yield return new object[] { "d", typeof(double[]), new double[] { -2, -1, 0, 1, 2, 3, 4 } };
            }
        };

        dynamic np
        {
            get
            {
                try
                {
                    return Py.Import("numpy");
                }
                catch (PythonException)
                {
                    Assert.Inconclusive("Numpy or dependency not installed");
                    return null;
                }
            }
        }

        dynamic array
        {
            get
            {
                try
                {
                    return Py.Import("array");
                }
                catch (PythonException)
                {
                    Assert.Inconclusive("Could not import array");
                    return null;
                }
            }
        }
    }

    public interface IGetList
    {
        List<string> GetList();
    }

    public class GetListImpl : IGetList
    {
        public List<string> GetList() => new() { "testing" };
    }
}
