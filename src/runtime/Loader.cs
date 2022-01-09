using System;
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
                var dllPath = Encoding.UTF8.GetString((byte*)data.ToPointer(), size);

                if (!string.IsNullOrEmpty(dllPath))
                {
                    PythonDLL = dllPath;
                }
                else
                {
                    PythonDLL = null;
                }

                var gs = PyGILState_Ensure();

                try
                {
                    // Console.WriteLine("Startup thread");
                    PythonEngine.InitExt();
                    // Console.WriteLine("Startup finished");
                }
                finally
                {
                    PyGILState_Release(gs);
                }
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
                var command = Encoding.UTF8.GetString((byte*)data.ToPointer(), size);

                if (command == "full_shutdown")
                {
                    var gs = PyGILState_Ensure();
                    try
                    {
                        PythonEngine.Shutdown();
                    }
                    finally
                    {
                        PyGILState_Release(gs);
                    }
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
