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

            using (iterObject)
            while (true)
            {
                using (Py.GIL())
                {
                    using var item = Runtime.PyIter_Next(iterObject);
                    if (item.IsNull())
                    {
                        Runtime.CheckExceptionOccurred();
                        iterObject.Dispose();
                        break;
                    }

                    yield return item.MoveToPyObject().As<T>();
                }
            }
        }
    }
}
