using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Loader
{
    public static class InternalDllImportResolver
    {
        public static IntPtr Resolve(string libraryName, Assembly assembly, int? flags) {
            if (libraryName == "__Internal") {

            }
        }

    }
}
