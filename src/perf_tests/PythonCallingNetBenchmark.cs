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
        public void ReadIntProperty()
        {
            using (Py.GIL())
            {
                var locals = new PyDict();
                locals.SetItem("a", new NetObject().ToPython());
                PythonEngine.Exec(@"
s = 0
for i in range(1000000):
  s += a.IntProperty
");
            }
        }
    }

    class NetObject
    {
        public int IntProperty { get; set; } = 42;
    }
}
