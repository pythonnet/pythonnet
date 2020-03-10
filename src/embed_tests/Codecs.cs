namespace Python.EmbeddingTest {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Python.Runtime;
    using Python.Runtime.Codecs;

    public class Codecs {
        [SetUp]
        public void SetUp() {
            PythonEngine.Initialize();
        }

        [TearDown]
        public void Dispose() {
            PythonEngine.Shutdown();
        }

        [Test]
        public void ConversionsGeneric() {
            ConversionsGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void ConversionsGeneric<T, TTuple>() {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            T restored = default;
            using (Py.GIL())
            using (var scope = Py.CreateScope()) {
                void Accept(T value) => restored = value;
                var accept = new Action<T>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void ConversionsObject() {
            ConversionsObject<ValueTuple<int, string, object>, ValueTuple>();
        }
        static void ConversionsObject<T, TTuple>() {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            T restored = default;
            using (Py.GIL())
            using (var scope = Py.CreateScope()) {
                void Accept(object value) => restored = (T)value;
                var accept = new Action<object>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleRoundtripObject() {
            TupleRoundtripObject<ValueTuple<int, string, object>, ValueTuple>();
        }
        static void TupleRoundtripObject<T, TTuple>() {
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            using (Py.GIL()) {
                var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
                Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out object restored));
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleRoundtripGeneric() {
            TupleRoundtripGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void TupleRoundtripGeneric<T, TTuple>() {
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            using (Py.GIL()) {
                var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
                Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out T restored));
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void ListCodecTest()
        {
            var codec = ListCodec.Instance;
            var items = new List<PyObject>() { new PyInt(1), new PyInt(2), new PyInt(3) };

            var x = new PyList(items.ToArray());
            Assert.IsTrue(codec.CanDecode(x, typeof(List<int>)));
            Assert.IsTrue(codec.CanDecode(x, typeof(IList<bool>)));
            Assert.IsTrue(codec.CanDecode(x, typeof(System.Collections.IEnumerable)));
            Assert.IsTrue(codec.CanDecode(x, typeof(IEnumerable<int>)));
            Assert.IsTrue(codec.CanDecode(x, typeof(ICollection<float>)));
            Assert.IsFalse(codec.CanDecode(x, typeof(bool)));

            System.Collections.IEnumerable plainEnumerable = null;
            Assert.DoesNotThrow(() => { codec.TryDecode<System.Collections.IEnumerable>(x, out plainEnumerable); });
            Assert.IsNotNull(plainEnumerable);
            IList<object> list = null;
            list = plainEnumerable.Cast<object>().ToList();
            Assert.AreEqual(list.Count, 3);
            Assert.AreEqual(list[0], 1);
            Assert.AreEqual(list[1], 2);
            Assert.AreEqual(list[2], 3);
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

        public bool CanDecode(PyObject objectType, Type targetType)
            => objectType.Handle == TheOnlySupportedSourceType.Handle
               && targetType == typeof(TTarget);
        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            if (typeof(T) != typeof(TTarget))
                throw new ArgumentException(nameof(T));
            value = (T)(object)DecodeResult;
            return true;
        }
    }
}
