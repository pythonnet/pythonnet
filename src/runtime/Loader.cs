using System;
using System.IO;
using System.Text;

namespace Python.Runtime
{
    using static Runtime;

    [Obsolete("Only to be used from within Python")]
    static class Loader
    {
        public unsafe static int Initialize(IntPtr data, int size)
        {
            try
            {
                // On .NET Framework, the host is python.exe which has no binding
                // redirects for netstandard2.0 shims (e.g. RuntimeInformation
                // Version=0.0.0.0 vs the 4.0.2.0 shim on disk). Binding redirects
                // via config files can't be injected after AppDomain creation, so
                // resolve assemblies from our runtime directory directly.
                AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                {
                    var name = new System.Reflection.AssemblyName(args.Name);
                    var dir = Path.GetDirectoryName(typeof(Loader).Assembly.Location);
                    var path = Path.Combine(dir, name.Name + ".dll");

                    return File.Exists(path)
                        ? System.Reflection.Assembly.LoadFrom(path)
                        : null;
                };

                var dllPath = Encodings.UTF8.GetString((byte*)data.ToPointer(), size);

                if (!string.IsNullOrEmpty(dllPath))
                {
                    PythonDLL = dllPath;
                }
                else
                {
                    PythonDLL = null;
                }

                using var _ = Py.GIL();
                PythonEngine.InitExt();
            }
            catch (Exception exc)
            {
                Console.Error.Write(
                    $"Failed to initialize pythonnet: {exc}\n{exc.StackTrace}"
                );
                return 1;
            }

            return 0;
        }

        public unsafe static int Shutdown(IntPtr data, int size)
        {
            try
            {
                var command = Encodings.UTF8.GetString((byte*)data.ToPointer(), size);

                if (command == "full_shutdown")
                {
                    using var _ = Py.GIL();
                    PythonEngine.Shutdown();
                }
            }
            catch (Exception exc)
            {
                Console.Error.Write(
                    $"Failed to shutdown pythonnet: {exc}\n{exc.StackTrace}"
                );
                return 1;
            }

            return 0;
        }
    }
}
