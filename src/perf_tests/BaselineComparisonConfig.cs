using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using Perfolizer.Horology;

namespace Python.PerformanceTests
{
    public class BaselineComparisonConfig : ManualConfig
    {
        public const string EnvironmentVariableName = "PythonRuntimeDLL";

        public BaselineComparisonConfig()
        {
            this.Options |= ConfigOptions.DisableOptimizationsValidator;

            string deploymentRoot = BenchmarkTests.DeploymentRoot;

            var baseJob = Job.Default
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithMaxIterationCount(100)
                .WithIterationTime(TimeInterval.FromMilliseconds(100));
            this.Add(baseJob
                .WithId("baseline")
                .WithEnvironmentVariable(EnvironmentVariableName,
                    Path.Combine(deploymentRoot, "baseline", "Python.Runtime.dll"))
                .WithBaseline(true));
            this.Add(baseJob
                .WithId("new")
                .WithEnvironmentVariable(EnvironmentVariableName,
                    Path.Combine(deploymentRoot, "new", "Python.Runtime.dll")));
        }

        static BaselineComparisonConfig() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        }

        static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args) {
            Console.WriteLine(args.Name);
            if (!args.Name.StartsWith("Python.Runtime"))
                return null;
            string pythonRuntimeDll = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrEmpty(pythonRuntimeDll))
                pythonRuntimeDll = Path.Combine(BenchmarkTests.DeploymentRoot, "baseline", "Python.Runtime.dll");
            return Assembly.LoadFrom(pythonRuntimeDll);
        }
    }
}
