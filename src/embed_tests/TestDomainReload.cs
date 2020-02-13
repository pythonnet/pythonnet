using System;
using System.Diagnostics;
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
            Assert.IsFalse(PythonEngine.IsInitialized);
            RunAssemblyAndUnload("test1");

            Assert.That(Runtime.Runtime.Py_IsInitialized() != 0,
                "On soft-shutdown mode, Python runtime should still running");

            RunAssemblyAndUnload("test2");
        }

        [Test]
        public static void CrossDomainObject()
        {
            IntPtr handle = IntPtr.Zero;
            Type type = typeof(Proxy);
            {
                AppDomain domain = CreateDomain("test_domain_reload");
                try
                {
                    var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                            type.Assembly.FullName,
                            type.FullName);
                    theProxy.Call("InitPython", ShutdownMode.Reload);
                    handle = (IntPtr)theProxy.Call("GetTestObject");
                    theProxy.Call("ShutdownPython");
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }

            {
                AppDomain domain = CreateDomain("test_domain_reload");
                try
                {
                    var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                            type.Assembly.FullName,
                            type.FullName);
                    theProxy.Call("InitPython", ShutdownMode.Reload);

                    // handle refering a clr object created in previous domain,
                    // it should had been deserialized and became callable agian.
                    theProxy.Call("RunTestObject", handle);
                    theProxy.Call("ShutdownPythonCompletely");
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }
            if (PythonEngine.DefaultShutdownMode == ShutdownMode.Normal)
            {
                Assert.IsTrue(Runtime.Runtime.Py_IsInitialized() == 0);
            }
        }

        /// <summary>
        /// This is a magic incantation required to run code in an application
        /// domain other than the current one.
        /// </summary>
        class Proxy : MarshalByRefObject
        {
            public void RunPython()
            {
                Console.WriteLine("[Proxy] Entering RunPython");
                PythonRunner.RunPython();
                Console.WriteLine("[Proxy] Leaving RunPython");
            }

            public object Call(string methodName, params object[] args)
            {
                var pythonrunner = typeof(PythonRunner);
                var method = pythonrunner.GetMethod(methodName);
                return method.Invoke(null, args);
            }
        }

        /// <summary>
        /// Create a domain, run the assembly in it (the RunPython function),
        /// and unload the domain.
        /// </summary>
        static void RunAssemblyAndUnload(string domainName)
        {
            Console.WriteLine($"[Program.Main] === creating domain {domainName}");

            AppDomain domain = CreateDomain(domainName);
            // Create a Proxy object in the new domain, where we want the
            // assembly (and Python .NET) to reside
            Type type = typeof(Proxy);
            var theProxy = (Proxy)domain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName);

            // From now on use the Proxy to call into the new assembly
            theProxy.RunPython();

            Console.WriteLine($"[Program.Main] Before Domain Unload on {domainName}");
            AppDomain.Unload(domain);
            Console.WriteLine($"[Program.Main] After Domain Unload on {domainName}");

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


    //
    // The code we'll test. All that really matters is
    //    using GIL { Python.Exec(pyScript); }
    // but the rest is useful for debugging.
    //
    // What matters in the python code is gc.collect and clr.AddReference.
    //
    // Note that the language version is 2.0, so no $"foo{bar}" syntax.
    //
    static class PythonRunner
    {
        public static void RunPython()
        {
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            string name = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine(string.Format("[{0} in .NET] In PythonRunner.RunPython", name));
            var mode = PythonEngine.DefaultShutdownMode;
            if (mode == ShutdownMode.Normal)
            {
                mode = ShutdownMode.Soft;
            }
            PythonEngine.Initialize(mode: mode);
            using (Py.GIL())
            {
                try
                {
                    var pyScript = string.Format("import clr\n"
                        + "print('[{0} in python] imported clr')\n"
                        + "clr.AddReference('System')\n"
                        + "print('[{0} in python] allocated a clr object')\n"
                        + "import gc\n"
                        + "gc.collect()\n"
                        + "print('[{0} in python] collected garbage')\n",
                        name);
                    PythonEngine.Exec(pyScript);
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("[{0} in .NET] Caught exception: {1}", name, e));
                }
            }
            PythonEngine.BeginAllowThreads();
        }


        private static IntPtr _state;

        public static void InitPython(ShutdownMode mode)
        {
            PythonEngine.Initialize(mode: mode);
            _state = PythonEngine.BeginAllowThreads();
        }

        public static void ShutdownPython()
        {
            PythonEngine.EndAllowThreads(_state);
            PythonEngine.Shutdown();
        }

        public static void ShutdownPythonCompletely()
        {
            PythonEngine.EndAllowThreads(_state);
            // XXX: Reload mode will reserve clr objects after `Runtime.Shutdown`,
            // if it used a another mode(the default mode) in other tests,
            // when other tests trying to access these reserved objects, it may cause Domain exception,
            // thus it needs to reduct to Soft mode to make sure all clr objects remove from Python.
            if (PythonEngine.DefaultShutdownMode != ShutdownMode.Reload)
            {
                PythonEngine.ShutdownMode = ShutdownMode.Soft;
            }
            PythonEngine.Shutdown();
            if (PythonEngine.DefaultShutdownMode == ShutdownMode.Normal)
            {
                // Normal mode will shutdown the VM, so it needs to be shutdown
                // for avoiding influence with other tests.
                Runtime.Runtime.Shutdown();
            }
        }

        public static IntPtr GetTestObject()
        {
            try
            {
                Type type = typeof(Python.EmbeddingTest.Domain.MyClass);
                string code = string.Format(@"
import clr
clr.AddReference('{0}')

from Python.EmbeddingTest.Domain import MyClass
obj = MyClass()
obj.Method()
obj.StaticMethod()
", Assembly.GetExecutingAssembly().FullName);

                using (Py.GIL())
                using (var scope = Py.CreateScope())
                {
                    scope.Exec(code);
                    using (PyObject obj = scope.Get("obj"))
                    {
                        Debug.Assert(obj.AsManagedObject(type).GetType() == type);
                        // We only needs its Python handle
                        Runtime.Runtime.XIncref(obj.Handle);
                        return obj.Handle;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }
        }

        public static void RunTestObject(IntPtr handle)
        {
            try
            {
                using (Py.GIL())
                {

                    using (PyObject obj = new PyObject(handle))
                    {
                        obj.InvokeMethod("Method");
                        obj.InvokeMethod("StaticMethod");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }
        }

        public static void ReleaseTestObject(IntPtr handle)
        {
            using (Py.GIL())
            {
                Runtime.Runtime.XDecref(handle);
            }
        }

        static void OnDomainUnload(object sender, EventArgs e)
        {
            Console.WriteLine(string.Format("[{0} in .NET] unloading", AppDomain.CurrentDomain.FriendlyName));
        }
    }

}


namespace Python.EmbeddingTest.Domain
{
    [Serializable]
    public class MyClass
    {
        public void Method() { }
        public static void StaticMethod() { }
    }
}


#endif
