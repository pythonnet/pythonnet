using System.Collections.Generic;

namespace Python.Runtime;

public class CLRMappedItem
{
    public object Instance { get; private set; }
    public List<PyObject> PyRefs { get; set; } = new List<PyObject>();
    public bool Stored { get; set; }

    public CLRMappedItem(object instance)
    {
        Instance = instance;
    }

    internal void AddRef(PyObject pyRef)
    {
        this.PyRefs.Add(pyRef);
    }
}
