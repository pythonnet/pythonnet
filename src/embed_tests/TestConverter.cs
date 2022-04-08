using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
            Assert.AreEqual(i.rawPtr, ni.rawPtr);
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

            var proxiedHandle = pyObjectProxy.GetAttr("Handle").As<IntPtr>();
            Assert.AreEqual(pyObject.Handle, proxiedHandle);
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
