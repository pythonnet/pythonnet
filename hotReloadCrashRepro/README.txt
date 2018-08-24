This project will reproduce the Python exception on second domain unload.
Make sure you use a version of Python .NET that calls PythonEngine.Shutdown() on DomainReload (starting with commit ###################)

How to repro:

1. Open hotReloadCrashRepro.csproj in Visual Studio 2017
2. Compile using the same platform as Python.Runtime.dll (e.g. x64)
3. Copy Python.Runtime.dll in the directory where hotReloadCrashRepro.exe is located (e.g. bin\x64\Debug)
4. Run "hotReloadCrashRepro.exe full_path_to_theAssembly.cs
    e.g. hotReloadCrashRepro.exe "D:\projects\pythonnet\hotReloadCrashRepro\theAssembly.cs"
    
The expected output is:    

[Program.Main] ===Pass #0===
[Program.Main] Creating the domain "My Domain 0"
[Program.Main] Building the assembly
[Proxy       ] In InitAssembly
[theAssembly ] PythonRunner.Init current domain = My Domain 0
[Proxy       ] In RunPython
[theAssembly ] In PythonRunner.RunPython
[Python      ] Done
[Program.Main] Before Domain Unload
[theAssembly ] In OnDomainUnload current domain = My Domain 0
[Program.Main] After Domain Unload
[Program.Main] The Proxy object is not valid anymore, domain unload complete.
[Program.Main] ===Pass #1===
[Program.Main] Creating the domain "My Domain 1"
[Proxy       ] In InitAssembly
[theAssembly ] PythonRunner.Init current domain = My Domain 1
[Proxy       ] In RunPython
[theAssembly ] In PythonRunner.RunPython
[Python      ] Done
[Program.Main] Before Domain Unload
[theAssembly ] In OnDomainUnload current domain = My Domain 1

Unhandled Exception: System.AppDomainUnloadedException: Attempted to access an unloaded AppDomain.
   at Python.Runtime.Runtime.Py_Finalize()
   at Python.Runtime.Runtime.Shutdown()
   at Python.Runtime.PythonEngine.Shutdown()
   at Python.Runtime.PythonEngine.OnDomainUnload(Object sender, EventArgs e)
[Program.Main] After Domain Unload
[Program.Main] The Proxy object is not valid anymore, domain unload complete.