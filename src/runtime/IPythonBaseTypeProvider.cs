using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    public interface IPythonBaseTypeProvider
    {
        /// <summary>
        /// Get Python types, that should be presented to Python as the base types
        /// for the specified .NET type.
        /// </summary>
        IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases);
    }
}
