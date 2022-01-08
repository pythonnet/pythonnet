using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Python.Runtime.Reflection;

[Serializable]
class ParameterHelper : IEquatable<ParameterInfo>
{
    public readonly string TypeName;
    public readonly ParameterModifier Modifier;
    public readonly ParameterHelper[]? GenericArguments;

    public ParameterHelper(ParameterInfo tp) : this(tp.ParameterType)
    {
        Modifier = ParameterModifier.None;

        if (tp.IsIn && tp.ParameterType.IsByRef)
        {
            Modifier = ParameterModifier.In;
        }
        else if (tp.IsOut && tp.ParameterType.IsByRef)
        {
            Modifier = ParameterModifier.Out;
        }
        else if (tp.ParameterType.IsByRef)
        {
            Modifier = ParameterModifier.Ref;
        }
    }

    public ParameterHelper(Type type)
    {
        TypeName = type.AssemblyQualifiedName;
        if (TypeName is null)
        {
            if (type.IsByRef || type.IsArray)
            {
                TypeName = type.IsArray ? "[]" : "&";
                GenericArguments = new[] { new ParameterHelper(type.GetElementType()) };
            }
            else
            {
                Debug.Assert(type.ContainsGenericParameters);
                TypeName = $"{type.Assembly}::{type.Namespace}/{type.Name}";
                GenericArguments = type.GenericTypeArguments.Select(t => new ParameterHelper(t)).ToArray();
            }
        }
    }

    public bool IsSpecialType => TypeName == "&" || TypeName == "[]";

    public bool Equals(ParameterInfo other)
    {
        return this.Equals(new ParameterHelper(other));
    }

    public bool Matches(ParameterInfo other) => this.Equals(other);

    public bool Equals(ParameterHelper other)
    {
        if (other is null) return false;

        if (!(other.TypeName == TypeName && other.Modifier == Modifier))
            return false;

        if (GenericArguments == other.GenericArguments) return true;

        if (GenericArguments is not null && other.GenericArguments is not null)
        {
            if (GenericArguments.Length != other.GenericArguments.Length) return false;
            for (int arg = 0; arg < GenericArguments.Length; arg++)
            {
                if (!GenericArguments[arg].Equals(other.GenericArguments[arg])) return false;
            }
            return true;
        }

        return false;
    }
}

enum ParameterModifier
{
    None,
    In,
    Out,
    Ref
}
