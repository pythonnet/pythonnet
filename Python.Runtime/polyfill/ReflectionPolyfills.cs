using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime
{
    public static class ReflectionPolyfills
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
}
