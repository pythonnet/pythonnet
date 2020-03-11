using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.Runtime.Codecs
{
    class ListCodec : IPyObjectDecoder
    {
        private enum CollectionRank
        {
            //order matters, this is in increasing order of specialization
            None,
            Iterable,
            Sequence,
            List
        }

        private CollectionRank GetRank(PyObject objectType)
        {
            var handle = objectType.Handle;
            //first check if the PyObject is iterable.
            IntPtr IterObject = Runtime.PyObject_GetIter(handle);
            if (IterObject == IntPtr.Zero)
                return CollectionRank.None;

            //now check if its a sequence
            if (Runtime.PySequence_Check(handle))
            {
                //last check if its a list
                if (Runtime.PyList_Check(handle))
                    return CollectionRank.List;
                return CollectionRank.Sequence;
            }

            return CollectionRank.Iterable;
        }

        private CollectionRank GetRank(Type targetType)
        {
            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (targetType == typeof(System.Collections.IEnumerable))
                return CollectionRank.Iterable;

            Func<Type, CollectionRank> getRankOfType = (Type type) => {
                if (type.GetGenericTypeDefinition() == typeof(IList<>))
                    return CollectionRank.List;
                if (type.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return CollectionRank.Sequence;
                if (type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return CollectionRank.Iterable;
                return CollectionRank.None;
            };

            if (targetType.IsGenericType)
            {
                var thisRank = getRankOfType(targetType);
                if (thisRank != CollectionRank.None)
                    return thisRank;
            }

            var maxRank = CollectionRank.None;
            //if it implements any of the standard C# collection interfaces, we can decode it.
            foreach (Type itf in targetType.GetInterfaces())
            {
                if (!itf.IsGenericType) continue;

                var thisRank = getRankOfType(itf);

                //this is the most specialized type.  return early
                if (thisRank == CollectionRank.List) return thisRank;

                //if it is more specialized, assign to max rank
                if ((int)thisRank > (int)maxRank)
                    maxRank = thisRank;
            }

            return maxRank;
        }


        public bool CanDecode(PyObject objectType, Type targetType)
        {
            //get the python object rank
            var pyRank = GetRank(objectType);
            if (pyRank == CollectionRank.None)
                return false;

            //get the clr object rank
            var clrRank = GetRank(targetType);
            if (clrRank == CollectionRank.None)
                return false;

            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (targetType == typeof(System.Collections.IEnumerable))
                return true;

            return (int)pyRank >= (int)clrRank;
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
