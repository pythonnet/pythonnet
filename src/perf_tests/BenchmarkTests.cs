using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;

using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using NUnit.Framework;

namespace Python.PerformanceTests
{
    public class BenchmarkTests
    {
        Summary summary;

        [OneTimeSetUp]
        public void SetUp()
        {
            Environment.CurrentDirectory = Path.Combine(DeploymentRoot, "new");
            this.summary = BenchmarkRunner.Run<PythonCallingNetBenchmark>();
            Assert.IsNotEmpty(this.summary.Reports);
            Assert.IsTrue(this.summary.Reports.All(r => r.Success));
        }

        [Test]
        public void ReadInt64Property()
        {
            double optimisticPerfRatio = GetOptimisticPerfRatio(this.summary.Reports);
            Assert.LessOrEqual(optimisticPerfRatio, 0.68);
        }

        [Test]
        public void WriteInt64Property()
        {
            double optimisticPerfRatio = GetOptimisticPerfRatio(this.summary.Reports);
            Assert.LessOrEqual(optimisticPerfRatio, 0.66);
        }

        static double GetOptimisticPerfRatio(
            IReadOnlyList<BenchmarkReport> reports,
            [CallerMemberName] string methodName = null)
        {
            reports = reports.Where(r => r.BenchmarkCase.Descriptor.WorkloadMethod.Name == methodName).ToArray();
            if (reports.Count == 0)
                throw new ArgumentException(
                    message: $"No reports found for {methodName}. "
                             + "You have to match test method name to benchmark method name or "
                             + "pass benchmark method name explicitly",
                    paramName: nameof(methodName));

            var baseline = reports.Single(r => r.BenchmarkCase.Job.ResolvedId == "baseline").ResultStatistics;
            var @new = reports.Single(r => r.BenchmarkCase.Job.ResolvedId != "baseline").ResultStatistics;

            double newTimeOptimistic = @new.Mean - (@new.StandardDeviation + baseline.StandardDeviation) * 0.5;

            return newTimeOptimistic / baseline.Mean;
        }

        public static string DeploymentRoot => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }
}
