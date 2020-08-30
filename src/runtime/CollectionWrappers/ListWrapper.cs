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
                var item = Runtime.PyList_GetItem(pyObject.Reference, index);
                var pyItem = new PyObject(item);

                if (!Converter.ToManaged(pyItem.Handle, typeof(T), out object obj, true))
                    Runtime.CheckExceptionOccurred();

                return (T)obj;
            }
            set
            {
                IntPtr pyItem = Converter.ToPython(value, typeof(T));
                if (pyItem == IntPtr.Zero)
                {
                    throw new InvalidCastException(
                        "cannot cast " + value.ToString() + "to type: " + typeof(T).ToString(),
                        new PythonException());
                }

                var result = Runtime.PyList_SetItem(pyObject.Handle, index, pyItem);
                if (result == -1)
                    Runtime.CheckExceptionOccurred();
            }
        }

        public int IndexOf(T item)
        {
            return indexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Collection is read-only");

            IntPtr pyItem = Converter.ToPython(item, typeof(T));
            if (pyItem == IntPtr.Zero)
                throw new PythonException();

            var result = Runtime.PyList_Insert(pyObject.Reference, index, pyItem);
            Runtime.XDecref(pyItem);
            if (result == -1)
                Runtime.CheckExceptionOccurred();
        }

        public void RemoveAt(int index)
        {
            var result = removeAt(index);

            //PySequence_DelItem will set an error if it fails.  throw it here
            //since RemoveAt does not have a bool return value.
            if (result == false)
                Runtime.CheckExceptionOccurred();
        }
    }
}
