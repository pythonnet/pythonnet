using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class SharedObjectsState
{
    public Dictionary<PyObject, CLRObject> InternalStores { get; set; }
    public Dictionary<PyObject, ExtensionType> Extensions { get; set; }
    public RuntimeDataStorage Wrappers { get; set; }
    public Dictionary<PyObject, InterDomainContext> Contexts { get; set; }
}
