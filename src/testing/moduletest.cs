using System;
using System.Threading;

namespace Python.Test {
    public class ModuleTest {
        public static void RunThreads() {
            var thread = new Thread(() => {
                var appdomain = AppDomain.CurrentDomain;
                var assemblies = appdomain.GetAssemblies();
                foreach (var assembly in assemblies) {
                    assembly.GetTypes();
                }
            });
            thread.Start();
        }
    }
}