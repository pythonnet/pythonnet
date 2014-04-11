// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;
using System.Collections.Generic;
using Python.Runtime;

namespace Python.Runtime {

public sealed class PythonConsole {

    private PythonConsole() {}

    [STAThread]
    public static int Main(string[] args) {
	// reference the static assemblyLoader to stop it being optimized away
        AssemblyLoader a = assemblyLoader;

        string [] cmd = Environment.GetCommandLineArgs();
        PythonEngine.Initialize();

        int i = Runtime.Py_Main(cmd.Length, cmd);
        PythonEngine.Shutdown();

        return i;
    }

    // Register a callback function to load embedded assmeblies.
    // (Python.Runtime.dll is included as a resource)
    private sealed class AssemblyLoader {
        Dictionary<string, Assembly> loadedAssemblies;

        public AssemblyLoader() {
            loadedAssemblies = new Dictionary<string, Assembly>();

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string shortName = args.Name.Split(',')[0];
                String resourceName = shortName + ".dll";

                if (loadedAssemblies.ContainsKey(resourceName)) {
                    return loadedAssemblies[resourceName];
                }

                // looks for the assembly from the resources and load it
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                    if (stream != null) {
                        Byte[] assemblyData = new Byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        Assembly assembly = Assembly.Load(assemblyData);
                        loadedAssemblies[resourceName] = assembly;
                        return assembly;
                    }
                }

                return null;
            };
        }
    };

    private static AssemblyLoader assemblyLoader = new AssemblyLoader();

};


}
