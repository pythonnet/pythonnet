using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Python.Runtime;

namespace Python.PerformanceTests
{
    public class BaselineComparisonBenchmarkBase
    {
        public BaselineComparisonBenchmarkBase()
        {
            Console.WriteLine($"CWD: {Environment.CurrentDirectory}");
            Console.WriteLine($"Using Python.Runtime from {typeof(PythonEngine).Assembly.Location} {typeof(PythonEngine).Assembly.GetName()}");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.FullName.StartsWith("Python.Runtime"))
                    Console.WriteLine(assembly.Location);
                foreach (var dependency in assembly.GetReferencedAssemblies())
                    if (dependency.FullName.Contains("Python.Runtime")) {
                        Console.WriteLine($"{assembly} -> {dependency}");
                    }
            }
            PythonEngine.Initialize();
        }

        static BaselineComparisonBenchmarkBase()
        {
            string pythonRuntimeDll = Environment.GetEnvironmentVariable(BaselineComparisonConfig.EnvironmentVariableName);
            if (string.IsNullOrEmpty(pythonRuntimeDll))
            {
                throw new ArgumentException(
                    "Required environment variable is missing",
                    BaselineComparisonConfig.EnvironmentVariableName);
            }

            Console.WriteLine("Preloading " + pythonRuntimeDll);
            Assembly.LoadFrom(pythonRuntimeDll);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.FullName.StartsWith("Python.Runtime"))
                    Console.WriteLine(assembly.Location);
                foreach(var dependency in assembly.GetReferencedAssemblies())
                    if (dependency.FullName.Contains("Python.Runtime")) {
                        Console.WriteLine($"{assembly} -> {dependency}");
                    }
            }
        }
    }
}
