using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    interface ILibraryLoader
    {
        IntPtr Load(string dllToLoad);

        IntPtr GetFunction(IntPtr hModule, string procedureName);

        void Free(IntPtr hModule);
    }

    static class LibraryLoader
    {
        static ILibraryLoader _instance = null;

        public static ILibraryLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        _instance = new WindowsLoader();
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        _instance = new LinuxLoader();
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        _instance = new DarwinLoader();
                    else
                        throw new PlatformNotSupportedException(
                            "This operating system is not supported"
                        );
                }

                return _instance;
            }
        }
    }

    class LinuxLoader : ILibraryLoader
    {
        private static int RTLD_NOW = 0x2;
        private static int RTLD_GLOBAL = 0x100;
        private static IntPtr RTLD_DEFAULT = IntPtr.Zero;
        private const string NativeDll = "libdl.so";

        public IntPtr Load(string dllToLoad)
        {
            ClearError();
            var res = dlopen(dllToLoad, RTLD_NOW | RTLD_GLOBAL);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new DllNotFoundException($"Could not load {dllToLoad} with flags RTLD_NOW | RTLD_GLOBAL: {err}");
            }

            return res;
        }

        public void Free(IntPtr handle)
        {
            dlclose(handle);
        }

        public IntPtr GetFunction(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = RTLD_DEFAULT;
            }

            ClearError();
            IntPtr res = dlsym(dllHandle, name);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new MissingMethodException($"Failed to load symbol {name}: {err}");
            }
            return res;
        }

        void ClearError()
        {
            dlerror();
        }

        string GetError()
        {
            var res = dlerror();
            if (res != IntPtr.Zero)
                return Marshal.PtrToStringAnsi(res);
            else
                return null;
        }

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr dlopen(string fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
    }

    class DarwinLoader : ILibraryLoader
    {
        private static int RTLD_NOW = 0x2;
        private static int RTLD_GLOBAL = 0x8;
        private const string NativeDll = "/usr/lib/libSystem.dylib";
        private static IntPtr RTLD_DEFAULT = new IntPtr(-2);

        public IntPtr Load(string dllToLoad)
        {
            ClearError();
            var res = dlopen(dllToLoad, RTLD_NOW | RTLD_GLOBAL);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new DllNotFoundException($"Could not load {dllToLoad} with flags RTLD_NOW | RTLD_GLOBAL: {err}");
            }

            return res;
        }

        public void Free(IntPtr handle)
        {
            dlclose(handle);
        }

        public IntPtr GetFunction(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = RTLD_DEFAULT;
            }

            ClearError();
            IntPtr res = dlsym(dllHandle, name);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new MissingMethodException($"Failed to load symbol {name}: {err}");
            }
            return res;
        }

        void ClearError()
        {
            dlerror();
        }

        string GetError()
        {
            var res = dlerror();
            if (res != IntPtr.Zero)
                return Marshal.PtrToStringAnsi(res);
            else
                return null;
        }

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr dlopen(String fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
    }

    class WindowsLoader : ILibraryLoader
    {
        private const string NativeDll = "kernel32.dll";


        public IntPtr Load(string dllToLoad)
        {
            var res = WindowsLoader.LoadLibrary(dllToLoad);
            if (res == IntPtr.Zero)
                throw new DllNotFoundException($"Could not load {dllToLoad}", new Win32Exception());
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
                throw new MissingMethodException($"Failed to load symbol {procedureName}", new Win32Exception());
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
