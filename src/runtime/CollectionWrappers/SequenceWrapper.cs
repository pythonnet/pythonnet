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
                var size = Runtime.PySequence_Size(pyObject.Handle);
                if (size == -1)
                {
                    Runtime.CheckExceptionOccurred();
                }

                return (int)size;
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
            var result = Runtime.PySequence_DelSlice(pyObject.Handle, 0, Count);
            if (result == -1)
            {
                Runtime.CheckExceptionOccurred();
            }
        }

        public bool Contains(T item)
        {
            //not sure if IEquatable is implemented and this will work!
            foreach (var element in this)
                if (element.Equals(item)) return true;

            return false;
        }

        private T getItem(int index)
        {
            IntPtr item = Runtime.PySequence_GetItem(pyObject.Handle, index);
            object obj;

            if (!Converter.ToManaged(item, typeof(T), out obj, true))
            {
                Runtime.XDecref(item);
                Runtime.CheckExceptionOccurred();
            }

            return (T)obj;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
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
                throw new IndexOutOfRangeException();

            return Runtime.PySequence_DelItem(pyObject.Handle, index) != 0;
        }

        protected int indexOf(T item)
        {
            var index = 0;
            foreach (var element in this)
            {
                if (element.Equals(item)) return index;
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
