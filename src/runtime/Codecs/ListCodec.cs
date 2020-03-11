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
            //TODO - convert pyTuple to IReadOnlyList

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

        private class PyEnumerable<T> : IEnumerable<T>
        {
            protected PyObject iterObject;
            protected PyObject pyObject;

            public PyEnumerable(PyObject pyObj)
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
                        Exceptions.Clear();
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
                        Exceptions.Clear();
                        Runtime.XDecref(item);
                        break;
                    }

                    Runtime.XDecref(item);
                    yield return (T)obj;
                }
            }
        }

        private class PyCollection<T> : PyEnumerable<T>, ICollection<T>
        {
            public PyCollection(PyObject pyObj) : base(pyObj)
            {

            }

            public int Count
            {
                get
                {
                    return (int)Runtime.PySequence_Size(pyObject.Handle);
                }
            }

            public virtual bool IsReadOnly => false;

            public virtual void Add(T item)
            {
                //not implemented for Python sequence rank
                throw new NotImplementedException();
            }

            public void Clear()
            {
                if (IsReadOnly)
                    throw new NotImplementedException();
                var result = Runtime.PySequence_DelSlice(pyObject.Handle, 0, Count);
                if (result == -1)
                    throw new Exception("failed to clear sequence");
            }

            public bool Contains(T item)
            {
                //not sure if IEquatable is implemented and this will work!
                foreach (var element in this)
                    if (element.Equals(item)) return true;

                return false;
            }

            protected T getItem(int index)
            {
                IntPtr item = Runtime.PySequence_GetItem(pyObject.Handle, index);
                object obj;

                if (!Converter.ToManaged(item, typeof(T), out obj, true))
                {
                    Exceptions.Clear();
                    Runtime.XDecref(item);
                    Exceptions.RaiseTypeError("wrong type in sequence");
                }

                return (T)obj;
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                for (int index = 0; index < Count; index++)
                {
                    array[index + arrayIndex] = getItem(index);
                }
            }

            protected bool removeAt(int index)
            {
                if (IsReadOnly)
                    throw new NotImplementedException();
                if (index >= Count || index < 0)
                    throw new IndexOutOfRangeException();

                return Runtime.PySequence_DelItem(pyObject.Handle, index) != 0;
            }

            protected int indexOf(T item)
            {
                var index = 0;
                foreach (var element in this)
                {
                    if (element.Equals(item)) return index;
                    index++;
                }

                return -1;
            }

            public bool Remove(T item)
            {
                return removeAt(indexOf(item));
            }
        }

        private class PyList<T> : PyCollection<T>, IList<T>
        {
            public PyList(PyObject pyObj) : base(pyObj)
            {

            }

            public T this[int index]
            {
                get
                {
                    IntPtr item = Runtime.PySequence_GetItem(pyObject.Handle, index);
                    object obj;

                    if (!Converter.ToManaged(item, typeof(T), out obj, true))
                    {
                        Exceptions.Clear();
                        Runtime.XDecref(item);
                        Exceptions.RaiseTypeError("wrong type in sequence");
                    }

                    return (T)obj;
                }
                set
                {
                    IntPtr pyItem = Converter.ToPython(value, typeof(T));
                    if (pyItem == IntPtr.Zero)
                        throw new Exception("failed to set item");

                    var result = Runtime.PySequence_SetItem(pyObject.Handle, index, pyItem);
                    Runtime.XDecref(pyItem);
                    if (result == -1)
                        throw new Exception("failed to set item");
                }
            }

            public int IndexOf(T item)
            {
                return indexOf(item);
            }

            public void Insert(int index, T item)
            {
                if (IsReadOnly)
                    throw new NotImplementedException();

                IntPtr pyItem = Converter.ToPython(item, typeof(T));
                if (pyItem == IntPtr.Zero)
                    throw new Exception("failed to insert item");

                var result = Runtime.PyList_Insert(pyObject.Handle, index, pyItem);
                Runtime.XDecref(pyItem);
                if (result == -1)
                    throw new Exception("failed to insert item");
            }

            public void RemoveAt(int index)
            {
                removeAt(index);
            }
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            //first see if T is a plan IEnumerable
            if (typeof(T) == typeof(System.Collections.IEnumerable))
            {
                object enumerable = new PyEnumerable<object>(pyObj);
                value = (T)enumerable;
                return true;
            }

            //next use the rank to return the appropriate type
            var rankAndType = GetRankAndType(typeof(T));
            if (rankAndType.Item1 == CollectionRank.None)
                throw new Exception("expected collection rank");


            var itemType = rankAndType.Item2;
            Type collectionType = null;
            if (rankAndType.Item1 == CollectionRank.Iterable)
            {
                collectionType = typeof(PyEnumerable<>).MakeGenericType(itemType);
            }
            else if (rankAndType.Item1 == CollectionRank.Sequence)
            {
                collectionType = typeof(PyCollection<>).MakeGenericType(itemType);
            }
            else if (rankAndType.Item1 == CollectionRank.List)
            {
                collectionType = typeof(PyList<>).MakeGenericType(itemType);
            }

            var instance = Activator.CreateInstance(collectionType, new[] { pyObj });
            value = (T)instance;
            return true;
        }

        public static ListCodec Instance { get; } = new ListCodec();

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
