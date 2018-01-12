using System;

namespace Python.Runtime
{
    using System.Collections.Generic;
    
    public class FifoDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, int> _innerDictionary;

        private readonly KeyValuePair<TKey,TValue>[] _fifoList;

        private bool _hasEmptySlots = true;

        public FifoDictionary(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity should be non-zero positive.");
            }

            _innerDictionary = new Dictionary<TKey, int>(capacity);
            _fifoList = new KeyValuePair<TKey, TValue>[capacity];

            Capacity = capacity;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int index;
            if (_innerDictionary.TryGetValue(key, out index))
            {
                value =_fifoList[index].Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        public void AddUnsafe(TKey key, TValue value)
        {
            if (!_hasEmptySlots)
            {
                _innerDictionary.Remove(_fifoList[NextSlotToAdd].Key);
            }
            
            _innerDictionary.Add(key, NextSlotToAdd);
            _fifoList[NextSlotToAdd] = new KeyValuePair<TKey, TValue>(key, value);

            NextSlotToAdd++;
            if (NextSlotToAdd >= Capacity)
            {
                _hasEmptySlots = false;
                NextSlotToAdd = 0;
            }
        }

        public int NextSlotToAdd { get; private set; }
        public int Capacity { get; }
    }
}
