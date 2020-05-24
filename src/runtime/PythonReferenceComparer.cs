#nullable enable
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Compares Python object wrappers by Python object references.
    /// <para>Similar to <see cref="object.ReferenceEquals"/> but for Python objects</para>
    /// </summary>
    public sealed class PythonReferenceComparer : IEqualityComparer<PyObject>
    {
        public static PythonReferenceComparer Instance { get; } = new PythonReferenceComparer();
        public bool Equals(PyObject? x, PyObject? y)
        {
            return x?.Handle == y?.Handle;
        }

        public int GetHashCode(PyObject obj) => obj.Handle.GetHashCode();

        private PythonReferenceComparer() { }
    }
}
