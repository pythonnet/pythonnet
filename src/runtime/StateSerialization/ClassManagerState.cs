using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class ClassManagerState
{
    public Dictionary<ReflectedClrType, InterDomainContext> Contexts { get; set; }
    public Dictionary<MaybeType, ReflectedClrType> Cache { get; set; }
}
