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
            pyObject = pyObj;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
        {
            PyObject iterObject = null;
            using (Py.GIL())
            {
                iterObject = new PyObject(Runtime.PyObject_GetIter(pyObject.Handle));
            }

            while (true)
            {
                IntPtr item = IntPtr.Zero;
                using (Py.GIL())
                {
                    item = Runtime.PyIter_Next(iterObject.Handle);
                }
                if (item == IntPtr.Zero) break;

                object obj = null;
                if (!Converter.ToManaged(item, typeof(T), out obj, true))
                {
                    Runtime.XDecref(item);
                    Runtime.CheckExceptionOccurred();
                }

                Runtime.XDecref(item);
                yield return (T)obj;
            }
        }
    }
}
