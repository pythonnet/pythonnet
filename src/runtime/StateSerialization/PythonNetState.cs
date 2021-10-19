using System;

namespace Python.Runtime.StateSerialization;

[Serializable]
internal class PythonNetState
{
    public MetatypeState Metatype { get; set; }
    public SharedObjectsState SharedObjects { get; set; }
    public TypeManagerState Types { get; set; }
    public ClassManagerState Classes { get; set; }
    public ImportHookState ImportHookState { get; set; }
}
