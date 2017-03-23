//============================================================================
// This file replaces the hand-maintained stub that used to implement clr.dll.
// This is a line-by-line port from IL back to C#.
// We now use RGiesecke.DllExport on the required static init method so it can be
// loaded by a standard CPython interpreter as an extension module. When it
// is loaded, it bootstraps the managed runtime integration layer and defers
// to it to do initialization and put the clr module into sys.modules, etc.

// The "USE_PYTHON_RUNTIME_*" defines control what extra evidence is used
// to help the CLR find the appropriate Python.Runtime assembly.

// If defined, the "pythonRuntimeVersionString" variable must be set to
// Python.Runtime's current version.
#define USE_PYTHON_RUNTIME_VERSION

// If defined, the "PythonRuntimePublicKeyTokenData" data array must be
// set to Python.Runtime's public key token. (sn -T Python.Runtin.dll)
#define USE_PYTHON_RUNTIME_PUBLIC_KEY_TOKEN

// If DEBUG is defined in the Build Properties, a few Console.WriteLine
// calls are made to indicate what's going on during the load...
//============================================================================
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using RGiesecke.DllExport;

public class clrModule
{
#if PYTHON3
    [DllExport("PyInit_clr", CallingConvention.StdCall)]
    public static IntPtr PyInit_clr()
#elif PYTHON2
    [DllExport("initclr", CallingConvention.StdCall)]
    public static void initclr()
#endif
    {
        DebugPrint("Attempting to load 'Python.Runtime' using standard binding rules.");
#if USE_PYTHON_RUNTIME_PUBLIC_KEY_TOKEN
        var pythonRuntimePublicKeyTokenData = new byte[] { 0x50, 0x00, 0xfe, 0xa6, 0xcb, 0xa7, 0x02, 0xdd };
#endif

        // Attempt to find and load Python.Runtime using standard assembly binding rules.
        // This roughly translates into looking in order:
        // - GAC
        // - ApplicationBase
        // - A PrivateBinPath under ApplicationBase
        // With an unsigned assembly, the GAC is skipped.
        var pythonRuntimeName = new AssemblyName("Python.Runtime")
        {
#if USE_PYTHON_RUNTIME_VERSION
            // Has no effect until SNK works. Keep updated anyways.
            Version = new Version("2.4.0"),
#endif
            CultureInfo = CultureInfo.InvariantCulture
        };
#if USE_PYTHON_RUNTIME_PUBLIC_KEY_TOKEN
        pythonRuntimeName.SetPublicKeyToken(pythonRuntimePublicKeyTokenData);
#endif
        // We've got the AssemblyName with optional features; try to load it.
        Assembly pythonRuntime;
        try
        {
            pythonRuntime = Assembly.Load(pythonRuntimeName);
            DebugPrint("Success loading 'Python.Runtime' using standard binding rules.");
        }
        catch (IOException)
        {
            DebugPrint("'Python.Runtime' not found using standard binding rules.");
            try
            {
                // If the above fails for any reason, we fallback to attempting to load "Python.Runtime.dll"
                // from the directory this assembly is running in. "This assembly" is probably "clr.pyd",
                // sitting somewhere in PYTHONPATH.  This is using Assembly.LoadFrom, and inherits all the
                // caveats of that call.  See MSDN docs for details.
                // Suzanne Cook's blog is also an excellent source of info on this:
                // http://blogs.msdn.com/suzcook/
                // http://blogs.msdn.com/suzcook/archive/2003/05/29/57143.aspx
                // http://blogs.msdn.com/suzcook/archive/2003/06/13/57180.aspx

                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string assemblyDirectory = Path.GetDirectoryName(executingAssembly.Location);
                if (assemblyDirectory == null)
                {
                    throw new InvalidOperationException(executingAssembly.Location);
                }
                string pythonRuntimeDllPath = Path.Combine(assemblyDirectory, "Python.Runtime.dll");
                DebugPrint($"Attempting to load Python.Runtime from: '{pythonRuntimeDllPath}'.");
                pythonRuntime = Assembly.LoadFrom(pythonRuntimeDllPath);
                DebugPrint($"Success loading 'Python.Runtime' from: '{pythonRuntimeDllPath}'.");
            }
            catch (InvalidOperationException)
            {
                DebugPrint("Could not load 'Python.Runtime'.");
#if PYTHON3
                return IntPtr.Zero;
#elif PYTHON2
                return;
#endif
            }
        }

        // Once here, we've successfully loaded SOME version of Python.Runtime
        // So now we get the PythonEngine and execute the InitExt method on it.
        Type pythonEngineType = pythonRuntime.GetType("Python.Runtime.PythonEngine");

#if PYTHON3
        return (IntPtr)pythonEngineType.InvokeMember("InitExt", BindingFlags.InvokeMethod, null, null, null);
#elif PYTHON2
        pythonEngineType.InvokeMember("InitExt", BindingFlags.InvokeMethod, null, null, null);
#endif
    }

    /// <summary>
    /// Substitute for Debug.Writeline(...). Ideally we would use Debug.Writeline
    /// but haven't been able to configure the TRACE from within Python.
    /// </summary>
    [Conditional("DEBUG")]
    private static void DebugPrint(string str)
    {
        Console.WriteLine(str);
    }
}
