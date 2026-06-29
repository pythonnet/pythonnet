using System;
using System.Collections.Generic;

namespace Python.Runtime.Codecs
{
    public class SequenceDecoder : IPyObjectDecoder
    {
        internal static bool IsSequence(Type targetType)
        {
            if (!targetType.IsGenericType)
                return false;

            return targetType.GetGenericTypeDefinition() == typeof(ICollection<>);
        }

        internal static bool IsSequence(PyType objectType)
        {
            //must implement iterable protocol to fully implement sequence protocol
            if (!IterableDecoder.IsIterable(objectType)) return false;

            //returns wheter it implements the sequence protocol
            //according to python doc this needs to exclude dict subclasses
            //but I don't know how to look for that given the objectType
            //rather than the instance.
            return objectType.HasAttr("__getitem__");
        }

        public bool CanDecode(PyType objectType, Type targetType)
        {
            return IsSequence(objectType) && IsSequence(targetType);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            if (pyObj == null) throw new ArgumentNullException(nameof(pyObj));

            var elementType = typeof(T).GetGenericArguments()[0];
            Type collectionType = typeof(CollectionWrappers.SequenceWrapper<>).MakeGenericType(elementType);

            var instance = Activator.CreateInstance(collectionType, new[] { pyObj });
            value = (T)instance;
            return true;
        }

        public static SequenceDecoder Instance { get; } = new SequenceDecoder();

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
