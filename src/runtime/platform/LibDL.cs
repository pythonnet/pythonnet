#pragma warning disable IDE1006 // Naming Styles (interface for native functions)
using System;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    interface ILibDL
    {
        IntPtr dlopen(string? fileName, int flags);
        IntPtr dlsym(IntPtr handle, string symbol);
        int dlclose(IntPtr handle);
        IntPtr dlerror();

        int RTLD_NOW { get; }
        int RTLD_GLOBAL { get; }
        IntPtr RTLD_DEFAULT { get; }
    }

    class LinuxLibDL : ILibDL
    {
        private const string NativeDll = "libdl.so";

        public int RTLD_NOW => 0x2;
        public int RTLD_GLOBAL => 0x100;
        public IntPtr RTLD_DEFAULT => IntPtr.Zero;

        public static ILibDL GetInstance()
        {
            try
            {
                ILibDL libdl2 = new LinuxLibDL2();
                // call dlerror to ensure library is resolved
                libdl2.dlerror();
                return libdl2;
            } catch (DllNotFoundException)
            {
                return new LinuxLibDL();
            }
        }

        IntPtr ILibDL.dlopen(string? fileName, int flags) => dlopen(fileName, flags);
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string? fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
    }

    class LinuxLibDL2 : ILibDL
    {
        private const string NativeDll = "libdl.so.2";

        public int RTLD_NOW => 0x2;
        public int RTLD_GLOBAL => 0x100;
        public IntPtr RTLD_DEFAULT => IntPtr.Zero;

        IntPtr ILibDL.dlopen(string? fileName, int flags) => dlopen(fileName, flags);
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string? fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
    }

    class MacLibDL : ILibDL
    {
        public int RTLD_NOW => 0x2;
        public int RTLD_GLOBAL => 0x8;
        const string NativeDll = "/usr/lib/libSystem.dylib";
        public IntPtr RTLD_DEFAULT => new(-2);

        IntPtr ILibDL.dlopen(string? fileName, int flags) => dlopen(fileName, flags);
        IntPtr ILibDL.dlsym(IntPtr handle, string symbol) => dlsym(handle, symbol);
        int ILibDL.dlclose(IntPtr handle) => dlclose(handle);
        IntPtr ILibDL.dlerror() => dlerror();

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string? fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
    }
}
