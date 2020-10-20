// We can't refer to or use Python.Runtime here.
// We want it to be loaded only inside the subdomains
using System;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.IO;

namespace Python.DomainReloadTests
{
    /// <summary>
    /// This class compiles a DLL that contains the class which code will change
    /// and a runner executable that will run Python code referencing the class.
    /// It's Main() will:
    /// * Run the runner and unlod it's domain
    /// * Modify and re-compile the test class
    /// * Re-run the runner and unload it twice
    /// </summary>
    class TestRunner
    {
        /// <summary>
        /// The code of the test class that changes
        /// </summary>
        const string ChangingClassTemplate = @"
using System;

namespace TestNamespace
{{
    [Serializable]
    public class TestClass
    {{
        {0}
        {1}
    }}
}}";

        /// <summary>
        /// The Python code that accesses the test class in the first step of the run
        /// </summary>
        const string PythonCodeStep1 = @"import clr
clr.AddReference('TestClass')
import sys
from TestNamespace import TestClass
foo = None
def do_work():
    global foo
    obj = TestClass()
    foo = TestClass.{0}
    sys.my_obj = foo
    print(sys.my_obj)
";

        /// <summary>
        /// The Python code that accesses the test class
        /// </summary>
        const string PythonCodeStep2 = @"import clr
clr.AddReference('TestClass')
import sys
from TestNamespace import TestClass
foo = None
def do_work():
    global foo
    print(foo)
    print(sys.my_obj)
";

        /// <summary>
        /// The runner's code. Runs the python code
        /// </summary>
        const string CaseRunnerTemplate = @"
using System;
using System.IO;
using Python.Runtime;
namespace CaseRunner
{{
    class CaseRunner
    {{
        public static int Main()
        {{
            PythonEngine.Initialize(mode:{0});
            try
            {{
                using (Py.GIL())
                {{
                    // Because the generated assemblies are in the $TEMP folder, add it to the path
                    var temp = Path.GetTempPath();
                    dynamic sys = Py.Import(""sys"");
                    sys.path.append(new PyString(temp));
                    dynamic test_mod = Py.Import(""domain_test_module.mod"");
                    test_mod.do_work();
                }}
                PythonEngine.Shutdown();
            }}
            catch (PythonException pe)
            {{
                throw new ArgumentException(message:pe.Message);
            }}
            return 0;
        }}
    }}
}}
";
        readonly static string PythonDllLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../runtime/bin/Python.Runtime.dll");

        public static int Main(string[] args)
        {
            Console.WriteLine($"Testing with arguments: {string.Join(", ", args)}");
            if (args.Length < 3)
            {
                return 123;
            }
            var tempFolderPython = Path.Combine(Path.GetTempPath(), "Python.Runtime.dll");
            if (File.Exists(tempFolderPython))
            {
                File.Delete(tempFolderPython);
            }

            File.Copy(PythonDllLocation, tempFolderPython);
            
            CreatePythonModule(string.Format(PythonCodeStep1, args[2]));
            var runnerAssembly = CreateCaseRunnerAssembly();
            {
                CreateTestClassAssembly(m1: args[0]);

                var runnerDomain = CreateDomain("case runner");
                RunAndUnload(runnerDomain, runnerAssembly);
            }

            // Re-create the python module to checkup on the members
            CreatePythonModule(PythonCodeStep2);

            {
                // remove the method
                CreateTestClassAssembly(m1: args[1]);

                // Do it twice for good measure
                {
                    var runnerDomain = CreateDomain("case runner 2");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
                {
                    var runnerDomain = CreateDomain("case runner 3");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
            }

            return 0;
        }

        static void RunAndUnload(AppDomain domain, string assemblyPath)
        {
            // Somehow the stack traces during execution sometimes have the wrong line numbers.
            // Add some info for when debugging is required.
            Console.WriteLine($"Runining domain {domain.FriendlyName}");
            domain.ExecuteAssembly(assemblyPath);
            AppDomain.Unload(domain);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static string CreateTestClassAssembly(string m1 = "", string m2 = "")
        {
            var name = "TestClass.dll";
            return CreateAssembly(name, string.Format(ChangingClassTemplate, m1, m2), exe: false);
        }

        static string CreateCaseRunnerAssembly(string shutdownMode = "ShutdownMode.Reload")
        {
            var code = string.Format(CaseRunnerTemplate, shutdownMode);
            var name = "TestCaseRunner.exe";
            return CreateAssembly(name, code, exe: true);
        }

        static string CreateAssembly(string name, string code, bool exe = false)
        {
            // Never return or hold the Assembly instance. This will cause
            // the assembly to be loaded into the current domain and this
            // interferes with the tests. The Domain can execute fine from a 
            // path, so let's return that.
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = exe;
            var assemblyName = name;
            var assemblyFullPath = Path.Combine(Path.GetTempPath(), assemblyName);
            parameters.OutputAssembly = assemblyFullPath;
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            parameters.ReferencedAssemblies.Add(PythonDllLocation);
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);
            if (results.NativeCompilerReturnValue != 0)
            {
                foreach (var error in results.Errors)
                {
                    System.Console.WriteLine(error);
                }
                throw new ArgumentException();
            }

            return assemblyFullPath;
        }

        static AppDomain CreateDomain(string name)
        {
            // Create the domain. Make sure to set PrivateBinPath to a relative
            // path from the CWD (namely, 'bin').
            // See https://stackoverflow.com/questions/24760543/createinstanceandunwrap-in-another-domain
            var currentDomain = AppDomain.CurrentDomain;
            var domainsetup = new AppDomainSetup()
            {
                ApplicationBase = Path.GetTempPath(),
                ConfigurationFile = currentDomain.SetupInformation.ConfigurationFile,
                LoaderOptimization = LoaderOptimization.SingleDomain,
                PrivateBinPath = "."
            };
            var domain = AppDomain.CreateDomain(
                    $"My Domain {name}",
                    currentDomain.Evidence,
                domainsetup);
            return domain;
        }

        static string CreatePythonModule(string code)
        {
            var modulePath = Path.Combine(Path.GetTempPath(), "domain_test_module");
            if (Directory.Exists(modulePath))
            {
                Directory.Delete(modulePath, recursive: true);
            }
            Directory.CreateDirectory(modulePath);

            File.Create(Path.Combine(modulePath, "__init__.py")).Close(); //Create and don't forget to close!
            using (var writer = File.CreateText(Path.Combine(modulePath, "mod.py")))
            {
                writer.Write(code);
            }

            return null;
        }

    }
}
