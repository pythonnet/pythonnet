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
            var filtered = name == "__Internal" ? null : name;
            return LibraryLoader.Instance.Load(filtered);
        }

        public static AssemblyLoadContext Instance { get; } = new InternalLoadContext();
    }
}
