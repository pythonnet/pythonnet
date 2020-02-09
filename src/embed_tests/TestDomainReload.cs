using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Python.Runtime;

//
// This test case is disabled on .NET Standard because it doesn't have all the
// APIs we use. We could work around that, but .NET Core doesn't implement
// domain creation, so it's not worth it.
//
// Unfortunately this means no continuous integration testing for this case.
//
#if !NETSTANDARD && !NETCOREAPP
namespace Python.EmbeddingTest
{
    class TestDomainReload
    {
        /// <summary>
        /// Test that the python runtime can survive a C# domain reload without crashing.
        ///
        /// At the time this test was written, there was a very annoying
        /// seemingly random crash bug when integrating pythonnet into Unity.
        ///
        /// The repro steps that David Lassonde, Viktoria Kovecses and
        /// Benoit Hudson eventually worked out:
        /// 1. Write a HelloWorld.cs script that uses Python.Runtime to access
        ///     some C# data from python: C# calls python, which calls C#.
        /// 2. Execute the script (e.g. make it a MenuItem and click it).
        /// 3. Touch HelloWorld.cs on disk, forcing Unity to recompile scripts.
        /// 4. Wait several seconds for Unity to be done recompiling and
        ///     reloading the C# domain.
        /// 5. Make python run the gc (e.g. by calling gc.collect()).
        ///
        /// The reason:
        /// A. In step 2, Python.Runtime registers a bunch of new types with
        ///     their tp_traverse slot pointing to managed code, and allocates
        ///     some objects of those types.
        /// B. In step 4, Unity unloads the C# domain. That frees the managed
        ///     code. But at the time of the crash investigation, pythonnet
        ///     leaked the python side of the objects allocated in step 1.
        /// C. In step 5, python sees some pythonnet objects in its gc list of
        ///     potentially-leaked objects. It calls tp_traverse on those objects.
        ///     But tp_traverse was freed in step 3 => CRASH.
        ///
        /// This test distills what's going on without needing Unity around (we'd see
        /// similar behaviour if we were using pythonnet on a .NET web server that did
        /// a hot reload).
        /// </summary>
        [Test]
        public static void DomainReloadAndGC()
        {
            // We're set up to run in the directory that includes the bin directory.
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            Assembly pythonRunner1 = BuildAssembly("test1");
            RunAssemblyAndUnload(pythonRunner1, "test1");

            Assert.That(Runtime.Runtime.Py_IsInitialized() != 0,
                "On soft-shutdown mode, Python runtime should still running");

            // This caused a crash because objects allocated in pythonRunner1
            // still existed in memory, but the code to do python GC on those
            // objects is gone.
            Assembly pythonRunner2 = BuildAssembly("test2");
            RunAssemblyAndUnload(pythonRunner2, "test2");

            PythonEngine.Shutdown();
        }

        [Test]
        public static void CrossDomainObject()
        {
            IntPtr handle;
            Type type = typeof(Proxy);
            Assembly assembly = BuildAssembly("test_domain_reload");
            {
                AppDomain domain = CreateDomain("test_domain_reload");
                var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                        type.Assembly.FullName,
                        type.FullName);
                theProxy.InitAssembly(assembly.Location);
                theProxy.Call("InitPython", ShutdownMode.Reload);
                handle = (IntPtr)theProxy.Call("GetTestObject");
                theProxy.Call("ShutdownPython");
                AppDomain.Unload(domain);
            }

            {
                AppDomain domain = CreateDomain("test_domain_reload");
                var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                        type.Assembly.FullName,
                        type.FullName);
                theProxy.InitAssembly(assembly.Location);
                theProxy.Call("InitPython", ShutdownMode.Reload);

