// We can't refer to or use Python.Runtime here.
// We want it to be loaded only inside the subdomains
using System;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;

namespace Python.DomainReloadTests
{
    /// <summary>
    /// This class provides an executable that can run domain reload tests.
    /// The setup is a bit complicated:
    /// 1. pytest runs test_*.py in this directory.
    /// 2. test_classname runs Python.DomainReloadTests.exe (this class) with an argument
    /// 3. This class at runtime creates a directory that has both C# and
    ///    python code, and compiles the C#.
    /// 4. This class then runs the C# code.
    ///
    /// But wait there's more indirection. The C# code that's run -- known as
    /// the test runner --
    /// This class compiles a DLL that contains the class which code will change
    /// and a runner executable that will run Python code referencing the class.
    /// Each test case:
    /// * Compiles some code, loads it into a domain, runs python that refers to it.
    /// * Unload the domain.
    /// * Compile a new piece of code, load it into a domain, run a new piece of python that accesses the code.
    /// * Unload the domain. Reload the domain, run the same python again.
    /// This class gets built into an executable which takes one argument:
    /// which test case to run. That's because pytest assumes we'll run
    /// everything in one process, but we really want a clean process on each
    /// test case to test the init/reload/teardown parts of the domain reload
    /// code.
    /// </summary>
    class TestRunner
    {
        const string TestAssemblyName = "DomainTests";

        class TestCase
        {
            /// <summary>
            /// The key to pass as an argument to choose this test.
            /// </summary>
            public string Name;

            /// <summary>
            /// The C# code to run in the first domain.
            /// </summary>
            public string DotNetBefore;

            /// <summary>
            /// The C# code to run in the second domain.
            /// </summary>
            public string DotNetAfter;

            /// <summary>
            /// The Python code to run as a module that imports the C#.
            /// It should have two functions: before() and after(). Before
            /// will be called when DotNetBefore is loaded; after will be
            /// called (twice) when DotNetAfter is loaded.
            /// To make the test fail, have those functions raise exceptions.
            ///
            /// Make sure there's no leading spaces since Python cares.
            /// </summary>
            public string PythonCode;
        }

        static TestCase[] Cases = new TestCase[]
        {
            new TestCase
            {
                Name = "class_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Before { }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class After { }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
from TestNamespace import Before

def before_reload():
    sys.my_cls = Before

def after_reload():
    try:
        sys.my_cls.Member()
    except AttributeError:
        print('Caught expected exception')
    else:
        raise AssertionError('Failed to throw exception')
                    ",
            },

            new TestCase 
            {
                Name = "static_member_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public static int Before() { return 5; } }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public static int After() { return 10; } }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Cls
    sys.my_fn = TestNamespace.Cls.Before
    sys.my_fn()
    TestNamespace.Cls.Before()

def after_reload():

    # We should have reloaded the class so we can access the new function.
    assert 10 == sys.my_cls.After()
    assert True is True

    try:
        # We should have reloaded the class. The old function still exists, but is now invalid.
        sys.my_cls.Before()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling class member that no longer exists')

    try:
        # We should have failed to reload the function which no longer exists.
        sys.my_fn()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling unbound .NET function that no longer exists')
                    ",
            },


            new TestCase 
            {
                Name = "member_rename",
                DotNetBefore = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public int Before() { return 5; } }
                    }",
                DotNetAfter = @"
                    namespace TestNamespace
                    {
                        [System.Serializable]
                        public class Cls { public int After() { return 10; } }
                    }",
                PythonCode = @"
import clr
import sys
clr.AddReference('DomainTests')
import TestNamespace

def before_reload():
    sys.my_cls = TestNamespace.Cls()
    sys.my_fn = TestNamespace.Cls().Before
    sys.my_fn()
    TestNamespace.Cls().Before()

def after_reload():

    # We should have reloaded the class so we can access the new function.
    assert 10 == sys.my_cls.After()
    assert True is True

    try:
        # We should have reloaded the class. The old function still exists, but is now invalid.
        sys.my_cls.Before()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling class member that no longer exists')

