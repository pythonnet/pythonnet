namespace Python.Runtime.Codecs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a group of <see cref="IPyObjectDecoder"/>s. Useful to group them by priority.
    /// </summary>
    public sealed class EncoderGroup: IPyObjectEncoder, IEnumerable<IPyObjectEncoder>, IDisposable
    {
        readonly List<IPyObjectEncoder> encoders = new();

        /// <summary>
        /// Add specified encoder to the group
        /// </summary>
        public void Add(IPyObjectEncoder item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            this.encoders.Add(item);
        }
        /// <summary>
        /// Remove all encoders from the group
        /// </summary>
        public void Clear() => this.encoders.Clear();

        /// <inheritdoc />
        public bool CanEncode(Type type) => this.encoders.Any(encoder => encoder.CanEncode(type));
        /// <inheritdoc />
        public PyObject? TryEncode(object value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            foreach (var encoder in this.GetEncoders(value.GetType()))
            {
                var result = encoder.TryEncode(value);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public IEnumerator<IPyObjectEncoder> GetEnumerator() => this.encoders.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.encoders.GetEnumerator();

        public void Dispose()
        {
            foreach (var encoder in this.encoders.OfType<IDisposable>())
            {
                encoder.Dispose();
            }
            this.encoders.Clear();
        }
    }

    public static class EncoderGroupExtensions
    {
        /// <summary>
        /// Gets specific instances of <see cref="IPyObjectEncoder"/>
        /// (potentially selecting one from a collection),
        /// that can encode the specified <paramref name="type"/>.
        /// </summary>
        public static IEnumerable<IPyObjectEncoder> GetEncoders(this IPyObjectEncoder decoder, Type type)
        {
            if (decoder is null) throw new ArgumentNullException(nameof(decoder));

            if (decoder is IEnumerable<IPyObjectEncoder> composite)
            {
                foreach (var nestedEncoder in composite)
                foreach (var match in nestedEncoder.GetEncoders(type))
                {
                    yield return match;
                }
            } else if (decoder.CanEncode(type))
            {
                yield return decoder;
            }
        }
    }
}
