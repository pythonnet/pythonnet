using System;

namespace Python.Runtime
{
    /// <summary>
    /// Decides whether a CLR type may be reflected into Python.
    /// <para>
    /// Filters are consulted when a type would first be presented to Python.
    /// Permitted types are cached for the lifetime of the engine and not
    /// re-checked; a refused type is checked again on each attempt
    /// </para>
    /// <para>
    /// This is a policy hook for embedders, not a security boundary: Python code
    /// holding any reflected object can still reach whatever that object's own
    /// API exposes.
    /// </para>
    /// </summary>
    public interface IClrTypeFilter
    {
        /// <summary>
        /// Whether <paramref name="type"/> may be reflected into Python. Returning
        /// false causes an attempt to present the type to Python to fail.
        /// </summary>
        bool ShouldReflect(Type type);
    }
}
