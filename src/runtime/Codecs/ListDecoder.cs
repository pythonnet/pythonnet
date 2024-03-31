using System;
using System.Collections.Generic;

namespace Python.Runtime.Codecs
{
    public class ListDecoder : IPyObjectDecoder
    {
        private static bool IsList(Type targetType)
        {
            if (!targetType.IsGenericType)
                return false;

            return targetType.GetGenericTypeDefinition() == typeof(IList<>);
        }

        private static bool IsList(PyType objectType)
        {
            //TODO accept any python object that implements the sequence and list protocols
            //must implement sequence protocol to fully implement list protocol
            //if (!SequenceDecoder.IsSequence(objectType)) return false;

            //returns wheter the type is a list.
            return PythonReferenceComparer.Instance.Equals(objectType, Runtime.PyListType);
        }

        public bool CanDecode(PyType objectType, Type targetType)
        {
            return IsList(objectType) && IsList(targetType);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            if (pyObj == null) throw new ArgumentNullException(nameof(pyObj));

            var elementType = typeof(T).GetGenericArguments()[0];
            Type collectionType = typeof(CollectionWrappers.ListWrapper<>).MakeGenericType(elementType);

            var instance = Activator.CreateInstance(collectionType, new[] { pyObj });
            value = (T)instance;
            return true;
        }

        public static ListDecoder Instance { get; } = new ListDecoder();

        public static void Register()
        {
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
