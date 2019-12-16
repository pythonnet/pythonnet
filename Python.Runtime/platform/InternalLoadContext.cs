using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Python.Runtime.Platform
{
    class InternalLoadContext : AssemblyLoadContext
    {
        protected override Assembly Load(AssemblyName name) => null;

        protected override IntPtr LoadUnmanagedDll(string name)
        {
            if (name == "__Internal")
            {
                var loader = LibraryLoader.Get(OperatingSystemType.Linux);
                return loader.Load(null);
            }

            return IntPtr.Zero;
        }

        public static AssemblyLoadContext Instance { get; } = new InternalLoadContext();
    }
}
