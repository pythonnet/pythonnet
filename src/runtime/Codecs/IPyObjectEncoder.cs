namespace Python.Runtime;

using System;

/// <summary>
/// Defines conversion from CLR objects into Python objects (e.g. <see cref="PyObject"/>) (marshalling)
/// </summary>
public interface IPyObjectEncoder
{
    /// <summary>
    /// Checks if encoder can encode CLR objects of specified type
    /// </summary>
    bool CanEncode(Type type);
    /// <summary>
    /// Attempts to encode CLR object <paramref name="value"/> into Python object
    /// </summary>
    PyObject? TryEncode(object value);
}
