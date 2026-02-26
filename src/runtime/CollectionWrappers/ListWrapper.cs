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
                using var _ = Py.GIL();
                var item = Runtime.PyList_GetItem(pyObject, index);
                var pyItem = new PyObject(item);
                return pyItem.As<T>()!;
            }
            set
            {
                using var _ = Py.GIL();
                var pyItem = value.ToPython();
                var result = Runtime.PyList_SetItem(pyObject, index, new NewReference(pyItem).Steal());
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

            using var _ = Py.GIL();
            var pyItem = item.ToPython();

            int result = Runtime.PyList_Insert(pyObject, index, pyItem);
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
