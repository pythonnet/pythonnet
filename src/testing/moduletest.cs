using System;
using System.Threading;

namespace Python.Test {
    public class ModuleTest {
        private static Thread _thread;

        public static void RunThreads()
        {
            _thread = new Thread(() => {
                var appdomain = AppDomain.CurrentDomain;
                var assemblies = appdomain.GetAssemblies();
                foreach (var assembly in assemblies) {
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
