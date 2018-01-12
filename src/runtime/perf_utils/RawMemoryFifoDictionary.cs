namespace Python.Runtime
{
    using System;
    using System.Runtime.InteropServices;

    public class RawMemoryFifoDictionary<TValue> : IDisposable
    {
        private readonly FifoDictionary<RawImmutableMemBlock, TValue> _innerDictionary;

        private readonly IntPtr _rawMemory;

        public RawMemoryFifoDictionary(int capacity, int maxItemSize)
        {
            if (maxItemSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxItemSize),
                    "Maximum item size should be non-zero positive.");
            }

            MaxItemSize = maxItemSize;
            _innerDictionary = new FifoDictionary<RawImmutableMemBlock, TValue>(capacity);
            _rawMemory = Marshal.AllocHGlobal(maxItemSize*capacity);
        }

        ~RawMemoryFifoDictionary()
        {
            ReleaseUnmanagedResources();
        }

        public int MaxItemSize { get; }

        public bool TryGetValue(RawImmutableMemBlock key, out TValue value)
        {
            return _innerDictionary.TryGetValue(key, out value);
        }

        public void AddUnsafe(RawImmutableMemBlock key, TValue value)
        {
            int nextSlot = _innerDictionary.NextSlotToAdd;
            var localKey = new RawImmutableMemBlock(key, _rawMemory + (MaxItemSize * nextSlot));
            _innerDictionary.AddUnsafe(localKey, value);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            if (_rawMemory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_rawMemory);
            }
        }
    }
}
