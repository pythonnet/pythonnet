namespace Python.Runtime.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public sealed class TupleCodec<TTuple> : IPyObjectEncoder, IPyObjectDecoder
    {
        TupleCodec() { }
        public static TupleCodec<TTuple> Instance { get; } = new TupleCodec<TTuple>();

        public bool CanEncode(Type type)
        {
            if (type == typeof(object) || type == typeof(TTuple)) return true;
            return type.Namespace == typeof(TTuple).Namespace
                   // generic versions of tuples are named Tuple`TYPE_ARG_COUNT
                   && type.Name.StartsWith(typeof(TTuple).Name + '`');
        }

        public PyObject? TryEncode(object value)
        {
            if (value == null) return null;

            var tupleType = value.GetType();
            if (tupleType == typeof(object)) return null;
            if (!this.CanEncode(tupleType)) return null;
            if (tupleType == typeof(TTuple)) return new PyTuple();

            nint fieldCount = tupleType.GetGenericArguments().Length;
            using var tuple = Runtime.PyTuple_New(fieldCount);
            PythonException.ThrowIfIsNull(tuple);
            int fieldIndex = 0;
            foreach (FieldInfo field in tupleType.GetFields())
            {
                var item = field.GetValue(value);
                using var pyItem = Converter.ToPython(item, field.FieldType);
                Runtime.PyTuple_SetItem(tuple.Borrow(), fieldIndex, pyItem.Steal());
                fieldIndex++;
            }
            return new PyTuple(tuple.Steal());
        }

        public bool CanDecode(PyType objectType, Type targetType)
            => PythonReferenceComparer.Instance.Equals(objectType, Runtime.PyTupleType)
            && this.CanEncode(targetType);

        public bool TryDecode<T>(PyObject pyObj, out T? value)
        {
            if (pyObj == null) throw new ArgumentNullException(nameof(pyObj));

            value = default;

            if (!Runtime.PyTuple_Check(pyObj)) return false;

            if (typeof(T) == typeof(object))
            {
                bool converted = Decode(pyObj, out object? result);
                if (converted)
                {
                    value = (T?)result;
                    return true;
                }

                return false;
            }

            var itemTypes = typeof(T).GetGenericArguments();
            nint itemCount = Runtime.PyTuple_Size(pyObj);
            if (itemTypes.Length != itemCount) return false;

            if (itemCount == 0)
            {
                value = (T)EmptyTuple;
                return true;
            }

            var elements = new object?[itemCount];
            for (int itemIndex = 0; itemIndex < itemTypes.Length; itemIndex++)
            {
                BorrowedReference pyItem = Runtime.PyTuple_GetItem(pyObj, itemIndex);
                if (!Converter.ToManaged(pyItem, itemTypes[itemIndex], out elements[itemIndex], setError: false))
                {
                    Exceptions.Clear();
                    return false;
                }
            }
            var factory = tupleCreate[itemCount].MakeGenericMethod(itemTypes);
            value = (T)factory.Invoke(null, elements);
            return true;
        }

        static bool Decode(PyObject tuple, out object? value)
        {
            long itemCount = Runtime.PyTuple_Size(tuple);
            if (itemCount == 0)
            {
                value = EmptyTuple;
                return true;
            }
            var elements = new object?[itemCount];
            var itemTypes = new Type[itemCount];
            value = null;
            for (int itemIndex = 0; itemIndex < elements.Length; itemIndex++)
            {
                var pyItem = Runtime.PyTuple_GetItem(tuple, itemIndex);
                if (!Converter.ToManaged(pyItem, typeof(object), out elements[itemIndex], setError: false))
                {
                    Exceptions.Clear();
                    return false;
                }

                itemTypes[itemIndex] = elements[itemIndex]?.GetType() ?? typeof(object);
            }

            var factory = tupleCreate[itemCount].MakeGenericMethod(itemTypes);
            value = factory.Invoke(null, elements);
            return true;
        }

        static readonly MethodInfo[] tupleCreate =
            typeof(TTuple).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(Tuple.Create))
                .OrderBy(m => m.GetParameters().Length)
                .ToArray();

        static readonly object EmptyTuple = tupleCreate[0].Invoke(null, parameters: new object[0]);

        public static void Register()
        {
            PyObjectConversions.RegisterEncoder(Instance);
            PyObjectConversions.RegisterDecoder(Instance);
        }
    }
}
