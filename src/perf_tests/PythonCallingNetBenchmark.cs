using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using BenchmarkDotNet.Attributes;
using Python.Runtime;

namespace Python.PerformanceTests
{
    [Config(typeof(BaselineComparisonConfig))]
    public class PythonCallingNetBenchmark: BaselineComparisonBenchmarkBase
    {
        [Benchmark]
        public void ReadInt64Property()
        {
            using (Py.GIL())
            {
                var locals = new PyDict();
                locals.SetItem("a", new NetObject().ToPython());
                Exec($@"
s = 0
for i in range(50000):
  s += a.{nameof(NetObject.LongProperty)}
", locals: locals);
            }
        }

        [Benchmark]
        public void WriteInt64Property() {
            using (Py.GIL()) {
                var locals = new PyDict();
                locals.SetItem("a", new NetObject().ToPython());
                Exec($@"
s = 0
for i in range(50000):
  a.{nameof(NetObject.LongProperty)} += i
", locals: locals);
            }
        }

        static void Exec(string code, PyDict locals)
        {
            MethodInfo exec = typeof(PythonEngine).GetMethod(nameof(PythonEngine.Exec));
            object localsArg = typeof(PyObject).Assembly.GetName().Version.Major >= 3
                ? locals : locals.Handle;
            exec.Invoke(null, new[]
            {
                code, localsArg, null
            });
        }
    }

    class NetObject
    {
        public long LongProperty { get; set; } = 42;
    }
}
