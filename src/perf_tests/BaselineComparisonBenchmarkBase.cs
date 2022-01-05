using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            try {
                PythonEngine.Initialize();
                Console.WriteLine("Python Initialized");
                Trace.Assert(PythonEngine.BeginAllowThreads() != IntPtr.Zero);
                Console.WriteLine("Threading enabled");
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        static BaselineComparisonBenchmarkBase()
        {
            SetupRuntimeResolve();
        }

        public static void SetupRuntimeResolve()
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

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        }

        static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args) {
            if (!args.Name.StartsWith("Python.Runtime"))
                return null;

            var preloaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Python.Runtime");
            if (preloaded != null) return preloaded;

            string pythonRuntimeDll = Environment.GetEnvironmentVariable(BaselineComparisonConfig.EnvironmentVariableName);
            if (string.IsNullOrEmpty(pythonRuntimeDll))
                return null;

            return Assembly.LoadFrom(pythonRuntimeDll);
        }
    }
}
