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
using Python.Runtime;

namespace Python.Runtime {

public sealed class PythonConsole {

    private PythonConsole() {}

    [STAThread]
    public static int Main(string[] args) {
        string [] cmd = Environment.GetCommandLineArgs();
        PythonEngine.Initialize();

        int i = Runtime.Py_Main(cmd.Length, cmd);
        PythonEngine.Shutdown();

        return i;
    }

    // Register a callback function to load embedded assmeblies.
    // (Python.Runtime.dll is included as a resource)
    private sealed class AssemblyLoader {
        public AssemblyLoader() {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                String resourceName = new AssemblyName(args.Name).Name + ".dll";

                // looks for the assembly from the resources and load it
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                    if (stream != null) {
                        Byte[] assemblyData = new Byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData);
                    }
                }

                return null;
            };
        }
    };

    private static AssemblyLoader assemblyLoader = new AssemblyLoader();

};


}
