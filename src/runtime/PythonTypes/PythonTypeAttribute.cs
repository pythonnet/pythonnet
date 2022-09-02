using System;

namespace Python.Runtime;

/// <summary>
/// Marks a property type with a specific python type. Normally, properties has .NET types, but if the property has a python type,
/// that cannot be represented in the propert type info, so this attribute is used to mark the property with the corresponding python type.
/// </summary>
public class PythonTypeAttribute : Attribute
{
    /// <summary> Type name. </summary>
    public string TypeName { get; }

    /// <summary> Importable module name. </summary>
    public string Module { get; }

    /// <summary>
    /// Creates a new instance of PythonTypeAttribute.
    /// </summary>
    /// <param name="pyTypeModule"></param>
    /// <param name="pyTypeName"></param>
    public PythonTypeAttribute(string pyTypeModule, string pyTypeName)
    {
        TypeName = pyTypeName;
        Module = pyTypeModule;
    }
}
