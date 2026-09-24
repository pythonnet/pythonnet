namespace Python.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Python.Runtime.Mixins;

    public sealed class InteropConfiguration: IDisposable
    {
        internal readonly PythonBaseTypeProviderGroup pythonBaseTypeProviders
            = new();

        /// <summary>Enables replacing base types of CLR types as seen from Python</summary>
        public IList<IPythonBaseTypeProvider> PythonBaseTypeProviders => this.pythonBaseTypeProviders;

        internal readonly ClrTypeFilterGroup clrTypeFilters = new();

        /// <summary>
        /// Restricts which CLR types are reflected into Python. Empty by default,
        /// so every type is reflected; a type is presented to Python only if every
        /// registered filter permits it.
        /// </summary>
        public IList<IClrTypeFilter> ClrTypeFilters => this.clrTypeFilters;

        public static InteropConfiguration MakeDefault()
        {
            return new InteropConfiguration
            {
                PythonBaseTypeProviders =
                {
                    DefaultBaseTypeProvider.Instance,
                    new CollectionMixinsProvider(new Lazy<PyObject>(() => Py.Import("clr._extras.collections"))),
                    new DynamicObjectMixinsProvider(new Lazy<PyObject>(() => Py.Import("clr._extras.dlr"))),
                },
            };
        }

        public void Dispose()
        {
            foreach (var provider in PythonBaseTypeProviders.OfType<IDisposable>())
            {
                provider.Dispose();
            }
            PythonBaseTypeProviders.Clear();
        }
    }
}
