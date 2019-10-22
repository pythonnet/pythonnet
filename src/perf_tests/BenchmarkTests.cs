using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NUnit.Framework;

namespace Python.PerformanceTests
{
    public class BenchmarkTests
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            Environment.CurrentDirectory = Path.Combine(DeploymentRoot, "new");
        }

        [Test]
        public void PythonCallingNet()
        {
            var summary = BenchmarkRunner.Run<PythonCallingNetBenchmark>();

            Assert.IsNotEmpty(summary.Reports);
            Assert.IsTrue(summary.Reports.All(r => r.Success));

            double optimisticPerfRatio = GetOptimisticPerfRatio(summary.Reports);
            Assert.LessOrEqual(optimisticPerfRatio, 1.03);
        }

        static double GetOptimisticPerfRatio(IReadOnlyList<BenchmarkReport> reports) {
            var baseline = reports.Single(r => r.BenchmarkCase.Job.ResolvedId == "baseline").ResultStatistics;
            var @new = reports.Single(r => r.BenchmarkCase.Job.ResolvedId != "baseline").ResultStatistics;
            double newTimeOptimistic = @new.Mean - (@new.StandardDeviation + baseline.StandardDeviation) * 0.5;
            return newTimeOptimistic / baseline.Mean;
        }

        public static string DeploymentRoot => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }
}