    try:
        # We should have failed to reload the function which no longer exists.
        sys.my_fn()
    except TypeError:
        print('Caught expected TypeError')
    else:
        raise AssertionError('Failed to throw exception: expected TypeError calling unbound .NET function that no longer exists')
                    ",
            },
        };

        /// <summary>
        /// The runner's code. Runs the python code
        /// This is a template for string.Format
        /// Arg 0 is the reload mode: ShutdownMode.Reload or other.
        /// Arg 1 is the no-arg python function to run, before or after.
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
            try
            {{
                PythonEngine.Initialize(mode:{0});
                using (Py.GIL())
                {{
                    // Because the generated assemblies are in the $TEMP folder, add it to the path
                    var temp = Path.GetTempPath();
                    dynamic sys = Py.Import(""sys"");
                    sys.path.append(new PyString(temp));
                    dynamic test_mod = Py.Import(""domain_test_module.mod"");
                    test_mod.{1}_reload();
                }}
                PythonEngine.Shutdown();
            }}
            catch (PythonException pe)
            {{
                throw new ArgumentException(message:pe.Message+""    ""+pe.StackTrace);
            }}
            catch (Exception e)
            {{
                Console.WriteLine(e.StackTrace);
                throw;
            }}
            return 0;
        }}
    }}
}}
";
        readonly static string PythonDllLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../runtime/bin/Python.Runtime.dll");

        public static int Main(string[] args)
        {
            TestCase testCase;
            if (args.Length < 1)
            {
                testCase = Cases[0];
            }
            else
            {
                string testName = args[0];
                Console.WriteLine($"-- Looking for domain reload test case {testName}");
                testCase = Cases.First(c => c.Name == testName);
            }
            Console.WriteLine($"-- Running domain reload test case: {testCase.Name}");

            var tempFolderPython = Path.Combine(Path.GetTempPath(), "Python.Runtime.dll");
            if (File.Exists(tempFolderPython))
            {
                File.Delete(tempFolderPython);
            }

            File.Copy(PythonDllLocation, tempFolderPython);
            
            CreatePythonModule(testCase);
            {
                var runnerAssembly = CreateCaseRunnerAssembly(verb:"before");
                CreateTestClassAssembly(testCase.DotNetBefore);
                {
                    var runnerDomain = CreateDomain("case runner before");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
                {
                    var runnerDomain = CreateDomain("case runner before (again)");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
            }

            {
                var runnerAssembly = CreateCaseRunnerAssembly(verb:"after");
                CreateTestClassAssembly(testCase.DotNetAfter);

                // Do it twice for good measure
                {
                    var runnerDomain = CreateDomain("case runner after");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
                {
                    var runnerDomain = CreateDomain("case runner after (again)");
                    RunAndUnload(runnerDomain, runnerAssembly);
                }
            }

            return 0;
        }

        static void RunAndUnload(AppDomain domain, string assemblyPath)
        {
            // Somehow the stack traces during execution sometimes have the wrong line numbers.
            // Add some info for when debugging is required.
            Console.WriteLine($"-- Running domain {domain.FriendlyName}");
            domain.ExecuteAssembly(assemblyPath);
            AppDomain.Unload(domain);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static string CreateTestClassAssembly(string code)
        {
            return CreateAssembly(TestAssemblyName + ".dll", code, exe: false);
        }

        static string CreateCaseRunnerAssembly(string verb, string shutdownMode = "ShutdownMode.Reload")
        {
            var code = string.Format(CaseRunnerTemplate, shutdownMode, verb);
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
                var stderr = System.Console.Error;
                stderr.WriteLine($"Error in {name} compiling:\n{code}");
                foreach (var error in results.Errors)
                {
                    stderr.WriteLine(error);
                }
                throw new ArgumentException("Error compiling code");
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

        static string CreatePythonModule(TestCase testCase)
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
                writer.Write(testCase.PythonCode);
            }

            return null;
        }

    }
}
