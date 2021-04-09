using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Python.Runtime
{
    using static Runtime;

    [Obsolete("Only to be used from within Python")]
    static class Loader
    {
        public unsafe static int Initialize(IntPtr data, int size)
        {
            IntPtr gs = IntPtr.Zero;
            try
            {
                var dllPath = Encoding.UTF8.GetString((byte*)data.ToPointer(), size);

                if (!string.IsNullOrEmpty(dllPath))
                {
                    PythonDLL = dllPath;
                }
                else
                {
                    PythonDLL = null;
                }

                gs = PyGILState_Ensure();

                // Console.WriteLine("Startup thread");
                PythonEngine.InitExt();
                // Console.WriteLine("Startup finished");
            }
            catch (Exception exc)
            {
                Console.Error.Write(
                    $"Failed to initialize pythonnet: {exc}\n{exc.StackTrace}"
                );
                return 1;
            }
            finally
            {
                if (gs != IntPtr.Zero)
                {
                    PyGILState_Release(gs);
                }
            }
            return 0;
        }

        public unsafe static int Shutdown(IntPtr data, int size)
        {
            IntPtr gs = IntPtr.Zero;
            try
            {
                var command = Encoding.UTF8.GetString((byte*)data.ToPointer(), size);

                if (command == "full_shutdown")
                {
                    gs = PyGILState_Ensure();
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
            finally
            {
                if (gs != IntPtr.Zero)
                {
                    PyGILState_Release(gs);
                }
            }
            return 0;
        }
    }
}
