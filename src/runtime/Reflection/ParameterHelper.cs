using System;
using System.Collections.Generic;
using System.Reflection;

namespace Python.Runtime.Reflection;

[Serializable]
struct ParameterHelper : IEquatable<ParameterInfo>
{
    public readonly string TypeName;
    public readonly ParameterModifier Modifier;

    public ParameterHelper(ParameterInfo tp)
    {
        TypeName = tp.ParameterType.AssemblyQualifiedName;
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

    public bool Equals(ParameterInfo other)
    {
        return this.Equals(new ParameterHelper(other));
    }

    public bool Matches(ParameterInfo other) => this.Equals(other);
}

enum ParameterModifier
{
    None,
    In,
    Out,
    Ref
}
