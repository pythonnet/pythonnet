namespace Python.Test
{
    using Python.Runtime;

    // this class should not be visible to Python
    [PyExport(false)]
    public class NonExportable { }
}
