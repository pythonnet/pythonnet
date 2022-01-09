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
            Assert.IsTrue(
                condition: this.summary.Reports.All(r => r.Success),
                message: "BenchmarkDotNet failed to execute or collect results of performance tests. See logs above.");
        }

        [Test]
        public void ReadInt64Property()
        {
            double optimisticPerfRatio = GetOptimisticPerfRatio(this.summary.Reports);
            AssertPerformanceIsBetterOrSame(optimisticPerfRatio, target: 1.35);
        }

        [Test]
        public void WriteInt64Property()
        {
            double optimisticPerfRatio = GetOptimisticPerfRatio(this.summary.Reports);
            AssertPerformanceIsBetterOrSame(optimisticPerfRatio, target: 1.25);
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

        public static void AssertPerformanceIsBetterOrSame(
            double actual, double target,
            double wiggleRoom = 1.1, [CallerMemberName] string testName = null) {
            double threshold = target * wiggleRoom;
            Assert.LessOrEqual(actual, threshold,
                $"{testName}: {actual:F3} > {threshold:F3} (target: {target:F3})"
                + ": perf result is higher than the failure threshold.");
        }
    }
}
