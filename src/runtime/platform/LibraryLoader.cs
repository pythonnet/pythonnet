using System;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    interface ILibraryLoader
    {
        IntPtr Load(string dllToLoad);

        IntPtr GetFunction(IntPtr hModule, string procedureName);

        bool Free(IntPtr hModule);
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
                    throw new Exception($"This operating system ({os}) is not supported");
            }
        }
    }

    class LinuxLoader : ILibraryLoader
    {
        private static int RTLD_NOW = 0x2;
        private static int RTLD_GLOBAL = 0x100;
        private static IntPtr RTLD_DEFAULT = IntPtr.Zero;
        private const string NativeDll = "libdl.so";

        public IntPtr Load(string fileName)
        {
            return dlopen($"lib{fileName}.so", RTLD_NOW | RTLD_GLOBAL);
        }

        public bool Free(IntPtr handle)
        {
            dlclose(handle);
            return true;
        }

        public IntPtr GetFunction(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = RTLD_DEFAULT;
            }

            // clear previous errors if any
            dlerror();
            IntPtr res = dlsym(dllHandle, name);
            IntPtr errPtr = dlerror();
            if (errPtr != IntPtr.Zero)
            {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
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

    class DarwinLoader : ILibraryLoader
    {
        private static int RTLD_NOW = 0x2;
        private static int RTLD_GLOBAL = 0x8;
        private const string NativeDll = "/usr/lib/libSystem.dylib";
        private static IntPtr RTLD_DEFAULT = new IntPtr(-2);

        public IntPtr Load(string fileName)
        {
            return dlopen($"lib{fileName}.dylib", RTLD_NOW | RTLD_GLOBAL);
        }

        public bool Free(IntPtr handle)
        {
            dlclose(handle);
            return true;
        }

        public IntPtr GetFunction(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = RTLD_DEFAULT;
            }

            // clear previous errors if any
            dlerror();
            IntPtr res = dlsym(dllHandle, name);
            IntPtr errPtr = dlerror();
            if (errPtr != IntPtr.Zero)
            {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
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

        [DllImport(NativeDll)]
        static extern IntPtr LoadLibrary(string dllToLoad);

        public IntPtr Load(string dllToLoad) => WindowsLoader.LoadLibrary(dllToLoad);

        [DllImport(NativeDll)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        public IntPtr GetFunction(IntPtr hModule, string procedureName) => WindowsLoader.GetProcAddress(hModule, procedureName);


        [DllImport(NativeDll)]
        static extern bool FreeLibrary(IntPtr hModule);

        public bool Free(IntPtr hModule) => WindowsLoader.FreeLibrary(hModule);
    }
}
