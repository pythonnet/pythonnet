using System;
using System.Runtime.InteropServices;

namespace Python.Runtime.Platform
{
    public enum MachineType
    {
        i386,
        x86_64,
        armv7l,
        armv8,
        aarch64,
        Other
    };

    /// <summary>
    /// Operating system type as reported by Python.
    /// </summary>
    public enum OperatingSystemType
    {
        Windows,
        Darwin,
        Linux,
        Other
    }


    static class SystemInfo
    {
        public static MachineType GetMachineType()
        {
            return Runtime.IsWindows ? GetMachineType_Windows() : GetMachineType_Unix();
        }

        public static string GetArchitecture()
        {
            return Runtime.IsWindows ? GetArchName_Windows() : GetArchName_Unix();
        }

        public static OperatingSystemType GetSystemType()
        {
            if (Runtime.IsWindows)
            {
                return OperatingSystemType.Windows;
            }
            switch (PythonEngine.Platform)
            {
                case "linux":
                    return OperatingSystemType.Linux;

                case "darwin":
                    return OperatingSystemType.Darwin;

                default:
                    return OperatingSystemType.Other;
            }
        }

        #region WINDOWS

        static string GetArchName_Windows()
        {
            // https://docs.microsoft.com/en-us/windows/win32/winprog64/wow64-implementation-details
            return Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
        }

        static MachineType GetMachineType_Windows()
        {
            if (Runtime.Is32Bit)
            {
                return MachineType.i386;
            }
            switch (GetArchName_Windows())
            {
                case "AMD64":
                    return MachineType.x86_64;
                case "ARM64":
                    return MachineType.aarch64;
                default:
                    return MachineType.Other;
            }
        }

        #endregion

        #region UNIX


        [StructLayout(LayoutKind.Sequential)]
        unsafe struct utsname_linux
        {
            const int NameLength = 65;

            /* Name of the implementation of the operating system.  */
            public fixed byte sysname[NameLength];

            /* Name of this node on the network.  */
            public fixed byte nodename[NameLength];

            /* Current release level of this implementation.  */
            public fixed byte release[NameLength];

            /* Current version level of this release.  */
            public fixed byte version[NameLength];

            /* Name of the hardware type the system is running on.  */
            public fixed byte machine[NameLength];

            // GNU extension
            fixed byte domainname[NameLength]; /* NIS or YP domain name */
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct utsname_darwin
        {
            const int NameLength = 256;

            /* Name of the implementation of the operating system.  */
            public fixed byte sysname[NameLength];

            /* Name of this node on the network.  */
            public fixed byte nodename[NameLength];

            /* Current release level of this implementation.  */
            public fixed byte release[NameLength];

            /* Current version level of this release.  */
            public fixed byte version[NameLength];

            /* Name of the hardware type the system is running on.  */
            public fixed byte machine[NameLength];
        }

        [DllImport("libc")]
        static extern int uname(IntPtr buf);


        static unsafe string GetArchName_Unix()
        {
            switch (GetSystemType())
            {
                case OperatingSystemType.Linux:
                    {
                        var buf = stackalloc utsname_linux[1];
                        if (uname((IntPtr)buf) != 0)
                        {
                            return null;
                        }
                        return Marshal.PtrToStringAnsi((IntPtr)buf->machine);
                    }

                case OperatingSystemType.Darwin:
                    {
                        var buf = stackalloc utsname_darwin[1];
                        if (uname((IntPtr)buf) != 0)
                        {
                            return null;
                        }
                        return Marshal.PtrToStringAnsi((IntPtr)buf->machine);
                    }

                default:
                    return null;
            }
        }

        static unsafe MachineType GetMachineType_Unix()
        {
            switch (GetArchName_Unix())
            {
                case "x86_64":
                case "em64t":
                    return Runtime.Is32Bit ? MachineType.i386 : MachineType.x86_64;
                case "i386":
                case "i686":
                    return MachineType.i386;

                case "armv7l":
                    return MachineType.armv7l;
                case "armv8":
                    return Runtime.Is32Bit ? MachineType.armv7l : MachineType.armv8;
                case "aarch64":
                    return Runtime.Is32Bit ? MachineType.armv7l : MachineType.aarch64;

                default:
                    return MachineType.Other;
            }
        }

        #endregion
    }
}
