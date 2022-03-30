using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime
{
    internal static class ReflectionPolyfills
    {
        public static AssemblyBuilder DefineDynamicAssembly(this AppDomain _, AssemblyName assemblyName, AssemblyBuilderAccess assemblyBuilderAccess)
        {
            return AssemblyBuilder.DefineDynamicAssembly(assemblyName, assemblyBuilderAccess);
        }

        public static Type CreateType(this TypeBuilder typeBuilder)
        {
            return typeBuilder.CreateTypeInfo();
        }

        public static T GetCustomAttribute<T>(this Type type) where T: Attribute
        {
            return type.GetCustomAttributes(typeof(T), inherit: false)
                .Cast<T>()
                .SingleOrDefault();
        }

        public static T GetCustomAttribute<T>(this Assembly assembly) where T: Attribute
        {
            return assembly.GetCustomAttributes(typeof(T), inherit: false)
                .Cast<T>()
                .SingleOrDefault();
        }

        public static bool IsFlagsEnum(this Type type)
            => type.GetCustomAttribute<FlagsAttribute>() is not null;
    }
}
