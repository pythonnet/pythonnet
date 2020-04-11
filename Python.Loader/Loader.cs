using System.Runtime.InteropServices;
using System.Text;
using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace Python
{
    public class Loader
    {
        AssemblyDefinition assembly;

        static public Loader FromFile(string filename)
        {
            return new Loader(File.OpenRead(filename));
        }

        public Loader(Stream stream)
        {
            assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters
            {
                ReadWrite = true,
                ReadingMode = ReadingMode.Immediate,
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

                var loader = Loader.FromFile(dllPath);
                loader.Remap(pythonDll);
                var assembly = loader.LoadAssembly();

                var eng = assembly.GetType("Python.Runtime.PythonEngine");
                var method = eng.GetMethod("InternalInitialize");
                var res = method.Invoke(null, new object[] { data, size });
                return (int)res;
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{exc}\n{exc.StackTrace}");
                return -1;
            }
        }
    }
}
