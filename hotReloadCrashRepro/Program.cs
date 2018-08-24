using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace hotReloadCrashRepro
{
    class Program
    {
        /// <summary>
        /// Args goes as follows:
        ///     0: The full path to theAssembly.cs 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            string pathToTheAssembly = "";
            try
            {
                // Defaults if args are not specified (standard location when
                // building with Visual Studio 2017, using the x64 configuration)
                pathToTheAssembly = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                           @"..\..\..\theAssembly.cs");
            }
            catch (Exception)
            {
            }

            if (args.Length > 0)
            {
                pathToTheAssembly = args[0];
            }

            // The exception is thrown on the second call to Py_Finalize
            //
            // First time through, on the Python side we litter some objects
            // that Python figures someone still has a reference to, so it
            // keeps them around -- leak!
            //
            // Second time through, Python gc looks at the leaked objects and calls
            // tp_traverse on them. But the tp_traverse handler is C# code that got
            // destroyed in the domain unload -- crash!)
            Assembly theCompiledAssembly = null;
            for(int i = 0; i < 2; ++i) {
                // Create the domain
                System.Console.WriteLine(string.Format("[Program.Main] ===Pass #{0}===",i));
                System.Console.WriteLine(string.Format("[Program.Main] Creating the domain \"My Domain {0}\"",i));
                var domain = AppDomain.CreateDomain(string.Format("My Domain {0}",i));

                // Build the assembly only once (we reuse the same assembly)
                if (i == 0)
                {
                    System.Console.WriteLine("[Program.Main] Building the assembly");

                    // The assembly is compiled as a dll in the same directory as the Program executable
                    theCompiledAssembly = BuildAssembly(pathToTheAssembly, "TheCompiledAssembly.dll");
                }
              
                // Create a Proxy object in the new domain, where we want the
                // assembly (and Python .NET) to reside
                Type type = typeof(Proxy);
                var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName);

                // From now on use the Proxy to call into the new assembly
                theProxy.InitAssembly(theCompiledAssembly.Location);
                theProxy.RunPython();

                System.Console.WriteLine("[Program.Main] Before Domain Unload");
                AppDomain.Unload(domain);
                System.Console.WriteLine("[Program.Main] After Domain Unload");

                // Validate that the assembly does not exist anymore
                try
                {
                    System.Console.WriteLine(string.Format("[Program.Main] The Proxy object is valid ({0}). Unexpected domain unload behavior",theProxy));
                }
                catch (Exception)
                {
                    System.Console.WriteLine("[Program.Main] The Proxy object is not valid anymore, domain unload complete.");
                }
            }
        }

        public class Proxy : MarshalByRefObject
        {
            static Assembly theAssembly = null;

            public void InitAssembly(string assemblyPath)
            {
                System.Console.WriteLine(string.Format("[Proxy       ] In InitAssembly"));

                theAssembly = Assembly.LoadFile(assemblyPath);
                var pythonrunner = theAssembly.GetType("PythonRunner");
                var initMethod = pythonrunner.GetMethod("Init");
                initMethod.Invoke(null, new object[] {});
            }
            public void RunPython()
            {
                System.Console.WriteLine(string.Format("[Proxy       ] In RunPython"));

                // Call into the new assembly. Will execute Python code
                var pythonrunner = theAssembly.GetType("PythonRunner");
                var runPythonMethod = pythonrunner.GetMethod("RunPython");
                runPythonMethod.Invoke(null, new object[] { });
            }
        }

        static System.Reflection.Assembly BuildAssembly(string pathToTheAssembly, string outputAssemblyName)
        {   
            var provider = CodeDomProvider.CreateProvider("CSharp");
            var compilerparams = new CompilerParameters(new string [] {"Python.Runtime.dll"});

            compilerparams.GenerateExecutable = false;
            compilerparams.GenerateInMemory = false;
            compilerparams.IncludeDebugInformation = true;
            compilerparams.OutputAssembly = outputAssemblyName;

            var results = 
                provider.CompileAssemblyFromFile(compilerparams, pathToTheAssembly);
            if (results.Errors.HasErrors) {   
                StringBuilder errors = new StringBuilder("Compiler Errors :\r\n");
                foreach (CompilerError error in results.Errors )
                {   
                    errors.AppendFormat("Line {0},{1}\t: {2}\n", 
                            error.Line, error.Column, error.ErrorText);
                }
                throw new Exception(errors.ToString());
            } else {   
                return results.CompiledAssembly;
            }
        }
    }
}
