using System;
using System.Collections.Generic;
using System.Reflection;
using Python.Runtime;

namespace Python.Runtime
{
    public sealed class PythonConsole
    {
        private static AssemblyLoader assemblyLoader = new AssemblyLoader();

        private PythonConsole()
        {
        }

        [STAThread]
        public static int Main(string[] args)
        {
            // reference the static assemblyLoader to stop it being optimized away
            AssemblyLoader a = assemblyLoader;

            string[] cmd = Environment.GetCommandLineArgs();
            PythonEngine.Initialize();

            int i = Runtime.Py_Main(cmd.Length, cmd);
            PythonEngine.Shutdown();

            return i;
        }

        // Register a callback function to load embedded assemblies.
        // (Python.Runtime.dll is included as a resource)
        private sealed class AssemblyLoader
        {
            Dictionary<string, Assembly> loadedAssemblies;

            public AssemblyLoader()
            {
                loadedAssemblies = new Dictionary<string, Assembly>();

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    string shortName = args.Name.Split(',')[0];
                    string resourceName = string.Format("{0}.dll", shortName);

                    if (loadedAssemblies.ContainsKey(resourceName))
                    {
                        return loadedAssemblies[resourceName];
                    }

                    // looks for the assembly from the resources and load it
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
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
    }
}
