using System;
using System.Collections.Generic;
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
                PythonEngine.Exec($@"
s = 0
for i in range(50000):
  s += a.{nameof(NetObject.LongProperty)}
", locals: locals.Handle);
            }
        }

        [Benchmark]
        public void WriteInt64Property() {
            using (Py.GIL()) {
                var locals = new PyDict();
                locals.SetItem("a", new NetObject().ToPython());
                PythonEngine.Exec($@"
s = 0
for i in range(50000):
  a.{nameof(NetObject.LongProperty)} += i
", locals: locals.Handle);
            }
        }
    }

    class NetObject
    {
        public long LongProperty { get; set; } = 42;
    }
}
