using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

// Workaround for the lack of required properties: https://github.com/dotnet/csharplang/issues/3630
// Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8618

[Serializable]
internal class SharedObjectsState
{
    public Dictionary<PyObject, CLRObject> InternalStores { get; init; }
    public Dictionary<PyObject, ExtensionType> Extensions { get; init; }
    public Dictionary<string, object?> Wrappers { get; init; }
    public Dictionary<PyObject, Dictionary<string, object?>> Contexts { get; init; }
}
