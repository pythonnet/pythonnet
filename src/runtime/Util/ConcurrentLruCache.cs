using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Python.Runtime;

internal sealed class ConcurrentLruCache<TKey, TValue> where TKey : notnull
{
    readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> map = new();
    readonly LinkedList<CacheItem> lru = new();
    readonly object gate = new();

    sealed record CacheItem(TKey Key, TValue Value);

    public ConcurrentLruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        Capacity = capacity;
    }

    public int Capacity { get; private set; }

    public int Count => map.Count;

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        if (valueFactory is null)
            throw new ArgumentNullException(nameof(valueFactory));

        if (TryGetValue(key, out var existing))
            return existing;

        var created = valueFactory(key);

        lock (gate)
        {
            if (map.TryGetValue(key, out var alreadyAdded))
            {
                MoveToFront(alreadyAdded);
                return alreadyAdded.Value.Value;
            }

            var item = new CacheItem(key, created);
            var node = new LinkedListNode<CacheItem>(item);
            lru.AddFirst(node);
            map[key] = node;
            EvictOverflow();
            return created;
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (map.TryGetValue(key, out var node))
        {
            lock (gate)
            {
                if (map.TryGetValue(key, out node))
                {
                    MoveToFront(node);
                    value = node.Value.Value;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }

    public void Clear()
    {
        lock (gate)
        {
            lru.Clear();
            map.Clear();
        }
    }

    void MoveToFront(LinkedListNode<CacheItem> node)
    {
        if (ReferenceEquals(lru.First, node))
            return;

        lru.Remove(node);
        lru.AddFirst(node);
    }

    void EvictOverflow()
    {
        while (map.Count > Capacity)
        {
            var last = lru.Last;
            if (last is null)
                return;

            lru.RemoveLast();
            map.TryRemove(last.Value.Key, out _);
        }
    }
}
