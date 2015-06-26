// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

//============================================================================
// This file replaces the  hand-maintained stub that used to implement clr.dll.
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

// If DEBUG_PRINT is defined in the Build Properties, a few System.Console.WriteLine
// calls are made to indicate what's going on during the load...
//============================================================================
using System;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
public class clrModule
// ReSharper restore InconsistentNaming
// ReSharper restore CheckNamespace
{
// ReSharper disable InconsistentNaming
#if (PYTHON32 || PYTHON33 || PYTHON34)
    [RGiesecke.DllExport.DllExport("PyInit_clr", System.Runtime.InteropServices.CallingConvention.StdCall)]
    public static IntPtr PyInit_clr()
#else
    [RGiesecke.DllExport.DllExport("initclr", System.Runtime.InteropServices.CallingConvention.StdCall)]
    public static void initclr()
#endif
// ReSharper restore InconsistentNaming    
    {
#if DEBUG_PRINT
        System.Console.WriteLine("Attempting to load Python.Runtime using standard binding rules... ");
#endif
#if USE_PYTHON_RUNTIME_PUBLIC_KEY_TOKEN
        var pythonRuntimePublicKeyTokenData = new byte[] { 0x50, 0x00, 0xfe, 0xa6, 0xcb, 0xa7, 0x02, 0xdd };
#endif

        // Attempt to find and load Python.Runtime using standard assembly binding rules.
        // This roughly translates into looking in order:
        // - GAC
        // - ApplicationBase
        // - A PrivateBinPath under ApplicationBase
        // With an unsigned assembly, the GAC is skipped.
        var pythonRuntimeName = new System.Reflection.AssemblyName("Python.Runtime")
            {
#if USE_PYTHON_RUNTIME_VERSION
                Version = new System.Version("4.0.0.1"), 
#endif
                CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
            };
#if USE_PYTHON_RUNTIME_PUBLIC_KEY_TOKEN
        pythonRuntimeName.SetPublicKeyToken(pythonRuntimePublicKeyTokenData);
#endif
        // We've got the AssemblyName with optional features; try to load it.
        System.Reflection.Assembly pythonRuntime;
        try
        {
            pythonRuntime = System.Reflection.Assembly.Load(pythonRuntimeName);
#if DEBUG_PRINT
            System.Console.WriteLine("Success!");
#endif
        }
        catch (System.IO.IOException)
        {
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

                var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                var assemblyDirectory = System.IO.Path.GetDirectoryName(executingAssembly.Location);
                if (assemblyDirectory == null)
                    throw new System.InvalidOperationException(executingAssembly.Location);
                var pythonRuntimeDllPath = System.IO.Path.Combine(assemblyDirectory, "Python.Runtime.dll");
#if DEBUG_PRINT
                System.Console.WriteLine("Attempting to load Python.Runtime from: '{0}'...", pythonRuntimeDllPath);
#endif
                pythonRuntime = System.Reflection.Assembly.LoadFrom(pythonRuntimeDllPath);
            }
            catch (System.InvalidOperationException) {
#if DEBUG_PRINT
                System.Console.WriteLine("Could not load Python.Runtime, so sad.");
#endif
#if (PYTHON32 || PYTHON33 || PYTHON34)
                return IntPtr.Zero;
#else
                return;
#endif
            }
        }

        // Once here, we've successfully loaded SOME version of Python.Runtime
		// So now we get the PythonEngine and execute the InitExt method on it.
        var pythonEngineType = pythonRuntime.GetType("Python.Runtime.PythonEngine");

#if (PYTHON32 || PYTHON33 || PYTHON34)
        return (IntPtr)pythonEngineType.InvokeMember("InitExt", System.Reflection.BindingFlags.InvokeMethod, null, null, null);
#else
        pythonEngineType.InvokeMember("InitExt", System.Reflection.BindingFlags.InvokeMethod, null, null, null);
#endif
    }
}
