using System;
using System.Collections.Generic;
using System.Collections;

namespace Python.Runtime.CollectionWrappers
{
    internal class IterableWrapper<T> : IEnumerable<T>
    {
        protected readonly PyObject pyObject;

        public IterableWrapper(PyObject pyObj)
        {
            if (pyObj == null)
                throw new ArgumentNullException();
            pyObject = new PyObject(pyObj.Reference);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
        {
            PyObject iterObject;
            using (Py.GIL())
            {
                var iter = Runtime.PyObject_GetIter(pyObject.Reference);
                PythonException.ThrowIfIsNull(iter);
                iterObject = iter.MoveToPyObject();
            }

            while (true)
            {
                using (Py.GIL())
                {
                    var item = Runtime.PyIter_Next(iterObject.Handle);
                    if (item == IntPtr.Zero)
                    {
                        Runtime.CheckExceptionOccurred();
                        iterObject.Dispose();
                        break;
                    }

                    yield return (T)new PyObject(item).AsManagedObject(typeof(T));
                }
            }
        }
    }
}
