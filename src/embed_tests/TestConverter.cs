using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public void ConvertListRoundTrip()
        {
            var list = new List<Type> { typeof(decimal), typeof(int) };
            var py = list.ToPython();
            object result;
            var converted = Converter.ToManaged(py, typeof(List<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, list);
        }

        [Test]
        public void GenericList()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var converted = Converter.ToManaged(py, typeof(IList<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(typeof(List<Type>), result.GetType());
            Assert.AreEqual(2, ((IReadOnlyCollection<Type>) result).Count);
            Assert.AreEqual(typeof(decimal), ((IReadOnlyCollection<Type>) result).ToList()[0]);
            Assert.AreEqual(typeof(int), ((IReadOnlyCollection<Type>) result).ToList()[1]);
        }

        [Test]
        public void ReadOnlyCollection()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var converted = Converter.ToManaged(py, typeof(IReadOnlyCollection<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(typeof(List<Type>), result.GetType());
            Assert.AreEqual(2, ((IReadOnlyCollection<Type>) result).Count);
            Assert.AreEqual(typeof(decimal), ((IReadOnlyCollection<Type>) result).ToList()[0]);
            Assert.AreEqual(typeof(int), ((IReadOnlyCollection<Type>) result).ToList()[1]);
        }

        [Test]
        public void ConvertPyListToArray()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var outputType = typeof(Type[]);
            var converted = Converter.ToManaged(py, outputType, out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, array);
            Assert.AreEqual(outputType, result.GetType());
        }

        [Test]
        public void ConvertInvalidDateTime()
        {
            var number = 10;
            var pyNumber = number.ToPython();

            object result;
            var converted = Converter.ToManaged(pyNumber, typeof(DateTime), out result, false);

            Assert.IsFalse(converted);
        }

        [Test]
        public void ConvertTimeSpanRoundTrip()
        {
            var timespan = new TimeSpan(0, 1, 0, 0);
            var pyTimedelta = timespan.ToPython();

            object result;
            var converted = Converter.ToManaged(pyTimedelta, typeof(TimeSpan), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, timespan);
        }

        [Test]
        public void ConvertDecimalPerformance()
        {
            var value = 1111111111.0001m;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 500000; i++)
            {
                var pyDecimal = value.ToPython();
                object result;
                var converted = Converter.ToManaged(pyDecimal, typeof(decimal), out result, false);
                if (!converted || result == null)
                {
                    throw new Exception("");
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Unspecified)]
        public void ConvertDateTimeRoundTripPerformance(DateTimeKind kind)
        {
            var datetime = new DateTime(2000, 1, 1, 2, 3, 4, 5, kind);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 500000; i++)
            {
                var pyDatetime = datetime.ToPython();
                object result;
                var converted = Converter.ToManaged(pyDatetime, typeof(DateTime), out result, false);
                if (!converted || result == null)
                {
                    throw new Exception("");
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [Test]
        public void ConvertDateTimeRoundTripNoTime()
        {
            var datetime = new DateTime(2000, 1, 1);
            var pyDatetime = datetime.ToPython();

            object result;
            var converted = Converter.ToManaged(pyDatetime, typeof(DateTime), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(datetime, result);
        }

        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Unspecified)]
        public void ConvertDateTimeRoundTrip(DateTimeKind kind)
        {
            var datetime = new DateTime(2000, 1, 1, 2, 3, 4, 5, kind);
            var pyDatetime = datetime.ToPython();

            object result;
            var converted = Converter.ToManaged(pyDatetime, typeof(DateTime), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(datetime, result);
        }

        [TestCase("")]
        [TestCase("America/New_York")]
        [TestCase("UTC")]
        public void ConvertDateTimeWithTimeZonePythonToCSharp(string timeZone)
        {
            const int year = 2024;
            const int month = 2;
            const int day = 27;
            const int hour = 12;
            const int minute = 30;
            const int second = 45;

            using (Py.GIL())
            {
                dynamic module = PyModule.FromString("module", @$"
from clr import AddReference
AddReference(""Python.EmbeddingTest"")
AddReference(""System"")

from Python.EmbeddingTest import *

from datetime import datetime
from pytz import timezone

tzinfo = timezone('{timeZone}') if '{timeZone}' else None

def GetPyDateTime():
    return datetime({year}, {month}, {day}, {hour}, {minute}, {second}, tzinfo=tzinfo) \
        if tzinfo else \
        datetime({year}, {month}, {day}, {hour}, {minute}, {second})

def GetNextDay(dateTime):
    return TestConverter.GetNextDay(dateTime)
");

                var pyDateTime = module.GetPyDateTime();
                var dateTimeResult = default(object);

                Assert.DoesNotThrow(() => Converter.ToManaged(pyDateTime, typeof(DateTime), out dateTimeResult, false));

                var managedDateTime = (DateTime)dateTimeResult;

                var expectedDateTime = new DateTime(year, month, day, hour, minute, second);
                Assert.AreEqual(expectedDateTime, managedDateTime);

                Assert.AreEqual(DateTimeKind.Unspecified, managedDateTime.Kind);
            }
        }

        [Test]
        public void ConvertDateTimeWithExplicitUTCTimeZonePythonToCSharp()
        {
            const int year = 2024;
            const int month = 2;
            const int day = 27;
            const int hour = 12;
            const int minute = 30;
            const int second = 45;

            using (Py.GIL())
            {
                var csDateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                // Converter.ToPython will set the datetime tzinfo to UTC using a custom tzinfo class
                using var pyDateTime = Converter.ToPython(csDateTime).MoveToPyObject();
                var dateTimeResult = default(object);

                Assert.DoesNotThrow(() => Converter.ToManaged(pyDateTime, typeof(DateTime), out dateTimeResult, false));

                var managedDateTime = (DateTime)dateTimeResult;

                var expectedDateTime = new DateTime(year, month, day, hour, minute, second);
                Assert.AreEqual(expectedDateTime, managedDateTime);

                Assert.AreEqual(DateTimeKind.Utc, managedDateTime.Kind);
            }
        }

        [Test]
        public void ConvertTimestampRoundTrip()
        {
            var timeSpan = new TimeSpan(1, 2, 3, 4, 5);
            var pyTimeSpan = timeSpan.ToPython();

            object result;
            var converted = Converter.ToManaged(pyTimeSpan, typeof(TimeSpan), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(timeSpan, result);
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

        [Test]
        public void PrimitiveIntConversion()
        {
            decimal value = 10;
            var pyValue = value.ToPython();

            // Try to convert python value to int
            var testInt = pyValue.As<int>();
            Assert.AreEqual(testInt , 10);
        }

        [TestCase(typeof(Type), true)]
        [TestCase(typeof(string), false)]
        [TestCase(typeof(TestCSharpModel), false)]
        public void NoErrorSetWhenFailingToConvertClassType(Type type, bool shouldConvert)
        {
            using var _ = Py.GIL();

            var module = PyModule.FromString("CallsCorrectOverloadWithoutErrors", @"
from clr import AddReference
AddReference(""System"")
AddReference(""Python.EmbeddingTest"")
from Python.EmbeddingTest import *

class TestPythonModel(TestCSharpModel):
    pass
");
            var testPythonModelClass = module.GetAttr("TestPythonModel");
            Assert.AreEqual(shouldConvert, Converter.ToManaged(testPythonModelClass, type, out var result, setError: false));
            Assert.IsFalse(Exceptions.ErrorOccurred());
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

    public class TestCSharpModel
    {
    }
}
