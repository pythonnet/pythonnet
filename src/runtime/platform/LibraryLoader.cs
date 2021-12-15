using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    interface ILibraryLoader
    {
        IntPtr Load(string? dllToLoad);

        IntPtr GetFunction(IntPtr hModule, string procedureName);

        void Free(IntPtr hModule);
    }

    static class LibraryLoader
    {
        static ILibraryLoader? _instance = null;

        public static ILibraryLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        _instance = new WindowsLoader();
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        _instance = new PosixLoader(LinuxLibDL.GetInstance());
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        _instance = new PosixLoader(new MacLibDL());
                    else
                        throw new PlatformNotSupportedException(
                            "This operating system is not supported"
                        );
                }

                return _instance;
            }
        }
    }

    class PosixLoader : ILibraryLoader
    {
        private readonly ILibDL libDL;

        public PosixLoader(ILibDL libDL)
        {
            this.libDL = libDL ?? throw new ArgumentNullException(nameof(libDL));
        }

        public IntPtr Load(string? dllToLoad)
        {
            ClearError();
            var res = libDL.dlopen(dllToLoad, libDL.RTLD_NOW | libDL.RTLD_GLOBAL);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new DllNotFoundException($"Could not load {dllToLoad} with flags RTLD_NOW | RTLD_GLOBAL: {err}");
            }

            return res;
        }

        public void Free(IntPtr handle)
        {
            libDL.dlclose(handle);
        }

        public IntPtr GetFunction(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = libDL.RTLD_DEFAULT;
            }

            ClearError();
            IntPtr res = libDL.dlsym(dllHandle, name);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new MissingMethodException($"Failed to load symbol {name}: {err}");
            }
            return res;
        }

        void ClearError()
        {
            libDL.dlerror();
        }

        string? GetError()
        {
            var res = libDL.dlerror();
            if (res != IntPtr.Zero)
                return Marshal.PtrToStringAnsi(res);
            else
                return null;
        }
    }

    class WindowsLoader : ILibraryLoader
    {
        private const string NativeDll = "kernel32.dll";


        public IntPtr Load(string? dllToLoad)
        {
            if (dllToLoad is null) return IntPtr.Zero;
            var res = WindowsLoader.LoadLibrary(dllToLoad);
            if (res == IntPtr.Zero)
                throw new DllNotFoundException($"Could not load {dllToLoad}.", new Win32Exception());
            return res;
        }

        public IntPtr GetFunction(IntPtr hModule, string procedureName)
        {
            if (hModule == IntPtr.Zero)
            {
                foreach(var module in GetAllModules())
                {
                    var func = GetProcAddress(module, procedureName);
                    if (func != IntPtr.Zero) return func;
                }
            }

            var res = WindowsLoader.GetProcAddress(hModule, procedureName);
            if (res == IntPtr.Zero)
                throw new MissingMethodException($"Failed to load symbol {procedureName}.", new Win32Exception());
            return res;
        }

        public void Free(IntPtr hModule) => WindowsLoader.FreeLibrary(hModule);

        static IntPtr[] GetAllModules()
        {
            var self = Process.GetCurrentProcess().Handle;

            uint bytes = 0;
            var result = new IntPtr[0];
            if (!EnumProcessModules(self, result, bytes, out var needsBytes))
                throw new Win32Exception();
            while (bytes < needsBytes)
            {
                bytes = needsBytes;
                result = new IntPtr[bytes / IntPtr.Size];
                if (!EnumProcessModules(self, result, bytes, out needsBytes))
                    throw new Win32Exception();
            }
            return result.Take((int)(needsBytes / IntPtr.Size)).ToArray();
        }

        [DllImport(NativeDll, SetLastError = true)]
        static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport(NativeDll, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport(NativeDll)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("Psapi.dll", SetLastError = true)]
        static extern bool EnumProcessModules(IntPtr hProcess, [In, Out] IntPtr[] lphModule, uint lphModuleByteCount, out uint byteCountNeeded);
    }
}
