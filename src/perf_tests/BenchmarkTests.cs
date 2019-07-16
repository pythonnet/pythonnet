using System;
using System.IO;
using System.Reflection;

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
            Console.WriteLine(summary);
            Assert.Inconclusive();
        }

        public static string DeploymentRoot => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }
}
