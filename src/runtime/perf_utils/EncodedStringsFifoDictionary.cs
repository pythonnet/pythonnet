using System;

namespace Python.Runtime
{
    using System.Runtime.InteropServices;

    public class EncodedStringsFifoDictionary: IDisposable
    {
        private readonly FifoDictionary<string, IntPtr> _innerDictionary;

        private readonly IntPtr _rawMemory;

        private readonly int _allocatedSize;

        public EncodedStringsFifoDictionary(int capacity, int maxItemSize)
        {
            if (maxItemSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxItemSize),
                    "Maximum item size should be non-zero positive.");
            }

            _innerDictionary = new FifoDictionary<string, IntPtr>(capacity);
            _allocatedSize = maxItemSize * capacity;
            _rawMemory = Marshal.AllocHGlobal(_allocatedSize);

            MaxItemSize = maxItemSize;
        }

        public int MaxItemSize { get; }

        public bool TryGetValue(string key, out IntPtr value)
        {
            return _innerDictionary.TryGetValue(key, out value);
        }

        public IntPtr AddUnsafe(string key)
        {
            int nextSlot = _innerDictionary.NextSlotToAdd;
            IntPtr ptr = _rawMemory + (MaxItemSize * nextSlot);
            _innerDictionary.AddUnsafe(key, ptr);
            return ptr;
        }

        public bool IsKnownPtr(IntPtr ptr)
        {
            var uptr = (ulong)ptr;
            var umem = (ulong)_rawMemory;

            return uptr >= umem && uptr < umem + (ulong)_allocatedSize;
        }

        private void ReleaseUnmanagedResources()
        {
            if (_rawMemory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_rawMemory);
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~EncodedStringsFifoDictionary()
        {
            ReleaseUnmanagedResources();
        }
    }
}
