using System;
using System.Linq;

namespace Python.Runtime
{
    /// <summary>
    /// Implements reflected generic types. Note that the Python behavior
    /// is the same for both generic type definitions and constructed open
    /// generic types. Both are essentially factories for creating closed
    /// types based on the required generic type parameters.
    /// </summary>
    [Serializable]
    internal class GenericType : ClassBase
    {
        internal GenericType(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __new__ for reflected generic types.
        /// </summary>
        public static NewReference tp_new(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            var self = (GenericType)GetManagedObject(tp)!;
            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            var type = self.type.Value;

            if (type.IsInterface && !type.IsConstructedGenericType)
            {
                var nargs = Runtime.PyTuple_Size(args);
                if (nargs == 1)
                {
                    var instance = Runtime.PyTuple_GetItem(args, 0);
                    return AsGenericInterface(instance, type);
                }
            }

            Exceptions.SetError(Exceptions.TypeError, "cannot instantiate an open generic type");

            return default;
        }

        static NewReference AsGenericInterface(BorrowedReference instance, Type targetType)
        {
            if (GetManagedObject(instance) is not CLRObject obj)
            {
                return Exceptions.RaiseTypeError("only .NET objects can be cast to .NET interfaces");
            }

            Type[] supportedInterfaces = obj.inst.GetType().GetInterfaces();
            Type[] constructedInterfaces = supportedInterfaces
                .Where(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == targetType)
                .ToArray();

            if (constructedInterfaces.Length == 1)
            {
                BorrowedReference pythonic = ClassManager.GetClass(constructedInterfaces[0]);
                using var args = Runtime.PyTuple_New(1);
                Runtime.PyTuple_SetItem(args.Borrow(), 0, instance);
                return Runtime.PyObject_CallObject(pythonic, args.Borrow());
            }

            if (constructedInterfaces.Length > 1)
            {
                string interfaces = string.Join(", ", constructedInterfaces.Select(TypeManager.GetPythonTypeName));
                return Exceptions.RaiseTypeError("Ambiguous cast to .NET interface. "
                                               + $"Object implements: {interfaces}");
            }

            return Exceptions.RaiseTypeError("object does not implement "
                                           + TypeManager.GetPythonTypeName(targetType));
        }

        /// <summary>
        /// Implements __call__ for reflected generic types.
        /// </summary>
        public static NewReference tp_call(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            Exceptions.SetError(Exceptions.TypeError, "object is not callable");
            return default;
        }
    }
}
