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

        private Tuple<CollectionRank, Type> GetRankAndType(Type collectionType)
        {
            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (collectionType == typeof(System.Collections.IEnumerable))
                return new Tuple<CollectionRank, Type>(CollectionRank.Iterable, typeof(object));

            Func<Type, CollectionRank> getRankOfType = (Type type) => {
                if (type.GetGenericTypeDefinition() == typeof(IList<>))
                    return CollectionRank.List;
                if (type.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return CollectionRank.Sequence;
                if (type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return CollectionRank.Iterable;
                return CollectionRank.None;
            };

            if (collectionType.IsGenericType)
            {
                //for compatibility we *could* do this and copy the value but probably not the best option.
                /*if (collectionType.GetGenericTypeDefinition() == typeof(List<>))
                    return new Tuple<CollectionRank, Type>(CollectionRank.List, elementType);*/

                var elementType = collectionType.GetGenericArguments()[0];
                var thisRank = getRankOfType(collectionType);
                if (thisRank != CollectionRank.None)
                    return new Tuple<CollectionRank, Type>(thisRank, elementType);
            }

            return null;
        }

        private CollectionRank? GetRank(Type targetType)
        {
            return GetRankAndType(targetType)?.Item1;
        }

        public bool CanDecode(PyObject objectType, Type targetType)
        {
            //get the python object rank
            var pyRank = GetRank(objectType);
            if (pyRank == CollectionRank.None)
                return false;

            //get the clr object rank
            var clrRank = GetRank(targetType);
            if (clrRank == null || clrRank == CollectionRank.None)
                return false;

            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (targetType == typeof(System.Collections.IEnumerable))
                return true;

            return (int)pyRank >= (int)clrRank;
        }

        private class GenericPyEnumerable<T> : IEnumerable<T>
        {
            protected PyObject iterObject;

            internal GenericPyEnumerable(PyObject pyObj)
            {
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
                        break;
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
                        break;
                    }

                    Runtime.XDecref(item);
                    yield return (T)obj;
                }
            }
        }

        private object ToPlainEnumerable(PyObject pyObj)
        {
            return new GenericPyEnumerable<object>(pyObj);
        }
        private object ToEnumerable<T>(PyObject pyObj)
        {
            return new GenericPyEnumerable<T>(pyObj);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            object var = null;
            //first see if T is a plan IEnumerable
            if (typeof(T) == typeof(System.Collections.IEnumerable))
            {
                var = new GenericPyEnumerable<object>(pyObj);
            }

            //next use the rank to return the appropriate type
            var clrRank = GetRank(typeof(T));
            if (clrRank == CollectionRank.Iterable)
                var = new GenericPyEnumerable<int>(pyObj);
            else
            {
                //var = null;
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
