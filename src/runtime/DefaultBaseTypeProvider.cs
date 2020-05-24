using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>Minimal Python base type provider</summary>
    public sealed class DefaultBaseTypeProvider : IPythonBaseTypeProvider
    {
        public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            if (existingBases is null)
                throw new ArgumentNullException(nameof(existingBases));
            if (existingBases.Count > 0)
                throw new ArgumentException("To avoid confusion, this type provider requires the initial set of base types to be empty");

            return new[] { new PyType(GetBaseType(type)) };
        }

        static BorrowedReference GetBaseType(Type type)
        {
            if (type == typeof(Exception))
                return new BorrowedReference(Exceptions.Exception);

            return type.BaseType is not null
                ? ClassManager.GetClass(type.BaseType).ObjectReference
                : new BorrowedReference(Runtime.PyBaseObjectType);
        }

        DefaultBaseTypeProvider(){}
        public static DefaultBaseTypeProvider Instance { get; } = new DefaultBaseTypeProvider();
    }
}
