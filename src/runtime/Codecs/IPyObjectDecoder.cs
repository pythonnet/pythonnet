namespace Python.Runtime;

using System;

/// <summary>
/// Defines <see cref="PyObject"/> conversion to CLR types (unmarshalling)
/// </summary>
public interface IPyObjectDecoder
{
    /// <summary>
    /// Checks if this decoder can decode from <paramref name="objectType"/> to <paramref name="targetType"/>
    /// </summary>
    bool CanDecode(PyType objectType, Type targetType);
    /// <summary>
    /// Attempts do decode <paramref name="pyObj"/> into a variable of specified type
    /// </summary>
    /// <typeparam name="T">CLR type to decode into</typeparam>
    /// <param name="pyObj">Object to decode</param>
    /// <param name="value">The variable, that will receive decoding result</param>
    /// <returns></returns>
    bool TryDecode<T>(PyObject pyObj, out T? value);
}
