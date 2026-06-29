using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Python.Runtime.Mixins;

class DynamicObjectMixinsProvider : IPythonBaseTypeProvider, IDisposable
{
    readonly Lazy<PyObject> mixinsModule;

    public DynamicObjectMixinsProvider(Lazy<PyObject> mixinsModule) =>
		this.mixinsModule = mixinsModule ?? throw new ArgumentNullException(nameof(mixinsModule));

    public PyObject Mixins => mixinsModule.Value;

    public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));

        if (existingBases is null)
            throw new ArgumentNullException(nameof(existingBases));

        if (!typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type))
            return existingBases;

        var newBases = new List<PyType>(existingBases)
        {
            new(Mixins.GetAttr("DynamicMetaObjectProviderMixin"))
        };

        if (type.IsInterface && type.BaseType is null)
        {
            newBases.RemoveAll(@base => PythonReferenceComparer.Instance.Equals(@base, Runtime.PyBaseObjectType));
        }

        return newBases;
    }

    public void Dispose()
    {
        if (this.mixinsModule.IsValueCreated)
        {
            this.mixinsModule.Value.Dispose();
        }
    }
}
