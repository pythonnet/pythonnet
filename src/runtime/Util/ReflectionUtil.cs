namespace Python.Runtime;

using System;
using System.Reflection;

static class ReflectionUtil
{
    public static MethodInfo? GetBaseGetMethod(this PropertyInfo property, bool nonPublic)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        Type baseType = property.DeclaringType.BaseType;
        BindingFlags bindingFlags = property.GetBindingFlags();

        while (baseType is not null)
        {
            var baseProperty = baseType.GetProperty(property.Name, bindingFlags | BindingFlags.DeclaredOnly);
            var accessor = baseProperty?.GetGetMethod(nonPublic);
            if (accessor is not null)
                return accessor;

            baseType = baseType.BaseType;
        }

        return null;
    }

    public static MethodInfo? GetBaseSetMethod(this PropertyInfo property, bool nonPublic)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        Type baseType = property.DeclaringType.BaseType;
        BindingFlags bindingFlags = property.GetBindingFlags();

        while (baseType is not null)
        {
            var baseProperty = baseType.GetProperty(property.Name, bindingFlags | BindingFlags.DeclaredOnly);
            var accessor = baseProperty?.GetSetMethod(nonPublic);
            if (accessor is not null)
                return accessor;

            baseType = baseType.BaseType;
        }

        return null;
    }

    public static BindingFlags GetBindingFlags(this PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        BindingFlags flags = default;
        flags |= accessor.IsStatic ? BindingFlags.Static : BindingFlags.Instance;
        flags |= accessor.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
        return flags;
    }
}
