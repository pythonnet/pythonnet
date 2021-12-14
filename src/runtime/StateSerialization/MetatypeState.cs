using System;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class MetatypeState
{
    public PyType CLRMetaType { get; set; }
}
