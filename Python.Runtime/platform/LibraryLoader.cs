using System;
using System.ComponentModel;
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
        public static ILibraryLoader Get(OperatingSystemType os)
        {
            switch (os)
            {
                case OperatingSystemType.Windows:
                    return new WindowsLoader();
                case OperatingSystemType.Darwin:
                    return new DarwinLoader();
                case OperatingSystemType.Linux:
                    return new LinuxLoader();
                default:
                    throw new PlatformNotSupportedException($"This operating system ({os}) is not supported");
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
            var filename = $"lib{dllToLoad}.so";
            ClearError();
            var res = dlopen(filename, RTLD_NOW | RTLD_GLOBAL);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new DllNotFoundException($"Could not load {filename} with flags RTLD_NOW | RTLD_GLOBAL: {err}");
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
            var filename = $"lib{dllToLoad}.dylib";
            ClearError();
            var res = dlopen(filename, RTLD_NOW | RTLD_GLOBAL);
            if (res == IntPtr.Zero)
            {
                var err = GetError();
                throw new DllNotFoundException($"Could not load {filename} with flags RTLD_NOW | RTLD_GLOBAL: {err}");
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
            var res = WindowsLoader.GetProcAddress(hModule, procedureName);
            if (res == IntPtr.Zero)
                throw new MissingMethodException($"Failed to load symbol {procedureName}", new Win32Exception());
            return res;
        }

        public void Free(IntPtr hModule) => WindowsLoader.FreeLibrary(hModule);

        [DllImport(NativeDll, SetLastError = true)]
        static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport(NativeDll, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport(NativeDll)]
        static extern bool FreeLibrary(IntPtr hModule);
    }
}
