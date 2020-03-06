namespace Python.EmbeddingTest {
    using System;
    using System.Collections.Generic;
    using System.Text;
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
        public void ConversionsGeneric()
        {
            ConversionsGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void ConversionsGeneric<T, TTuple>()
        {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            T restored = default;
            using (Py.GIL())
            using (var scope = Py.CreateScope())
            {
                void Accept(T value) => restored = value;
                var accept = new Action<T>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void ConversionsObject()
        {
            ConversionsObject<ValueTuple<int, string, object>, ValueTuple>();
        }
        static void ConversionsObject<T, TTuple>()
        {
            TupleCodec<TTuple>.Register();
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            T restored = default;
            using (Py.GIL())
            using (var scope = Py.CreateScope())
            {
                void Accept(object value) => restored = (T)value;
                var accept = new Action<object>(Accept).ToPython();
                scope.Set(nameof(tuple), tuple);
                scope.Set(nameof(accept), accept);
                scope.Exec($"{nameof(accept)}({nameof(tuple)})");
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleRoundtripObject()
        {
            TupleRoundtripObject<ValueTuple<int, string, object>, ValueTuple>();
        }
        static void TupleRoundtripObject<T, TTuple>()
        {
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            using (Py.GIL())
            {
                var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
                Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out object restored));
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void TupleRoundtripGeneric()
        {
            TupleRoundtripGeneric<ValueTuple<int, string, object>, ValueTuple>();
        }

        static void TupleRoundtripGeneric<T, TTuple>()
        {
            var tuple = Activator.CreateInstance(typeof(T), 42, "42", new object());
            using (Py.GIL())
            {
                var pyTuple = TupleCodec<TTuple>.Instance.TryEncode(tuple);
                Assert.IsTrue(TupleCodec<TTuple>.Instance.TryDecode(pyTuple, out T restored));
                Assert.AreEqual(expected: tuple, actual: restored);
            }
        }

        [Test]
        public void FunctionAction()
        {
            FunctionCodec.Register();
            var codec = FunctionCodec.Instance;

            //decoding - python functions to C# actions
            {
                PyInt x = new PyInt(1);
                PyDict y = new PyDict();
                //non-callables can't be decoded into Action
                Assert.IsFalse(codec.CanDecode(x, typeof(Action)));
                Assert.IsFalse(codec.CanDecode(y, typeof(Action)));

                var locals = new PyDict();
                PythonEngine.Exec(@"
def foo():
    return 1
def bar(a):
    return 2
", null, locals.Handle);

                //foo, the function with no arguments
                var fooFunc = locals.GetItem("foo");
                Assert.IsFalse(codec.CanDecode(fooFunc, typeof(bool)));
                Assert.IsFalse(codec.CanDecode(fooFunc, typeof(Action<object[]>)));
                Assert.IsTrue(codec.CanDecode(fooFunc, typeof(Action)));

                Action fooAction;
                Assert.IsTrue(codec.TryDecode(fooFunc, out fooAction));
                Assert.DoesNotThrow(() => fooAction());

                //bar, the function with an argument
                var barFunc = locals.GetItem("bar");
                Assert.IsFalse(codec.CanDecode(barFunc, typeof(bool)));
                Assert.IsFalse(codec.CanDecode(barFunc, typeof(Action)));
                Assert.IsTrue(codec.CanDecode(barFunc, typeof(Action<object[]>)));

                Action<object[]> barAction;
                Assert.IsTrue(codec.TryDecode(barFunc, out barAction));
                Assert.DoesNotThrow(() => barAction(new[] { (object)true }));
            }

            //encoding, C# actions to python functions
            {
                //can't decode non-actions
                Assert.IsFalse(codec.CanEncode(typeof(int)));
                Assert.IsFalse(codec.CanEncode(typeof(Dictionary<string, int>)));

                Action foo = () => { };
                Assert.IsTrue(codec.CanEncode(foo.GetType()));

                Assert.DoesNotThrow(() => { codec.TryEncode(foo); });

                Action<object[]> bar = (object[] args) => { var z = args.Length; };
                Assert.IsTrue(codec.CanEncode(bar.GetType()));
                Assert.DoesNotThrow(() => { codec.TryEncode(bar); });
            }
        }
    }
}
