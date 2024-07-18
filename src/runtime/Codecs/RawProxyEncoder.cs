using System;

namespace Python.Runtime.Codecs
{
    /// <summary>
    /// A .NET object encoder, that returns raw proxies (e.g. no conversion to Python types).
    /// <para>You must inherit from this class and override <see cref="CanEncode"/>.</para>
    /// </summary>
    public class RawProxyEncoder: IPyObjectEncoder
    {
        public PyObject TryEncode(object value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            return PyObject.FromManagedObject(value);
        }

        public virtual bool CanEncode(Type type) => false;
    }
}
