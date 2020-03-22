using System;
using System.Collections.Generic;
using System.Collections;

namespace Python.Runtime.CollectionWrappers
{
    internal class IterableWrapper<T> : IEnumerable<T>
    {
        protected PyObject iterObject;
        protected PyObject pyObject;

        public IterableWrapper(PyObject pyObj)
        {
            pyObject = pyObj;
            iterObject = new PyObject(Runtime.PyObject_GetIter(pyObj.Handle));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IntPtr item;
            while ((item = Runtime.PyIter_Next(iterObject.Handle)) != IntPtr.Zero)
            {
                object obj = null;
                if (!Converter.ToManaged(item, typeof(object), out obj, true))
                {
                    Runtime.XDecref(item);
                    Runtime.CheckExceptionOccurred();
                }

                Runtime.XDecref(item);
                yield return obj;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            IntPtr item;
            while ((item = Runtime.PyIter_Next(iterObject.Handle)) != IntPtr.Zero)
            {
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
