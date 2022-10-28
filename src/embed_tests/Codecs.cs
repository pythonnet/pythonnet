namespace Python.EmbeddingTest {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Python.Runtime;
    using Python.Runtime.Codecs;

    public class Codecs
    {
        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [TearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TupleConversionsGeneric()
        {
            TupleConversionsGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void TupleConversionsGeneric<T, TTuple>()
        {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            T restored = default;
            using (var scope = Py.CreateScope())
            {
                void Accept(T value) => restored = value;
                using var accept = new Action<T>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleConversionsObject()
        {
            TupleConversionsObject<ValueTuple<double, string, object>, ValueTuple>();
        }
        static void TupleConversionsObject<T, TTuple>()
        {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42.0, "42", new object());
            T restored = default;
            using (var scope = Py.CreateScope())
            {
                void Accept(object value) => restored = (T)value;
                using var accept = new Action<object>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleRoundtripObject()
        {
            TupleRoundtripObject<ValueTuple<double, string, object>, ValueTuple>();
        }
        static void TupleRoundtripObject<T, TTuple>()
        {
            var tuple = Activator.CreateInstance(typeof(T), 42.0, "42", new object());
            using var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
            Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out object restored));
            Assert.AreEqual(expected: tuple, actual: restored);
        }

        [Test]
        public void TupleRoundtripGeneric()
        {
            TupleRoundtripGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void TupleRoundtripGeneric<T, TTuple>()
        {
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            using var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
            Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out T restored));
            Assert.AreEqual(expected: tuple, actual: restored);
        }

        static PyObject GetPythonIterable() => PythonEngine.Eval("map(lambda x: x, [1,2,3])");

        [Test]
        public void ListDecoderTest()
        {
            var codec = ListDecoder.Instance;
            var items = new List<PyObject>() { new PyInt(1), new PyInt(2), new PyInt(3) };

            using var pyList = new PyList(items.ToArray());

            using var pyListType = pyList.GetPythonType();
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IList<bool>)));
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IList<int>)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(System.Collections.IEnumerable)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(IEnumerable<int>)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(ICollection<float>)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(bool)));

            //we'd have to copy into a list instance to do this, it would not be lossless.
            //lossy converters can be implemented outside of the python.net core library
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(List<int>)));

            //convert to list of int
            IList<int> intList = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out intList); });
            CollectionAssert.AreEqual(intList, new List<object> { 1, 2, 3 });

            //convert to list of string.   This will not work.
            //The ListWrapper class will throw a python exception when it tries to access any element.
            //TryDecode is a lossless conversion so there will be no exception at that point
            //interestingly, since the size of the python list can be queried without any conversion,
            //the IList will report a Count of 3.
            IList<string> stringList = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out stringList); });
            Assert.AreEqual(stringList.Count, 3);
            Assert.Throws(typeof(InvalidCastException), () => { var x = stringList[0]; });

            //can't convert python iterable to list (this will require a copy which isn't lossless)
            using var foo = GetPythonIterable();
            using var fooType = foo.GetPythonType();
            Assert.IsFalse(codec.CanDecode(fooType, typeof(IList<int>)));
        }

        [Test]
        public void SequenceDecoderTest()
        {
            var codec = SequenceDecoder.Instance;
            var items = new List<PyObject>() { new PyInt(1), new PyInt(2), new PyInt(3) };

            //SequenceConverter can only convert to any ICollection
            using var pyList = new PyList(items.ToArray());
            using var listType = pyList.GetPythonType();
            //it can convert a PyList, since PyList satisfies the python sequence protocol

            Assert.IsFalse(codec.CanDecode(listType, typeof(bool)));
            Assert.IsFalse(codec.CanDecode(listType, typeof(IList<int>)));
            Assert.IsFalse(codec.CanDecode(listType, typeof(System.Collections.IEnumerable)));
            Assert.IsFalse(codec.CanDecode(listType, typeof(IEnumerable<int>)));

            Assert.IsTrue(codec.CanDecode(listType, typeof(ICollection<float>)));
            Assert.IsTrue(codec.CanDecode(listType, typeof(ICollection<string>)));
            Assert.IsTrue(codec.CanDecode(listType, typeof(ICollection<int>)));

            //convert to collection of int
            ICollection<int> intCollection = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out intCollection); });
            CollectionAssert.AreEqual(intCollection, new List<object> { 1, 2, 3 });

            //no python exception should have occurred during the above conversion and check
            Runtime.CheckExceptionOccurred();

            //convert to collection of string.   This will not work.
            //The SequenceWrapper class will throw a python exception when it tries to access any element.
            //TryDecode is a lossless conversion so there will be no exception at that point
            //interestingly, since the size of the python sequence can be queried without any conversion,
            //the IList will report a Count of 3.
            ICollection<string> stringCollection = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out stringCollection); });
            Assert.AreEqual(3, stringCollection.Count());
            Assert.Throws(typeof(InvalidCastException), () => {
                string[] array = new string[3];
                stringCollection.CopyTo(array, 0);
            });

            Runtime.CheckExceptionOccurred();

            //can't convert python iterable to collection (this will require a copy which isn't lossless)
            //python iterables do not satisfy the python sequence protocol
            var foo = GetPythonIterable();
            var fooType = foo.GetPythonType();
            Assert.IsFalse(codec.CanDecode(fooType, typeof(ICollection<int>)));

            //python tuples do satisfy the python sequence protocol
            var pyTuple = new PyTuple(items.ToArray());
            var pyTupleType = pyTuple.GetPythonType();

            Assert.IsTrue(codec.CanDecode(pyTupleType, typeof(ICollection<float>)));
            Assert.IsTrue(codec.CanDecode(pyTupleType, typeof(ICollection<int>)));
            Assert.IsTrue(codec.CanDecode(pyTupleType, typeof(ICollection<string>)));

            //convert to collection of int
            ICollection<int> intCollection2 = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyTuple, out intCollection2); });
            CollectionAssert.AreEqual(intCollection2, new List<object> { 1, 2, 3 });

            //no python exception should have occurred during the above conversion and check
            Runtime.CheckExceptionOccurred();

            //convert to collection of string.   This will not work.
            //The SequenceWrapper class will throw a python exception when it tries to access any element.
            //TryDecode is a lossless conversion so there will be no exception at that point
            //interestingly, since the size of the python sequence can be queried without any conversion,
            //the IList will report a Count of 3.
            ICollection<string> stringCollection2 = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyTuple, out stringCollection2); });
            Assert.AreEqual(3, stringCollection2.Count());
            Assert.Throws(typeof(InvalidCastException), () => {
                string[] array = new string[3];
                stringCollection2.CopyTo(array, 0);
            });

            Runtime.CheckExceptionOccurred();

        }

        [Test]
        public void IterableDecoderTest()
        {
            var codec = IterableDecoder.Instance;
            var items = new List<PyObject>() { new PyInt(1), new PyInt(2), new PyInt(3) };

            var pyList = new PyList(items.ToArray());
            var pyListType = pyList.GetPythonType();
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(IList<bool>)));
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(System.Collections.IEnumerable)));
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IEnumerable<int>)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(ICollection<float>)));
            Assert.IsFalse(codec.CanDecode(pyListType, typeof(bool)));

            //ensure a PyList can be converted to a plain IEnumerable
            System.Collections.IEnumerable plainEnumerable1 = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out plainEnumerable1); });
            CollectionAssert.AreEqual(plainEnumerable1.Cast<PyInt>().Select(i => i.ToInt32()), new List<object> { 1, 2, 3 });

            //can convert to any generic ienumerable.  If the type is not assignable from the python element
            //it will lead to an empty iterable when decoding.  TODO - should it throw?
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IEnumerable<int>)));
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IEnumerable<double>)));
            Assert.IsTrue(codec.CanDecode(pyListType, typeof(IEnumerable<string>)));

            IEnumerable<int> intEnumerable = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out intEnumerable); });
            CollectionAssert.AreEqual(intEnumerable, new List<object> { 1, 2, 3 });

            Runtime.CheckExceptionOccurred();

            IEnumerable<double> doubleEnumerable = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out doubleEnumerable); });
            CollectionAssert.AreEqual(doubleEnumerable, new List<object> { 1, 2, 3 });

            Runtime.CheckExceptionOccurred();

            IEnumerable<string> stringEnumerable = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out stringEnumerable); });

            Assert.Throws(typeof(InvalidCastException), () => {
                foreach (string item in stringEnumerable)
                {
                    var x = item;
                }
            });
            Assert.Throws(typeof(InvalidCastException), () => {
                stringEnumerable.Count();
            });

            Runtime.CheckExceptionOccurred();

            //ensure a python class which implements the iterator protocol can be converter to a plain IEnumerable
            var foo = GetPythonIterable();
            var fooType = foo.GetPythonType();
            System.Collections.IEnumerable plainEnumerable2 = null;
            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out plainEnumerable2); });
            CollectionAssert.AreEqual(plainEnumerable2.Cast<PyInt>().Select(i => i.ToInt32()), new List<object> { 1, 2, 3 });

            //can convert to any generic ienumerable.  If the type is not assignable from the python element
            //it will be an exception during TryDecode
            Assert.IsTrue(codec.CanDecode(fooType, typeof(IEnumerable<int>)));
            Assert.IsTrue(codec.CanDecode(fooType, typeof(IEnumerable<double>)));
            Assert.IsTrue(codec.CanDecode(fooType, typeof(IEnumerable<string>)));

            Assert.DoesNotThrow(() => { codec.TryDecode(pyList, out intEnumerable); });
            CollectionAssert.AreEqual(intEnumerable, new List<object> { 1, 2, 3 });
        }

        // regression for https://github.com/pythonnet/pythonnet/issues/1427
        [Test]
        public void PythonRegisteredDecoder_NoStackOverflowOnSystemType()
        {
            const string PyCode = @"
import clr
import System
from Python.Runtime import PyObjectConversions
from Python.Runtime.Codecs import RawProxyEncoder


class ListAsRawEncoder(RawProxyEncoder):
    __namespace__ = 'Dummy'
    def CanEncode(self, clr_type):
        return clr_type.Name == 'IList`1' and clr_type.Namespace == 'System.Collections.Generic'


list_encoder = ListAsRawEncoder()
PyObjectConversions.RegisterEncoder(list_encoder)

system_type = list_encoder.GetType()";

            PythonEngine.Exec(PyCode);
        }

        const string TestExceptionMessage = "Hello World!";
        [Test]
        public void ExceptionEncoded()
        {
            PyObjectConversions.RegisterEncoder(new ValueErrorCodec());
            void CallMe() => throw new ValueErrorWrapper(TestExceptionMessage);
            var callMeAction = new Action(CallMe);
            using var scope = Py.CreateScope();
            scope.Exec(@"
def call(func):
  try:
    func()
  except ValueError as e:
    return str(e)
");
            var callFunc = scope.Get("call");
            string message = callFunc.Invoke(callMeAction.ToPython()).As<string>();
            Assert.AreEqual(TestExceptionMessage, message);
        }

        [Test]
        public void ExceptionDecoded()
        {
            PyObjectConversions.RegisterDecoder(new ValueErrorCodec());
            using var scope = Py.CreateScope();
            var error = Assert.Throws<ValueErrorWrapper>(()
                => PythonEngine.Exec($"raise ValueError('{TestExceptionMessage}')"));
            Assert.AreEqual(TestExceptionMessage, error.Message);
        }

        [Test]
        public void DateTimeDecoded()
        {
            using var scope = Py.CreateScope();
            scope.Exec(@"
import clr
from datetime import datetime


from Python.EmbeddingTest import Codecs, DateTimeDecoder

DateTimeDecoder.Setup()
");
            scope.Exec("Codecs.AcceptsDateTime(datetime(2021, 1, 22))");
        }

        [Test]
        public void FloatDerivedDecoded()
        {
            using var scope = Py.CreateScope();
            scope.Exec(@"class FloatDerived(float): pass");
            using var floatDerived = scope.Eval("FloatDerived");
            var decoder = new DecoderReturningPredefinedValue<object>(floatDerived, 42);
            PyObjectConversions.RegisterDecoder(decoder);
            using var result = scope.Eval("FloatDerived()");
            object decoded = result.As<object>();
            Assert.AreEqual(42, decoded);
        }

        [Test]
        public void ExceptionDecodedNoInstance()
        {
            PyObjectConversions.RegisterDecoder(new InstancelessExceptionDecoder());
            using var scope = Py.CreateScope();
            var error = Assert.Throws<ValueErrorWrapper>(() => PythonEngine.Exec(
                $"[].__iter__().__next__()"));
            Assert.AreEqual(TestExceptionMessage, error.Message);
        }

        public static void AcceptsDateTime(DateTime v) {}

        [Test]
        public void As_Object_AffectedByDecoders()
        {
            var everythingElseToSelf = new EverythingElseToSelfDecoder();
            PyObjectConversions.RegisterDecoder(everythingElseToSelf);

            var pyObj = PythonEngine.Eval("iter");
            var decoded = pyObj.As<object>();
            Assert.AreSame(everythingElseToSelf, decoded);
        }

        public class EverythingElseToSelfDecoder : IPyObjectDecoder
        {
            public bool CanDecode(PyType objectType, Type targetType)
            {
                return targetType.IsAssignableFrom(typeof(EverythingElseToSelfDecoder));
            }

            public bool TryDecode<T>(PyObject pyObj, out T value)
            {
                value = (T)(object)this;
                return true;
            }
        }

        class ValueErrorWrapper : Exception
        {
            public ValueErrorWrapper(string message) : base(message) { }
        }

        class ValueErrorCodec : IPyObjectEncoder, IPyObjectDecoder
        {
            public bool CanDecode(PyType objectType, Type targetType)
                => this.CanEncode(targetType)
                   && PythonReferenceComparer.Instance.Equals(objectType, PythonEngine.Eval("ValueError"));

            public bool CanEncode(Type type) => type == typeof(ValueErrorWrapper)
                                                || typeof(ValueErrorWrapper).IsSubclassOf(type);

            public bool TryDecode<T>(PyObject pyObj, out T value)
            {
                var message = pyObj.GetAttr("args")[0].As<string>();
                value = (T)(object)new ValueErrorWrapper(message);
                return true;
            }

            public PyObject TryEncode(object value)
            {
                var error = (ValueErrorWrapper)value;
                return PythonEngine.Eval("ValueError").Invoke(error.Message.ToPython());
            }
        }

        class InstancelessExceptionDecoder : IPyObjectDecoder, IDisposable
        {
            readonly PyObject PyErr = Py.Import("clr.interop").GetAttr("PyErr");

            public bool CanDecode(PyType objectType, Type targetType)
                => PythonReferenceComparer.Instance.Equals(PyErr, objectType);

            public void Dispose()
            {
                PyErr.Dispose();
            }

            public bool TryDecode<T>(PyObject pyObj, out T value)
            {
                if (pyObj.HasAttr("value"))
                {
                    value = default;
                    return false;
                }

                value = (T)(object)new ValueErrorWrapper(TestExceptionMessage);
                return true;
            }
        }
    }

    /// <summary>
    /// "Decodes" only objects of exact type <typeparamref name="T"/>.
    /// Result is just the raw proxy to the encoder instance itself.
    /// </summary>
    class ObjectToEncoderInstanceEncoder<T> : IPyObjectEncoder
    {
        public bool CanEncode(Type type) => type == typeof(T);
        public PyObject TryEncode(object value) => PyObject.FromManagedObject(this);
    }

    /// <summary>
    /// Decodes object of specified Python type to the predefined value <see cref="DecodeResult"/>
    /// </summary>
    /// <typeparam name="TTarget">Type of the <see cref="DecodeResult"/></typeparam>
    class DecoderReturningPredefinedValue<TTarget> : IPyObjectDecoder
    {
        public PyObject TheOnlySupportedSourceType { get; }
        public TTarget DecodeResult { get; }

        public DecoderReturningPredefinedValue(PyObject objectType, TTarget decodeResult)
        {
            this.TheOnlySupportedSourceType = objectType;
            this.DecodeResult = decodeResult;
        }

        public bool CanDecode(PyType objectType, Type targetType)
            => PythonReferenceComparer.Instance.Equals(objectType, TheOnlySupportedSourceType)
               && targetType == typeof(TTarget);
        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            if (typeof(T) != typeof(TTarget))
                throw new ArgumentException(nameof(T));
            value = (T)(object)DecodeResult;
            return true;
        }
    }

    public class DateTimeDecoder : IPyObjectDecoder
    {
        public static void Setup()
        {
            PyObjectConversions.RegisterDecoder(new DateTimeDecoder());
        }

        public bool CanDecode(PyType objectType, Type targetType)
        {
            return targetType == typeof(DateTime);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            var dt = new DateTime(
                pyObj.GetAttr("year").As<int>(),
                pyObj.GetAttr("month").As<int>(),
                pyObj.GetAttr("day").As<int>(),
                pyObj.GetAttr("hour").As<int>(),
                pyObj.GetAttr("minute").As<int>(),
                pyObj.GetAttr("second").As<int>());
            value = (T)(object)dt;
            return true;
        }
    }
}
