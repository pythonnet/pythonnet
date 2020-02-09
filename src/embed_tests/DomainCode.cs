using Python.Runtime;
using System;
using System.Diagnostics;
using System.Reflection;

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
    static readonly Action<IntPtr> XIncref;
    static readonly Action<IntPtr> XDecref;

    static PythonRunner()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo incMethod = typeof(Runtime).GetMethod("XIncref", flags);
        MethodInfo decMethod = typeof(Runtime).GetMethod("XDecref", flags);

        XIncref = (Action<IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr>), incMethod);
        XDecref = (Action<IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr>), decMethod);
    }

    public static void RunPython()
    {
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        string name = AppDomain.CurrentDomain.FriendlyName;
        Console.WriteLine(string.Format("[{0} in .NET] In PythonRunner.RunPython", name));
        PythonEngine.Initialize(mode: ShutdownMode.Reload);
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
        PythonEngine.ShutdownMode = ShutdownMode.Normal;
        PythonEngine.Shutdown();
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
                    XIncref(obj.Handle);
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
        using (Py.GIL())
        {
            
            using (PyObject obj = new PyObject(handle))
            {
                obj.InvokeMethod("Method");
                obj.InvokeMethod("StaticMethod");
            }
        }
    }

    public static void ReleaseTestObject(IntPtr handle)
    {
        using (Py.GIL())
        {
            XDecref(handle);
        }
    }

    static void OnDomainUnload(object sender, EventArgs e)
    {
        Console.WriteLine(string.Format("[{0} in .NET] unloading", AppDomain.CurrentDomain.FriendlyName));
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
