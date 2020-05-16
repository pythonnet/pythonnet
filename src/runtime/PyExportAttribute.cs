namespace Python.Runtime {
    using System;

    /// <summary>
    /// Controls visibility to Python for public .NET type or an entire assembly
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Enum
                    | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Assembly,
        AllowMultiple = false,
        Inherited = false)]
    public class PyExportAttribute : Attribute
    {
        internal readonly bool Export;
        public PyExportAttribute(bool export) { this.Export = export; }
    }
}
