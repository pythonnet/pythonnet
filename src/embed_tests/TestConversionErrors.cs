using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// A failure while converting a CLR value for Python has to surface as a
    /// Python exception that Python code can catch, rather than as a CLR exception
    /// unwinding through the interpreter.
    /// </summary>
    public class TestConversionErrors
    {
        internal const string Failure = "conversion failed on purpose";

        [SetUp]
        public void SetUp() => PyObjectConversions.RegisterEncoder(new FailingConversionEncoder());

        [TearDown]
        public void TearDown() => PyObjectConversions.Reset();

        [TestCase("host.Method()", TestName = "MethodReturn")]
        [TestCase("host.OutParam(None)", TestName = "OutParameterAndReturn")]
        [TestCase("host.OutOnly(None)", TestName = "OutParameterOnly")]
        [TestCase("list(host.Iterate())", TestName = "IteratorItem")]
        [TestCase("host.Array1[0]", TestName = "ArrayItem")]
        [TestCase("host.Array2[0, 0]", TestName = "MultidimensionalArrayItem")]
        [TestCase("host[0]", TestName = "IndexerResult")]
        public void ConversionFailureIsCatchableInPython(string expression)
        {
            Assert.That(CaughtInPython(expression), Does.Contain(Failure));
        }

        [Test]
        public void ThrowingCurrentIsCatchableInPython()
        {
            Assert.That(
                CaughtInPython("list(host.ThrowingEnumerable())"),
                Does.Contain(ThrowingCurrentEnumerator.Message));
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> inside a Python try/except and
        /// returns what Python caught. If the failure escapes Python instead, the
        /// call throws and the test fails.
        /// </summary>
        static string CaughtInPython(string expression)
        {
            using var scope = Py.CreateScope();
            scope.Set("host", new ConversionErrorHost());
            scope.Exec($@"
def attempt():
    try:
        {expression}
    except Exception as e:
        return str(e)
    return 'nothing raised'
");
            return scope.Eval<string>("attempt()");
        }
    }

    public class UnconvertibleValue { }

    class FailingConversionEncoder : IPyObjectEncoder
    {
        public bool CanEncode(Type type) => type == typeof(UnconvertibleValue);

        public PyObject TryEncode(object value) =>
            throw new InvalidOperationException(TestConversionErrors.Failure);
    }

    /// <summary>Hands an unconvertible value to Python through each boundary.</summary>
    public class ConversionErrorHost
    {
        public UnconvertibleValue Method() => new();
        public int OutParam(out UnconvertibleValue value) { value = new(); return 1; }
        public void OutOnly(out UnconvertibleValue value) => value = new();
        public IEnumerable<UnconvertibleValue> Iterate() { yield return new(); }
        public UnconvertibleValue[] Array1 = { new() };
        public UnconvertibleValue[,] Array2 = { { new() } };
        public UnconvertibleValue this[int index] => new();
        public IEnumerable ThrowingEnumerable() => new ThrowingCurrentEnumerable();
    }

    class ThrowingCurrentEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => new ThrowingCurrentEnumerator();
    }

    class ThrowingCurrentEnumerator : IEnumerator
    {
        public const string Message = "Current failed on purpose";
        bool _moved;

        public object Current => throw new InvalidOperationException(Message);
        public bool MoveNext() => !_moved && (_moved = true);
        public void Reset() => _moved = false;
    }
}
