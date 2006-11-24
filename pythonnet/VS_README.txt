I'm not the original author of Python for .Net, and when I first tried to compile it with VS2005, I had a lot of troubles, so I thought you might want to know how I managed to do it.

- Create a VS Solution.
- Add a new project "PythonRuntime"
- Add all files rom src/runtime in your project.
- Replace the Properties.AssemblyInfo.cs with the src/runtime/assemblyinfo.cs file.
- In importhook.cs, change the "ClrModule" reference to "ModuleObject". (I'm not sure if it breaks something somewhere, but it doesn't seem to break anything on the embedded side).
- You should then be able to compile it.

I only use Python for .Net to embed python, so I never tried, nor do I know how to compile CLR.dll.

I added test units for the modification I made to the embedded side. They are in src\embed_tests. You need NUnit to run them.

- Create a new project "UnitTests" in you previously created solution.
- Add a reference to NUnit.framework
- Add a reference to PythonRuntime
- Compile
- Open the nunit project and run it.

Virgil Dupras

