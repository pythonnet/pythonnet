using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Python.PerformanceTests
{
    public class BaselineComparisonConfig : ManualConfig
    {
        public const string EnvironmentVariableName = "PythonRuntimeDLL";

        public BaselineComparisonConfig()
        {
            this.Options |= ConfigOptions.DisableOptimizationsValidator;

            string deploymentRoot = BenchmarkTests.DeploymentRoot;

            this.Add(Job.Default
                .WithEnvironmentVariable(EnvironmentVariableName,
                    Path.Combine(deploymentRoot, "baseline", "Python.Runtime.dll"))
                .AsBaseline());
            this.Add(Job.Default
                .WithEnvironmentVariable(EnvironmentVariableName,
                    Path.Combine(deploymentRoot, "new", "Python.Runtime.dll")));
        }
    }
}
