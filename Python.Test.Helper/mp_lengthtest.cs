using System;
using System.Collections;
using System.Collections.Generic;

namespace Python.Test
{
    public class MpLengthCollectionTest : ICollection
    {
        private readonly List<int> items;

        public MpLengthCollectionTest()
        {
            SyncRoot = new object();
            items = new List<int>
            {
                1,
                2,
                3
            };
        }

        public int Count => items.Count;

        public object SyncRoot { get; private set; }

        public bool IsSynchronized => false;

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class MpLengthExplicitCollectionTest : ICollection
    {
        private readonly List<int> items;
        private readonly object syncRoot;

        public MpLengthExplicitCollectionTest()
        {
            syncRoot = new object();
            items = new List<int>
            {
                9,
                10
            };
        }
        int ICollection.Count => items.Count;

        object ICollection.SyncRoot => syncRoot;

        bool ICollection.IsSynchronized => false;

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class MpLengthGenericCollectionTest<T> : ICollection<T>
    {
        private readonly List<T> items;

        public MpLengthGenericCollectionTest() {
            SyncRoot = new object();
            items = new List<T>();
        }

        public int Count => items.Count;

        public object SyncRoot { get; private set; }

        public bool IsSynchronized => false;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            items.Add(item);
        }

        public void Clear()
        {
            items.Clear();
        }

        public bool Contains(T item)
        {
            return items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)items).GetEnumerator();
        }

        public bool Remove(T item)
        {
            return items.Remove(item);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return items.GetEnumerator();
        }
    }

    public class MpLengthExplicitGenericCollectionTest<T> : ICollection<T>
    {
        private readonly List<T> items;

        public MpLengthExplicitGenericCollectionTest()
        {
            items = new List<T>();
        }

        int ICollection<T>.Count => items.Count;

        bool ICollection<T>.IsReadOnly => false;

        public void Add(T item)
        {
            items.Add(item);
        }

        void ICollection<T>.Clear()
        {
            items.Clear();
        }

        bool ICollection<T>.Contains(T item)
        {
            return items.Contains(item);
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)items).GetEnumerator();
        }

        bool ICollection<T>.Remove(T item)
        {
            return items.Remove(item);
        }
    }
}
