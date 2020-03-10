using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime.Codecs
{
    class ListCodec : IPyObjectDecoder
    {
        public bool CanDecode(PyObject objectType, Type targetType)
        {
            //first check if the PyObject is iterable.
            IntPtr IterObject = Runtime.PyObject_GetIter(objectType.Handle);
            if (IterObject == IntPtr.Zero)
                return false;

            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (targetType == typeof(System.Collections.IEnumerable))
                return true;

            //if its not a plain IEnumerable it must be a generic type
            if (!targetType.IsGenericType) return false;

            Predicate<Type> IsCLRSequence = (Type type) => {
                return (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                        type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                        type.GetGenericTypeDefinition() == typeof(IList<>));
            };

            if (IsCLRSequence(targetType))
                return true;

            //if it implements any of the standard C# collection interfaces, we can decode it.
            foreach (Type itf in targetType.GetInterfaces())
            {
                if (IsCLRSequence(itf))
                    return true;
            }

            //TODO objectType should implement the Sequence protocol to be convertible to ICollection
            //     and the list protocol to be convertible to IList.  We should check for list first,
            //     then collection, then enumerable


            //if we get here we cannot decode it.
            return false;
        }

        private class PyEnumerable : System.Collections.IEnumerable
        {
            PyObject iterObject;
            internal PyEnumerable(PyObject pyObj)
            {
                iterObject = new PyObject(Runtime.PyObject_GetIter(pyObj.Handle));
            }

            public IEnumerator GetEnumerator()
            {
                IntPtr item;
                while ((item = Runtime.PyIter_Next(iterObject.Handle)) != IntPtr.Zero)
                {
                    object obj = null;
                    if (!Converter.ToManaged(item, typeof(object), out obj, true))
                    {
                        Runtime.XDecref(item);
                        break;
                    }

                    Runtime.XDecref(item);
                    yield return obj;
                }
            }
        }

        private object ToPlainEnumerable(PyObject pyObj)
        {
            return new PyEnumerable(pyObj);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            object var = null;
            //first see if T is a plan IEnumerable
            if (typeof(T) == typeof(System.Collections.IEnumerable))
            {
                var = ToPlainEnumerable(pyObj);
            }
            
            value = (T)var;
            return false;
        }

        public static ListCodec Instance { get; } = new ListCodec();

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
