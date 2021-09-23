namespace Python.Runtime
{
    using System;
    using System.Collections.Generic;

    using Python.Runtime.Mixins;

    public sealed class InteropConfiguration
    {
        internal readonly PythonBaseTypeProviderGroup pythonBaseTypeProviders
            = new PythonBaseTypeProviderGroup();

        /// <summary>Enables replacing base types of CLR types as seen from Python</summary>
        public IList<IPythonBaseTypeProvider> PythonBaseTypeProviders => this.pythonBaseTypeProviders;

        public static InteropConfiguration MakeDefault()
        {
            return new InteropConfiguration
            {
                PythonBaseTypeProviders =
                {
                    DefaultBaseTypeProvider.Instance,
                    new CollectionMixinsProvider(new Lazy<PyObject>(() => Py.Import("clr._extras.collections"))),
                },
            };
        }
    }
}
