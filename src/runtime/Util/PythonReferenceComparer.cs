using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Compares Python object wrappers by Python object references.
    /// <para>Similar to <see cref="object.ReferenceEquals"/> but for Python objects</para>
    /// </summary>
    [Serializable]
    public sealed class PythonReferenceComparer : IEqualityComparer<PyObject>
    {
        public static PythonReferenceComparer Instance { get; } = new PythonReferenceComparer();
        public bool Equals(PyObject? x, PyObject? y)
        {
            return x?.rawPtr == y?.rawPtr;
        }

        public int GetHashCode(PyObject obj) => obj.rawPtr.GetHashCode();

        private PythonReferenceComparer() { }
    }
}
