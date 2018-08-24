using System;
using Python.Runtime;
using System.Reflection;

public class DummyClass
{
    static public DummyClass instance = new DummyClass();
    public static DummyClass DummyMethod()
    {
        return instance;
    }
}

class PythonRunner
{
    static public void Init()
    {
        System.Console.WriteLine(string.Format("[theAssembly ] PythonRunner.Init current domain = {0}",AppDomain.CurrentDomain.FriendlyName));

        // Register to domain unload
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
    }

    private static void OnDomainUnload(object sender, EventArgs e)
    {
        System.Console.WriteLine(string.Format("[theAssembly ] In OnDomainUnload current domain = {0}",AppDomain.CurrentDomain.FriendlyName));
    }

    public static void RunPython() {
        System.Console.WriteLine("[theAssembly ] In PythonRunner.RunPython");
        using (Py.GIL()) {
            try {
                var pyScript = 
                    "import clr\n" +
                    "clr.AddReference('System') \n" +
                    "print('[Python      ] Done')\n";

                PythonEngine.Exec(pyScript);
            } catch(Exception e) {
                System.Console.WriteLine(string.Format("Caught exception: {0}",e));
            }   
        }   
    }
}
