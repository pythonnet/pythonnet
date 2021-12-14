using System;
using System.Collections.Generic;
using System.Linq;

namespace Python.Runtime.Mixins
{
    class CollectionMixinsProvider : IPythonBaseTypeProvider, IDisposable
    {
        readonly Lazy<PyObject> mixinsModule;
        public CollectionMixinsProvider(Lazy<PyObject> mixinsModule)
        {
            this.mixinsModule = mixinsModule ?? throw new ArgumentNullException(nameof(mixinsModule));
        }

        public PyObject Mixins => this.mixinsModule.Value;

        public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (existingBases is null)
                throw new ArgumentNullException(nameof(existingBases));

            var interfaces = NewInterfaces(type).Select(GetDefinition).ToArray();

            var newBases = new List<PyType>();
            newBases.AddRange(existingBases);

            // dictionaries
            if (interfaces.Contains(typeof(IDictionary<,>)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("MutableMappingMixin")));
            }
            else if (interfaces.Contains(typeof(IReadOnlyDictionary<,>)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("MappingMixin")));
            }

            // item collections
            if (interfaces.Contains(typeof(IList<>))
                || interfaces.Contains(typeof(System.Collections.IList)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("MutableSequenceMixin")));
            }
            else if (interfaces.Contains(typeof(IReadOnlyList<>)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("SequenceMixin")));
            }
            else if (interfaces.Contains(typeof(ICollection<>))
                     || interfaces.Contains(typeof(System.Collections.ICollection)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("CollectionMixin")));
            }
            else if (interfaces.Contains(typeof(System.Collections.IEnumerable)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("IterableMixin")));
            }

            // enumerators
            if (interfaces.Contains(typeof(System.Collections.IEnumerator)))
            {
                newBases.Add(new PyType(this.Mixins.GetAttr("IteratorMixin")));
            }

            if (newBases.Count == existingBases.Count)
            {
                return existingBases;
            }

            if (type.IsInterface && type.BaseType is null)
            {
                newBases.RemoveAll(@base => PythonReferenceComparer.Instance.Equals(@base, Runtime.PyBaseObjectType));
            }

            return newBases;
        }

        static Type[] NewInterfaces(Type type)
        {
            var result = type.GetInterfaces();
            return type.BaseType != null
                ? result.Except(type.BaseType.GetInterfaces()).ToArray()
                : result;
        }

        static Type GetDefinition(Type type)
            => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        public void Dispose()
        {
            if (this.mixinsModule.IsValueCreated)
            {
                this.mixinsModule.Value.Dispose();
            }
        }
    }
}
