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

        [Test]
        public void TestConvertSingleToManaged(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.MinValue, float.MaxValue, float.NaN,
                float.Epsilon)] float testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat, typeof(float), out convertedValue, false);

            Assert.That(converted, Is.True);
            Assert.That(((float)convertedValue).Equals(testValue), Is.True);
        }

        [Test]
        public void TestConvertDoubleToManaged(
            [Values(double.PositiveInfinity, double.NegativeInfinity, double.MinValue, double.MaxValue, double.NaN,
                double.Epsilon)] double testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat, typeof(double), out convertedValue, false);

            Assert.That(converted, Is.True);
            Assert.That(((double)convertedValue).Equals(testValue), Is.True);
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
                        Assert.That(res, Is.False);
                        var bo = Exceptions.ExceptionMatches(Exceptions.TypeError);
                        Assert.That(Exceptions.ExceptionMatches(Exceptions.TypeError)
                            || Exceptions.ExceptionMatches(Exceptions.ValueError), Is.True);
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
                        Assert.That(res, Is.False);
                        Assert.That(Exceptions.ExceptionMatches(Exceptions.OverflowError), Is.True);
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
            Assert.That(ni, Is.EqualTo(Const));
        }

        [Test]
        public void BigIntExplicit()
        {
            BigInteger val = 42;
            var i = new PyInt(val);
            var ni = i.As<BigInteger>();
            Assert.That(ni, Is.EqualTo(val));
            var nullable = i.As<BigInteger?>();
            Assert.That(nullable, Is.EqualTo(val));
        }

        [Test]
        public void PyIntImplicit()
        {
            var i = new PyInt(1);
            var ni = (PyObject)i.As<object>();
            Assert.That(PythonReferenceComparer.Instance.Equals(i, ni), Is.True);
        }

        [Test]
        public void ToPyList()
        {
            var list = new PyList();
            list.Append("hello".ToPython());
            list.Append("world".ToPython());
            var back = list.ToPython().As<PyList>();
            Assert.That(back.Length(), Is.EqualTo(list.Length()));
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
            Assert.That(proxiedHandle, Is.EqualTo(pyObject.DangerousGetAddressOrNull()));
        }

        [Test]
        public void GenericToPython()
        {
            int i = 42;
            var pyObject = i.ToPythonAs<IConvertible>();
            var type = pyObject.GetPythonType();
            Assert.That(type.Name, Is.EqualTo(nameof(IConvertible)));
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
            Assert.That(result, Is.EqualTo(new[] { "testing" }).AsCollection);
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
