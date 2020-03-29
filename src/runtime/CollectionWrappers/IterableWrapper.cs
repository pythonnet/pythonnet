using System;
using System.Collections.Generic;
using System.Collections;

namespace Python.Runtime.CollectionWrappers
{
    internal class IterableWrapper<T> : IEnumerable<T>
    {
        protected PyObject pyObject;

        public IterableWrapper(PyObject pyObj)
        {
            pyObject = pyObj;
        }

        private void propagateIterationException()
        {
            var err = Runtime.PyErr_Occurred();
            if (err != null && err != Exceptions.StopIteration)
            {
                Runtime.CheckExceptionOccurred();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (pyObject == null) yield break;
            PyObject iterObject = new PyObject(Runtime.PyObject_GetIter(pyObject.Handle));
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

            propagateIterationException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (pyObject == null) yield break;
            PyObject iterObject = new PyObject(Runtime.PyObject_GetIter(pyObject.Handle));
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

            propagateIterationException();
        }
    }
}