                // handle refering a clr object created in previous domain,
                // it should had been deserialized and became callable agian.
                theProxy.Call("RunTestObject", handle);
                theProxy.Call("ShutdownPythonCompletely");
                AppDomain.Unload(domain);
            }
            Assert.IsTrue(Runtime.Runtime.Py_IsInitialized() == 0);
        }

        /// <summary>
        /// Build an assembly out of the source code above.
        ///
        /// This creates a file <paramref name="assemblyName"/>.dll in order
        /// to support the statement "proxy.theAssembly = assembly" below.
        /// That statement needs a file, can't run via memory.
        /// </summary>
        static Assembly BuildAssembly(string assemblyName)
        {
            var provider = CodeDomProvider.CreateProvider("CSharp");
            var compilerparams = new CompilerParameters();
            var assemblies = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                             where !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location)
                             select assembly.Location;
            compilerparams.ReferencedAssemblies.AddRange(assemblies.ToArray());

            compilerparams.GenerateExecutable = false;
            compilerparams.GenerateInMemory = false;
            compilerparams.IncludeDebugInformation = false;
            compilerparams.OutputAssembly = assemblyName;

            var dir = Path.GetDirectoryName(new StackTrace(true).GetFrame(0).GetFileName());
            var results = provider.CompileAssemblyFromFile(compilerparams, Path.Combine(dir, "DomainCode.cs"));
            if (results.Errors.HasErrors)
            {
                var errors = new System.Text.StringBuilder("Compiler Errors:\n");
                foreach (CompilerError error in results.Errors)
                {
                    errors.AppendFormat("Line {0},{1}\t: {2}\n",
                            error.Line, error.Column, error.ErrorText);
                }
                throw new Exception(errors.ToString());
            }
            else
            {
                return results.CompiledAssembly;
            }
        }

        /// <summary>
        /// This is a magic incantation required to run code in an application
        /// domain other than the current one.
        /// </summary>
        class Proxy : MarshalByRefObject
        {
            Assembly theAssembly = null;

            public void InitAssembly(string assemblyPath)
            {
                theAssembly = Assembly.LoadFile(System.IO.Path.GetFullPath(assemblyPath));
            }

            public void RunPython()
            {
                Console.WriteLine("[Proxy] Entering RunPython");

                // Call into the new assembly. Will execute Python code
                var pythonrunner = theAssembly.GetType("PythonRunner");
                var runPythonMethod = pythonrunner.GetMethod("RunPython");
                runPythonMethod.Invoke(null, new object[] { });

                Console.WriteLine("[Proxy] Leaving RunPython");
            }

            public object Call(string methodName, params object[] args)
            {
                var pythonrunner = theAssembly.GetType("PythonRunner");
                var method = pythonrunner.GetMethod(methodName);
                return method.Invoke(null, args);
            }
        }

        /// <summary>
        /// Create a domain, run the assembly in it (the RunPython function),
        /// and unload the domain.
        /// </summary>
        static void RunAssemblyAndUnload(Assembly assembly, string assemblyName)
        {
            Console.WriteLine($"[Program.Main] === creating domain for assembly {assembly.FullName}");

            AppDomain domain = CreateDomain(assemblyName);
            // Create a Proxy object in the new domain, where we want the
            // assembly (and Python .NET) to reside
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Type type = typeof(Proxy);
            var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName);

            // From now on use the Proxy to call into the new assembly
            theProxy.InitAssembly(assemblyName);
            theProxy.RunPython();

            Console.WriteLine($"[Program.Main] Before Domain Unload on {assembly.FullName}");
            AppDomain.Unload(domain);
            Console.WriteLine($"[Program.Main] After Domain Unload on {assembly.FullName}");

            // Validate that the assembly does not exist anymore
            try
            {
                Console.WriteLine($"[Program.Main] The Proxy object is valid ({theProxy}). Unexpected domain unload behavior");
                Assert.Fail($"{theProxy} should be invlaid now");
            }
            catch (AppDomainUnloadedException)
            {
                Console.WriteLine("[Program.Main] The Proxy object is not valid anymore, domain unload complete.");
            }
        }

        private static AppDomain CreateDomain(string name)
        {
            // Create the domain. Make sure to set PrivateBinPath to a relative
            // path from the CWD (namely, 'bin').
            // See https://stackoverflow.com/questions/24760543/createinstanceandunwrap-in-another-domain
            var currentDomain = AppDomain.CurrentDomain;
            var domainsetup = new AppDomainSetup()
            {
                ApplicationBase = currentDomain.SetupInformation.ApplicationBase,
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

        /// <summary>
        /// Resolves the assembly. Why doesn't this just work normally?
        /// </summary>
        static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in loadedAssemblies)
            {
                if (assembly.FullName == args.Name)
                {
                    return assembly;
                }
            }

            return null;
        }
    }
}
#endif
