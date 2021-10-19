using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class SharedObjectsState
{
    public List<CLRObject> InternalStores { get; set; }
    public List<ManagedType> Extensions { get; set; }
    public RuntimeDataStorage Wrappers { get; set; }
    public Dictionary<PyObject, InterDomainContext> Contexts { get; set; }
}
