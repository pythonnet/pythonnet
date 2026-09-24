using System;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestClrTypeFilter
    {
        static readonly string RefusedName = typeof(FilterRefusedType).FullName!;

        readonly RefuseOneType _filter = new(typeof(FilterRefusedType));

        // The engine is already running, so the filter is added to the live
        // configuration, as Inheritance does with PythonBaseTypeProviders. That
        // is safe here because nothing else ever reflects FilterRefusedType, so
        // it cannot already be cached
        [OneTimeSetUp]
        public void SetUp() => PythonEngine.InteropConfiguration.ClrTypeFilters.Add(_filter);

        [OneTimeTearDown]
        public void TearDown() => PythonEngine.InteropConfiguration.ClrTypeFilters.Remove(_filter);

        [TestCase("host.Method()", TestName = "MethodReturn")]
        [TestCase("host.OutParam(None)", TestName = "OutParameterAndReturn")]
        [TestCase("host.OutOnly(None)", TestName = "OutParameterOnly")]
        [TestCase("list(host.Iterate())", TestName = "IteratorItem")]
        [TestCase("host.Property", TestName = "InstanceProperty")]
        [TestCase("type(host).StaticProperty", TestName = "StaticProperty")]
        [TestCase("host.Field", TestName = "InstanceField")]
        [TestCase("type(host).StaticField", TestName = "StaticField")]
        [TestCase("host.Array1[0]", TestName = "ArrayItem")]
        [TestCase("host.Array2[0, 0]", TestName = "MultidimensionalArrayItem")]
        [TestCase("host[0]", TestName = "IndexerResult")]
        [TestCase("host.AsInterface().__implementation__", TestName = "InterfaceImplementation")]
        [TestCase("host.AsInterface().__raw_implementation__", TestName = "InterfaceRawImplementation")]
        public void RefusedTypeRaisesCatchableTypeError(string expression)
        {
            using var scope = Py.CreateScope();
            scope.Set("host", new FilterHost());
            scope.Exec($@"
def attempt():
    try:
        {expression}
    except TypeError as e:
        return str(e)
    return 'nothing raised'
");
            Assert.That(scope.Eval<string>("attempt()"), Does.Contain(RefusedName));
        }

        [Test]
        public void PermittedTypesAreUnaffected()
        {
            using var scope = Py.CreateScope();
            scope.Set("host", new FilterHost());
            Assert.That(scope.Eval<int>("host.Permitted().Value"), Is.EqualTo(42));
        }

        [Test]
        public void UncaughtRefusalReachesTheCallerAsTypeError()
        {
            using var scope = Py.CreateScope();
            scope.Set("host", new FilterHost());

            var error = Assert.Throws<PythonException>(() => scope.Exec("host.Method()"));
            Assert.That(error.Type.Name, Is.EqualTo("TypeError"));
            Assert.That(error.Message, Does.Contain(RefusedName));
        }

        [Test]
        public void RefusedTypeIsCheckedOnEveryAttempt()
        {
            var counter = new CountingFilter(typeof(FilterRefusedType));
            var filters = PythonEngine.InteropConfiguration.ClrTypeFilters;
            filters.Insert(0, counter);
            try
            {
                using var scope = Py.CreateScope();
                scope.Set("host", new FilterHost());
                for (var i = 0; i < 2; i++)
                    Assert.Throws<PythonException>(() => scope.Exec("host.Method()"));

                // A refusal is never cached, so the filters see the type again
                Assert.That(counter.Count, Is.EqualTo(2));
            }
            finally
            {
                filters.Remove(counter);
            }
        }

        [Test]
        public void NoFiltersByDefault()
        {
            using var configuration = InteropConfiguration.MakeDefault();
            Assert.That(configuration.ClrTypeFilters, Is.Empty);
        }

        [Test]
        public void TypeIsReflectedOnlyIfEveryFilterPermitsIt()
        {
            var group = new ClrTypeFilterGroup();
            Assert.That(group.ShouldReflect(typeof(object)), Is.True, "an empty group permits everything");

            group.Add(new RefuseOneType(typeof(string)));
            Assert.That(group.ShouldReflect(typeof(object)), Is.True);
            Assert.That(group.ShouldReflect(typeof(string)), Is.False);

            group.Add(new RefuseOneType(typeof(object)));
            Assert.That(group.ShouldReflect(typeof(object)), Is.False);
        }

        sealed class RefuseOneType(Type refused) : IClrTypeFilter
        {
            public bool ShouldReflect(Type type) => type != refused;
        }

        sealed class CountingFilter(Type counted) : IClrTypeFilter
        {
            public int Count { get; private set; }

            public bool ShouldReflect(Type type)
            {
                if (type == counted)
                    Count++;
                return true;
            }
        }
    }

    /// <summary>Refused by <see cref="TestClrTypeFilter"/>; reflected nowhere else.</summary>
    public class FilterRefusedType : IFilterTarget { }

    public interface IFilterTarget { }

    public class FilterPermittedType
    {
        public int Value => 42;
    }

    /// <summary>Hands a refused type to Python through each boundary.</summary>
    public class FilterHost
    {
        public FilterRefusedType Method() => new();
        public int OutParam(out FilterRefusedType value) { value = new(); return 1; }
        public void OutOnly(out FilterRefusedType value) => value = new();
        public IEnumerable<FilterRefusedType> Iterate() { yield return new(); }
        public FilterRefusedType Property => new();
        public static FilterRefusedType StaticProperty => new();
        public FilterRefusedType Field = new();
        public static FilterRefusedType StaticField = new();
        public FilterRefusedType[] Array1 = { new() };
        public FilterRefusedType[,] Array2 = { { new() } };
        public FilterRefusedType this[int index] => new();
        public IFilterTarget AsInterface() => new FilterRefusedType();
        public FilterPermittedType Permitted() => new();
    }
}
