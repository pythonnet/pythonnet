using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class ClassManagerState
{
    public Dictionary<PyType, InterDomainContext> Contexts { get; set; }
    public Dictionary<MaybeType, PyType> Cache { get; set; }
}
