using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime
{
#if NETSTANDARD
    public static class ReflectionPolifills
    {
        public static AssemblyBuilder DefineDynamicAssembly(this AppDomain appDomain, AssemblyName assemblyName, AssemblyBuilderAccess assemblyBuilderAccess)
        {
            return AssemblyBuilder.DefineDynamicAssembly(assemblyName, assemblyBuilderAccess);
        }

        public static Type CreateType(this TypeBuilder typeBuilder)
        {
            return typeBuilder.GetTypeInfo().GetType();
        }
    }
#endif
}
