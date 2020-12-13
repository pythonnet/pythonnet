using System.Runtime.InteropServices;
using System.Text;
using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace Python.Loader
{
    public class RuntimeLoader
    {
        AssemblyDefinition assembly;

        static public RuntimeLoader FromFile(string filename)
        {
            return new RuntimeLoader(File.OpenRead(filename));
        }

        public RuntimeLoader(Stream stream)
        {
            var asmResolver = new NetStandardResolver();
            assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters
            {
                AssemblyResolver = asmResolver,
                InMemory = true,
            });
        }

        public void Remap(string pythonDll)
        {
            var moduleRef = new ModuleReference(pythonDll);

            var module = assembly.MainModule;
            module.ModuleReferences.Add(moduleRef);

            foreach (var type in module.Types)
            {
                foreach (var func in type.Methods)
                {
                    if (func.HasPInvokeInfo)
                    {
                        var info = func.PInvokeInfo;
                        if (info.Module.Name == "__Internal")
                        {
                            info.Module = moduleRef;
                        }
                    }
                }
            }
        }

        public Assembly LoadAssembly()
        {
            using (var stream = new MemoryStream())
            {
                assembly.Write(stream);
                return Assembly.Load(stream.ToArray());
            }
        }
    }

    static class Internal
    {
        static Type PythonEngine = null;

        public static int Initialize(IntPtr data, int size)
        {
            try
            {
                var buf = new byte[size];
                Marshal.Copy(data, buf, 0, size);
                var str = UTF8Encoding.Default.GetString(buf);

                var splitted = str.Split(';');

                var dllPath = splitted[0];
                var pythonDll = splitted[1];

                Console.WriteLine("Remapping __Internal in {0} to {1}", dllPath, pythonDll);
                var loader = RuntimeLoader.FromFile(dllPath);
                loader.Remap(pythonDll);
                var assembly = loader.LoadAssembly();

                PythonEngine = assembly.GetType("Python.Runtime.PythonEngine");
                var method = PythonEngine.GetMethod("InternalInitialize");
                var res = (int)method.Invoke(null, new object[] { data, size });
                Console.WriteLine("Done calling init: {0}", res);
                return res;
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{exc}\n{exc.StackTrace}");
                return -1;
            }
        }

        public static int Shutdown(IntPtr data, int size)
        {
            if (PythonEngine == null)
                return -2;

            try
            {
                var method = PythonEngine.GetMethod("InternalShutdown");
                return (int)method.Invoke(null, new object[] { data, size });
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{exc}\n{exc.StackTrace}");
                return -1;
            }
        }
    }
}
