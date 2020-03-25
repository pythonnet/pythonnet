using System;
using System.Collections.Generic;

namespace Python.Runtime.CollectionWrappers
{
    internal class ListWrapper<T> : SequenceWrapper<T>, IList<T>
    {
        public ListWrapper(PyObject pyObj) : base(pyObj)
        {

        }

        public T this[int index]
        {
            get
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
            set
            {
                IntPtr pyItem = Converter.ToPython(value, typeof(T));
                if (pyItem == IntPtr.Zero)
                    throw new Exception("failed to set item");

                var result = Runtime.PySequence_SetItem(pyObject.Handle, index, pyItem);
                Runtime.XDecref(pyItem);
                if (result == -1)
                    throw new Exception("failed to set item");
            }
        }

        public int IndexOf(T item)
        {
            return indexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (IsReadOnly)
                throw new NotImplementedException();

            IntPtr pyItem = Converter.ToPython(item, typeof(T));
            if (pyItem == IntPtr.Zero)
                throw new Exception("failed to insert item");

            var result = Runtime.PyList_Insert(pyObject.Reference, index, pyItem);
            Runtime.XDecref(pyItem);
            if (result == -1)
                throw new Exception("failed to insert item");
        }

        public void RemoveAt(int index)
        {
            removeAt(index);
        }
    }
}
