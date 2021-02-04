using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Python.Test
{
    /// <summary>
    /// Supports units tests for dictionary __contains__ and __len__
    /// </summary>
    public class PublicDictionaryTest
    {
        public IDictionary<string, int> items;

        public PublicDictionaryTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 }
                .ToDictionary(k => k.ToString(), v => v);
        }
    }


    public class ProtectedDictionaryTest
    {
        protected IDictionary<string, int> items;

        public ProtectedDictionaryTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 }
                .ToDictionary(k => k.ToString(), v => v);
        }
    }


    public class InternalDictionaryTest
    {
        internal IDictionary<string, int> items;

        public InternalDictionaryTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 }
                .ToDictionary(k => k.ToString(), v => v);
        }
    }


    public class PrivateDictionaryTest
    {
        private IDictionary<string, int> items;

        public PrivateDictionaryTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 }
                .ToDictionary(k => k.ToString(), v => v);
        }
    }

    public class InheritedDictionaryTest : IDictionary<string, int>
    {
        private readonly IDictionary<string, int> items;

        public InheritedDictionaryTest()
        {
            items = new int[5] { 0, 1, 2, 3, 4 }
                .ToDictionary(k => k.ToString(), v => v);
        }

        public int this[string key]
        {
            get { return items[key]; }
            set { items[key] = value; }
        }

        public ICollection<string> Keys => items.Keys;

        public ICollection<int> Values => items.Values;

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public void Add(string key, int value) => items.Add(key, value);

        public void Add(KeyValuePair<string, int> item) => items.Add(item);

        public void Clear() => items.Clear();

        public bool Contains(KeyValuePair<string, int> item) => items.Contains(item);

        public bool ContainsKey(string key) => items.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => items.GetEnumerator();

        public bool Remove(string key) => items.Remove(key);

        public bool Remove(KeyValuePair<string, int> item) => items.Remove(item);

        public bool TryGetValue(string key, out int value) => items.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
