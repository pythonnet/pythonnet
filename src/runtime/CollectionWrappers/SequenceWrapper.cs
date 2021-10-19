using System;
using System.Collections.Generic;

namespace Python.Runtime.CollectionWrappers
{
    internal class SequenceWrapper<T> : IterableWrapper<T>, ICollection<T>
    {
        public SequenceWrapper(PyObject pyObj) : base(pyObj)
        {

        }

        public int Count
        {
            get
            {
                var size = Runtime.PySequence_Size(pyObject.Reference);
                if (size == -1)
                {
                    Runtime.CheckExceptionOccurred();
                }

                return checked((int)size);
            }
        }

        public virtual bool IsReadOnly => false;

        public virtual void Add(T item)
        {
            //not implemented for Python sequence.
            //ICollection is the closest analogue but it doesn't map perfectly.
            //SequenceWrapper is not final and can be subclassed if necessary
            throw new NotImplementedException();
        }

        public void Clear()
        {
            if (IsReadOnly)
                throw new NotImplementedException();
            int result = Runtime.PySequence_DelSlice(pyObject, 0, Count);
            if (result == -1)
            {
                Runtime.CheckExceptionOccurred();
            }
        }

        public bool Contains(T item)
        {
            //not sure if IEquatable is implemented and this will work!
            foreach (var element in this)
                if (object.Equals(element, item)) return true;

            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new NullReferenceException();

            if ((array.Length - arrayIndex) < this.Count)
                throw new InvalidOperationException("Attempting to copy to an array that is too small");

            var index = 0;
            foreach (var item in this)
            {
                array[index + arrayIndex] = item;
                index++;
            }
        }

        protected bool removeAt(int index)
        {
            if (IsReadOnly)
                throw new NotImplementedException();
            if (index >= Count || index < 0)
                return false;

            int result = Runtime.PySequence_DelItem(pyObject, index);

            if (result == 0)
                return true;

            Runtime.CheckExceptionOccurred();
            return false;
        }

        protected int indexOf(T item)
        {
            var index = 0;
            foreach (var element in this)
            {
                if (object.Equals(element, item)) return index;
                index++;
            }

            return -1;
        }

        public bool Remove(T item)
        {
            var result = removeAt(indexOf(item));

            //clear the python exception from PySequence_DelItem
            //it is idiomatic in C# to return a bool rather than
            //throw for a failed Remove in ICollection
            if (result == false)
                Runtime.PyErr_Clear();
            return result;
        }
    }
}
