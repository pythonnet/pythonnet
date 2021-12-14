using System;
using System.Collections.Generic;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class ImportHookState
{
    public PyModule PyCLRModule { get; set; }
    public PyObject Root { get; set; }
    public Dictionary<PyString, PyObject> Modules { get; set; }
}
