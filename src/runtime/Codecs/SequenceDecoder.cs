using System;
using System.Collections.Generic;

namespace Python.Runtime.Codecs
{
    class SequenceDecoder : IPyObjectDecoder
    {
        internal static bool IsSequence(Type targetType)
        {
            if (!targetType.IsGenericType)
                return false;

            return targetType.GetGenericTypeDefinition() == typeof(ICollection<>);
        }

        internal static bool IsSequence(PyObject objectType)
        {
            //must implement iterable protocol to fully implement sequence protocol
            if (!IterableDecoder.IsIterable(objectType)) return false;

            //returns wheter it implements the sequence protocol
            return Runtime.PySequence_Check(objectType.Handle);
        }

        public bool CanDecode(PyObject objectType, Type targetType)
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
