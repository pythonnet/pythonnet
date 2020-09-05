using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Python.Runtime;

using PyRuntime = Python.Runtime.Runtime;
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
        abstract class CrossCaller : MarshalByRefObject
        {
            public abstract ValueType Execute(ValueType arg);
        }


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
            Assert.That(PyRuntime.Py_IsInitialized() != 0,
                "On soft-shutdown mode, Python runtime should still running");

            RunAssemblyAndUnload("test2");
            Assert.That(PyRuntime.Py_IsInitialized() != 0,
                "On soft-shutdown mode, Python runtime should still running");

            if (PythonEngine.DefaultShutdownMode == ShutdownMode.Normal)
            {
                // The default mode is a normal mode,
                // it should shutdown the Python VM avoiding influence other tests.
                PyRuntime.PyGILState_Ensure();
                PyRuntime.Py_Finalize();
            }
        }

        #region CrossDomainObject

        class CrossDomainObjectStep1 : CrossCaller
        {
            public override ValueType Execute(ValueType arg)
            {
                try
                {
                    // Create a C# user-defined object in Python. Asssing some values.
                    Type type = typeof(Python.EmbeddingTest.Domain.MyClass);
                    string code = string.Format(@"
import clr
clr.AddReference('{0}')

from Python.EmbeddingTest.Domain import MyClass
obj = MyClass()
obj.Method()
obj.StaticMethod()
obj.Property = 1
obj.Field = 10
", Assembly.GetExecutingAssembly().FullName);

                    using (Py.GIL())
                    using (var scope = Py.CreateScope())
                    {
                        scope.Exec(code);
                        using (PyObject obj = scope.Get("obj"))
                        {
                            Debug.Assert(obj.AsManagedObject(type).GetType() == type);
                            // We only needs its Python handle
                            PyRuntime.XIncref(obj.Handle);
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
        }


        class CrossDomainObjectStep2 : CrossCaller
        {
            public override ValueType Execute(ValueType arg)
            {
                // handle refering a clr object created in previous domain,
                // it should had been deserialized and became callable agian.
                IntPtr handle = (IntPtr)arg;
                try
                {
                    using (Py.GIL())
                    {
                        IntPtr tp = Runtime.Runtime.PyObject_TYPE(handle);
                        IntPtr tp_clear = Marshal.ReadIntPtr(tp, TypeOffset.tp_clear);
                        Assert.That(tp_clear, Is.Not.Null);

                        using (PyObject obj = new PyObject(handle))
                        {
                            obj.InvokeMethod("Method");
                            obj.InvokeMethod("StaticMethod");

                            using (var scope = Py.CreateScope())
                            {
                                scope.Set("obj", obj);
                                scope.Exec(@"
obj.Method()
obj.StaticMethod()
obj.Property += 1
obj.Field += 10
");
                            }
                            var clrObj = obj.As<Domain.MyClass>();
                            Assert.AreEqual(clrObj.Property, 2);
                            Assert.AreEqual(clrObj.Field, 20);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    throw;
                }
                return 0;
            }
        }

        /// <summary>
        /// Create a C# custom object in a domain, in python code.
        /// Unload the domain, create a new domain.
        /// Make sure the C# custom object created in the previous domain has been re-created
        /// </summary>
        [Test]
        public static void CrossDomainObject()
        {
            RunDomainReloadSteps<CrossDomainObjectStep1, CrossDomainObjectStep2>();
        }

        #endregion

        #region TestClassReference

        class ReloadClassRefStep1 : CrossCaller
        {
            public override ValueType Execute(ValueType arg)
            {
                const string code = @"
from Python.EmbeddingTest.Domain import MyClass

def test_obj_call():
    obj = MyClass()
    obj.Method()
    obj.StaticMethod()
    obj.Property = 1
    obj.Field = 10

test_obj_call()
";
                const string name = "test_domain_reload_mod";
                using (Py.GIL())
                {
                    // Create a new module
                    IntPtr module = PyRuntime.PyModule_New(name);
                    Assert.That(module != IntPtr.Zero);
                    IntPtr globals = PyRuntime.PyObject_GetAttrString(module, "__dict__");
                    Assert.That(globals != IntPtr.Zero);
                    try
                    {
                        // import builtins
                        // module.__dict__[__builtins__] = builtins
                        int res = PyRuntime.PyDict_SetItemString(globals, "__builtins__",
                            PyRuntime.PyEval_GetBuiltins());
                        PythonException.ThrowIfIsNotZero(res);

                        // Execute the code in the module's scope
                        PythonEngine.Exec(code, globals);
                        // import sys
                        // modules = sys.modules
                        IntPtr modules = PyRuntime.PyImport_GetModuleDict();
                        // modules[name] = module
                        res = PyRuntime.PyDict_SetItemString(modules, name, module);
                        PythonException.ThrowIfIsNotZero(res);
                    }
                    catch
                    {
                        PyRuntime.XDecref(module);
                        throw;
                    }
                    finally
                    {
                        PyRuntime.XDecref(globals);
                    }
                    return module;
                }
            }
        }

        class ReloadClassRefStep2 : CrossCaller
        {
            public override ValueType Execute(ValueType arg)
            {
                var module = (IntPtr)arg;
                using (Py.GIL())
                {
                    var test_obj_call = PyRuntime.PyObject_GetAttrString(module, "test_obj_call");
                    PythonException.ThrowIfIsNull(test_obj_call);
                    var args = PyRuntime.PyTuple_New(0);
                    var res = PyRuntime.PyObject_CallObject(test_obj_call, args);
                    PythonException.ThrowIfIsNull(res);

                    PyRuntime.XDecref(args);
                    PyRuntime.XDecref(res);
                }
                return 0;
            }
        }


        [Test]
        /// <summary>
        /// Create a new Python module, define a function in it.
        /// Unload the domain, load a new one.
        /// Make sure the function (and module) still exists.
        /// </summary>
        public void TestClassReference()
        {
            RunDomainReloadSteps<ReloadClassRefStep1, ReloadClassRefStep2>();
        }

        #endregion

        #region Tempary tests

        // https://github.com/pythonnet/pythonnet/pull/1074#issuecomment-596139665
        [Test]
        public void CrossReleaseBuiltinType()
        {
            void ExecTest()
            {
                try
                {
                    PythonEngine.Initialize();
                    var numRef = CreateNumReference();
                    Assert.True(numRef.IsAlive);
                    PythonEngine.Shutdown(); // <- "run" 1 ends
                    PythonEngine.Initialize(); // <- "run" 2 starts

                    GC.Collect();
                    GC.WaitForPendingFinalizers(); // <- this will put former `num` into Finalizer queue
                    Finalizer.Instance.Collect(forceDispose: true);
                    // ^- this will call PyObject.Dispose, which will call XDecref on `num.Handle`,
                    // but Python interpreter from "run" 1 is long gone, so it will corrupt memory instead.
                    Assert.False(numRef.IsAlive);
                }
                finally
                {
                    PythonEngine.Shutdown();
                }
            }

            var errorArgs = new List<Finalizer.ErrorArgs>();
            void ErrorHandler(object sender, Finalizer.ErrorArgs e)
            {
                errorArgs.Add(e);
            }
            Finalizer.Instance.ErrorHandler += ErrorHandler;
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    ExecTest();
                }
            }
            finally
            {
                Finalizer.Instance.ErrorHandler -= ErrorHandler;
            }
            Assert.AreEqual(errorArgs.Count, 0);
        }

        [Test]
        public void CrossReleaseCustomType()
        {
            void ExecTest()
            {
                try
                {
                    PythonEngine.Initialize();
                    var objRef = CreateConcreateObject();
                    Assert.True(objRef.IsAlive);
                    PythonEngine.Shutdown(); // <- "run" 1 ends
                    PythonEngine.Initialize(); // <- "run" 2 starts
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Finalizer.Instance.Collect(forceDispose: true);
                    Assert.False(objRef.IsAlive);
                }
                finally
                {
                    PythonEngine.Shutdown();
                }
            }

            var errorArgs = new List<Finalizer.ErrorArgs>();
            void ErrorHandler(object sender, Finalizer.ErrorArgs e)
            {
                errorArgs.Add(e);
            }
            Finalizer.Instance.ErrorHandler += ErrorHandler;
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    ExecTest();
                }
            }
            finally
            {
                Finalizer.Instance.ErrorHandler -= ErrorHandler;
            }
            Assert.AreEqual(errorArgs.Count, 0);
        }

        private static WeakReference CreateNumReference()
        {
            var num = 3216757418.ToPython();
            Assert.AreEqual(num.Refcount, 1);
            WeakReference numRef = new WeakReference(num, false);
            return numRef;
        }

        private static WeakReference CreateConcreateObject()
        {
            var obj = new Domain.MyClass().ToPython();
            Assert.AreEqual(obj.Refcount, 1);
            WeakReference numRef = new WeakReference(obj, false);
            return numRef;
        }

        #endregion Tempary tests

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
        
        static T CreateInstanceInstanceAndUnwrap<T>(AppDomain domain)
        {
            Type type = typeof(T);
            var theProxy = (T)domain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName);
            return theProxy;
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
            var theProxy = CreateInstanceInstanceAndUnwrap<Proxy>(domain);

            theProxy.Call("InitPython", ShutdownMode.Soft);
            // From now on use the Proxy to call into the new assembly
            theProxy.RunPython();

            theProxy.Call("ShutdownPython");
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

        static void RunDomainReloadSteps<T1, T2>() where T1 : CrossCaller where T2 : CrossCaller
        {
            ValueType arg = null;
            Type type = typeof(Proxy);
            {
                AppDomain domain = CreateDomain("test_domain_reload_1");
                try
                {
                    var theProxy = CreateInstanceInstanceAndUnwrap<Proxy>(domain);
                    theProxy.Call("InitPython", ShutdownMode.Reload);

                    var caller = CreateInstanceInstanceAndUnwrap<T1>(domain);
                    arg = caller.Execute(arg);

                    theProxy.Call("ShutdownPython");
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }

            {
                AppDomain domain = CreateDomain("test_domain_reload_2");
                try
                {
                    var theProxy = CreateInstanceInstanceAndUnwrap<Proxy>(domain);
                    theProxy.Call("InitPython", ShutdownMode.Reload);

                    var caller = CreateInstanceInstanceAndUnwrap<T2>(domain);
                    caller.Execute(arg);
                    theProxy.Call("ShutdownPythonCompletely");
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }
            if (PythonEngine.DefaultShutdownMode == ShutdownMode.Normal)
            {
                Assert.IsTrue(PyRuntime.Py_IsInitialized() == 0);
            }
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
            Console.WriteLine("[{0} in .NET] In PythonRunner.RunPython", name);
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
                    throw;
                }
            }
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
            var defaultMode = PythonEngine.DefaultShutdownMode;
            if (defaultMode != ShutdownMode.Reload)
            {
                PythonEngine.ShutdownMode = defaultMode;
            }
            PythonEngine.Shutdown();
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
        public int Property { get; set; }
        public int Field;
        public void Method() { }
        public static void StaticMethod() { }
    }
}


#endif
