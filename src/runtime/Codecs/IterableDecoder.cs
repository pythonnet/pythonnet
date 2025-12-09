using System;
using System.Collections.Generic;

namespace Python.Runtime.Codecs
{
    public class IterableDecoder : IPyObjectDecoder
    {
        internal static bool IsIterable(Type targetType)
        {
            //if it is a plain IEnumerable, we can decode it using sequence protocol.
            if (targetType == typeof(System.Collections.IEnumerable))
                return true;

            if (!targetType.IsGenericType)
                return false;

            return targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
        }

        internal static bool IsIterable(PyType objectType)
        {
            return objectType.HasAttr("__iter__");
        }

        public bool CanDecode(PyType objectType, Type targetType)
        {
            return IsIterable(objectType) && IsIterable(targetType);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            //first see if T is a plan IEnumerable
            if (typeof(T) == typeof(System.Collections.IEnumerable))
            {
                object enumerable = new CollectionWrappers.IterableWrapper<object>(pyObj);
                value = (T)enumerable;
                return true;
            }

            var elementType = typeof(T).GetGenericArguments()[0];
            var collectionType = typeof(CollectionWrappers.IterableWrapper<>).MakeGenericType(elementType);

            var instance = Activator.CreateInstance(collectionType, new[] { pyObj });
            value = (T)instance;
            return true;
        }

        public static IterableDecoder Instance { get; } = new IterableDecoder();

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
