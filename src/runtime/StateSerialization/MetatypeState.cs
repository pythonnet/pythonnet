using System;

namespace Python.Runtime.StateSerialization;

// Workaround for the lack of required properties: https://github.com/dotnet/csharplang/issues/3630
// Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8618

[Serializable]
internal class MetatypeState
{
    public PyType CLRMetaType { get; set; }
}
