using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Python.Runtime;

namespace Python.Runtime
{
    /// <summary>
    /// Example of Embedding Python inside of a .NET program.
    /// </summary>
    /// <remarks>
    /// It has similar functionality to doing `import clr` from within Python, but this does it
    /// the other way around; That is, it loads Python inside of .NET program.
    /// See https://github.com/pythonnet/pythonnet/issues/358 for more info.
    /// </remarks>
    public sealed class PythonConsole
    {
#if NET40
        private static AssemblyLoader assemblyLoader = new AssemblyLoader();
#endif
        private PythonConsole()
        {
        }

        [STAThread]
        public static int Main(string[] args)
        {
            // Only net40 is capable to safely inject python.runtime.dll into resources.
#if NET40
            // reference the static assemblyLoader to stop it being optimized away
            AssemblyLoader a = assemblyLoader;
#endif
            string[] cmd = Environment.GetCommandLineArgs();
            PythonEngine.Initialize();

            int i = Runtime.Py_Main(cmd.Length, cmd);
            PythonEngine.Shutdown();

            return i;
        }

#if NET40
        // Register a callback function to load embedded assemblies.
        // (Python.Runtime.dll is included as a resource)
        private sealed class AssemblyLoader
        {
            private Dictionary<string, Assembly> loadedAssemblies;

            public AssemblyLoader()
            {
                loadedAssemblies = new Dictionary<string, Assembly>();

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    string shortName = args.Name.Split(',')[0];
                    string resourceName = $"{shortName}.dll";

                    if (loadedAssemblies.ContainsKey(resourceName))
                    {
                        return loadedAssemblies[resourceName];
                    }

                    // looks for the assembly from the resources and load it
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var assemblyData = new byte[stream.Length];
                            stream.Read(assemblyData, 0, assemblyData.Length);
                            Assembly assembly = Assembly.Load(assemblyData);
                            loadedAssemblies[resourceName] = assembly;
                            return assembly;
                        }
                    }
                    return null;
                };
            }
        }
#endif
    }
}
