using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime
{
    public static class ReflectionPolifills
    {
#if NETCOREAPP
        public static AssemblyBuilder DefineDynamicAssembly(this AppDomain appDomain, AssemblyName assemblyName, AssemblyBuilderAccess assemblyBuilderAccess)
        {
            return AssemblyBuilder.DefineDynamicAssembly(assemblyName, assemblyBuilderAccess);
        }
#endif
    }
}
