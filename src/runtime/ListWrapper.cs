using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime {
    /// <summary>
    /// Implements IEnumerable<typeparamref name="T"/> for any python iterable.
    /// </summary>
    internal class IterableWrapper<T> : IEnumerable<T> {
        private IntPtr iterObject;

        public IterableWrapper(IntPtr value) {
            iterObject = Runtime.PyObject_GetIter(value);
            if (iterObject == IntPtr.Zero)
                Exceptions.RaiseTypeError("not an iterator");
            Runtime.XIncref(iterObject);
        }
        ~IterableWrapper() {
            Runtime.XDecref(iterObject);
        }

        public IEnumerator<T> GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            IntPtr item;
            while ((item = Runtime.PyIter_Next(iterObject)) != IntPtr.Zero) {
                object obj = null;
                if (!Converter.ToManaged(item, typeof(T), out obj, true)) {
                    Runtime.XDecref(item);
                    Runtime.XDecref(iterObject);
                    Exceptions.RaiseTypeError("wrong type in sequence");
                }

                Runtime.XDecref(item);
                yield return obj;
            }
        }
    }

    /// <summary>
    /// Implements IList<typeparamref name="T"/> for any python sequence.
    /// Some methods/properties are only available on certaintypes of sequences, like list
    /// </summary>
    internal class ListWrapper<T> : IterableWrapper<T>, IList<T>
    {
        private IntPtr seq;
        public ListWrapper(IntPtr value) : base(value)
        {
            this.seq = value;
            Runtime.XIncref(value);
            bool IsSeqObj = Runtime.PySequence_Check(value);
            if (!IsSeqObj)
                Exceptions.RaiseTypeError("not a sequence");

        }
        ~ListWrapper()
        {
            Runtime.XDecref(seq);
        }
        public T this[int index]
        {
            get
            {
                IntPtr item = Runtime.PySequence_GetItem(seq, index);
                object obj;

                if (!Converter.ToManaged(item, typeof(T), out obj, true)) {
                    Runtime.XDecref(item);
                    Exceptions.RaiseTypeError("wrong type in sequence");
                }

                return (T)obj;
            }
            set
            {
                IntPtr pyItem = Converter.ToPython(value, typeof(T));
                if (pyItem == IntPtr.Zero)
                    throw new Exception("failed to set item");

                var result = Runtime.PySequence_SetItem(seq, index, pyItem);
                Runtime.XDecref(pyItem);
                if (result == -1)
                    throw new Exception("failed to set item");
            }
        }

        public int Count
        {
            get
            {
                var len = Runtime.PySequence_Size(seq);
                return (int)len;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return Runtime.PyTuple_Check(seq); //python tuples are immutable
            }

        }

        public void Add(T item) {
            if (IsReadOnly)
                throw new NotImplementedException();

            //only implemented if this is a list!
            if (!Runtime.PyList_Check(seq))
                throw new NotImplementedException();

            IntPtr pyItem = Converter.ToPython(item, typeof(T));
            if (pyItem == IntPtr.Zero)
                throw new Exception("failed to add item");

            var result = Runtime.PyList_Append(seq, pyItem);
            Runtime.XDecref(pyItem);
            if (result == -1)
                throw new Exception("failed to add item");
        }

        public void Clear() {
            if (IsReadOnly)
                throw new NotImplementedException();
            var result = Runtime.PySequence_DelSlice(seq, 0, Count);
            if (result == -1)
                throw new Exception("failed to clear sequence");
        }

        public bool Contains(T item)
        {
            //not sure if IEquatable is implemented and this will work!
            foreach (var element in this)
                if (element.Equals(item)) return true;

            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int index = 0; index < Count; index++)
            {
                array[index + arrayIndex] = this[index];
            }
        }

        public int IndexOf(T item) {
            var index = 0;
            foreach (var element in this) {
                if (element.Equals(item)) return index;
                index++;
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            if (IsReadOnly)
                throw new NotImplementedException();

            //only implemented if this is a list!
            if (!Runtime.PyList_Check(seq))
                throw new NotImplementedException();

            IntPtr pyItem = Converter.ToPython(item, typeof(T));
            if (pyItem == IntPtr.Zero)
                throw new Exception("failed to insert item");

            var result = Runtime.PyList_Insert(seq, index, pyItem);
            Runtime.XDecref(pyItem);
            if (result == -1)
                throw new Exception("failed to insert item");
        }

        public bool InternalRemoveAt(int index)
        {
            if (IsReadOnly)
                throw new NotImplementedException();
            if (index >= Count || index < 0)
                throw new IndexOutOfRangeException();

            return Runtime.PySequence_DelItem(seq, index) != 0;
        }

        public bool Remove(T item)
        {
            return InternalRemoveAt(IndexOf(item));
        }

        public void RemoveAt(int index)
        {
            InternalRemoveAt(index);
        }
    }
}
