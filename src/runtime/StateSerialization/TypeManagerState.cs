using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class TypeManagerState
{
    public Dictionary<MaybeType, PyType> Cache { get; set; }
}
