using System;
using System.Reflection;
using System.Threading;

namespace Python.Test
{
    public class ModuleTest
    {
        private static Thread _thread;

        public static void RunThreads()
        {
            _thread = new Thread(() =>
            {
                AppDomain appdomain = AppDomain.CurrentDomain;
                Assembly[] assemblies = appdomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    assembly.GetTypes();
                }
            });
            _thread.Start();
        }

        public static void JoinThreads()
        {
            _thread.Join();
        }
    }
}
