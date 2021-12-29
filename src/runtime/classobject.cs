using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected types.
    /// Managed classes and value types are represented in Python by actual
    /// Python type objects. Each of those type objects is associated with
    /// an instance of ClassObject, which provides its implementation.
    /// </summary>
    [Serializable]
    internal class ClassObject : ClassBase
    {
        internal readonly int NumCtors = 0;

        internal ClassObject(Type tp) : base(tp)
        {
            var _ctors = type.Value.GetConstructors();
            NumCtors = _ctors.Length;
        }


        /// <summary>
        /// Helper to get docstring from reflected constructor info.
        /// </summary>
        internal NewReference GetDocString()
        {
            if (!type.Valid)
            {
                return Exceptions.RaiseTypeError(type.DeletedMessage);
            }

            MethodBase[] methods = type.Value.GetConstructors();
            var str = "";
            foreach (MethodBase t in methods)
            {
                if (str.Length > 0)
                {
                    str += Environment.NewLine;
                }
                str += t.ToString();
            }
            return Runtime.PyString_FromString(str);
        }


        /// <summary>
        /// Implements __new__ for reflected classes and value types.
        /// </summary>
        static NewReference tp_new_impl(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            var self = GetManagedObject(tp) as ClassObject;

            // Sanity check: this ensures a graceful error if someone does
            // something intentially wrong like use the managed metatype for
            // a class that is not really derived from a managed class.
            if (self == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }

            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            Type type = self.type.Value;

            // Primitive types do not have constructors, but they look like
            // they do from Python. If the ClassObject represents one of the
            // convertible primitive types, just convert the arg directly.
            if (type.IsPrimitive || type == typeof(string))
            {
                if (Runtime.PyTuple_Size(args) != 1)
                {
                    Exceptions.SetError(Exceptions.TypeError, "no constructors match given arguments");
                    return default;
                }

                BorrowedReference op = Runtime.PyTuple_GetItem(args, 0);

                if (!Converter.ToManaged(op, type, out var result, true))
                {
                    return default;
                }

                return CLRObject.GetReference(result!, tp);
            }

            if (type.IsAbstract)
            {
                Exceptions.SetError(Exceptions.TypeError, "cannot instantiate abstract class");
                return default;
            }

            if (type.IsEnum)
            {
                return NewEnum(type, args, tp);
            }

            if (IsGenericNullable(type))
            {
                // Nullable<T> has special handling in .NET runtime.
                // Invoking its constructor via reflection on an uninitialized instance
                // does not actually set the object fields.
                return NewNullable(type, args, kw, tp);
            }

            object obj = FormatterServices.GetUninitializedObject(type);

            return self.NewObjectToPython(obj, tp);
        }

        protected virtual void SetTypeNewSlot(BorrowedReference pyType, SlotsHolder slotsHolder)
        {
            TypeManager.InitializeSlotIfEmpty(pyType, TypeOffset.tp_new, new Interop.BBB_N(tp_new_impl), slotsHolder);
        }

        public override bool HasCustomNew()
        {
            if (base.HasCustomNew()) return true;

            Type clrType = type.Value;
            return clrType.IsPrimitive
                || clrType.IsEnum
                || clrType == typeof(string)
                || IsGenericNullable(clrType);
        }

        static bool IsGenericNullable(Type type)
            => type.IsValueType && type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        public override void InitializeSlots(BorrowedReference pyType, SlotsHolder slotsHolder)
        {
            base.InitializeSlots(pyType, slotsHolder);

            this.SetTypeNewSlot(pyType, slotsHolder);
        }

        protected virtual NewReference NewObjectToPython(object obj, BorrowedReference tp)
            => CLRObject.GetReference(obj, tp);

        private static NewReference NewEnum(Type type, BorrowedReference args, BorrowedReference tp)
        {
            nint argCount = Runtime.PyTuple_Size(args);
            bool allowUnchecked = false;
            if (argCount == 2)
            {
                var allow = Runtime.PyTuple_GetItem(args, 1);
                if (!Converter.ToManaged(allow, typeof(bool), out var allowObj, true) || allowObj is null)
                {
                    Exceptions.RaiseTypeError("second argument to enum constructor must be a boolean");
                    return default;
                }
                allowUnchecked |= (bool)allowObj;
            }

            if (argCount < 1 || argCount > 2)
            {
                Exceptions.SetError(Exceptions.TypeError, "no constructors match given arguments");
                return default;
            }

            var op = Runtime.PyTuple_GetItem(args, 0);
            if (!Converter.ToManaged(op, type.GetEnumUnderlyingType(), out object? result, true))
            {
                return default;
            }

            if (!allowUnchecked && !Enum.IsDefined(type, result) && !type.IsFlagsEnum())
            {
                Exceptions.SetError(Exceptions.ValueError, "Invalid enumeration value. Pass True as the second argument if unchecked conversion is desired");
                return default;
            }

            object enumValue = Enum.ToObject(type, result);
            return CLRObject.GetReference(enumValue, tp);
        }

        private static NewReference NewNullable(Type type, BorrowedReference args, BorrowedReference kw, BorrowedReference tp)
        {
            Debug.Assert(IsGenericNullable(type));

            if (kw != null)
            {
                return Exceptions.RaiseTypeError("System.Nullable<T> constructor does not support keyword arguments");
            }

            nint argsCount = Runtime.PyTuple_Size(args);
            if (argsCount != 1)
            {
                return Exceptions.RaiseTypeError("System.Nullable<T> constructor expects 1 argument, got " + (int)argsCount);
            }

            var value = Runtime.PyTuple_GetItem(args, 0);
            var elementType = type.GetGenericArguments()[0];
            return Converter.ToManaged(value, elementType, out var result, setError: true)
                ? CLRObject.GetReference(result!, tp)
                : default;
        }


        /// <summary>
        /// Implementation of [] semantics for reflected types. This exists
        /// both to implement the Array[int] syntax for creating arrays and
        /// to support generic name overload resolution using [].
        /// </summary>
        public override NewReference type_subscript(BorrowedReference idx)
        {
            if (!type.Valid)
            {
                return Exceptions.RaiseTypeError(type.DeletedMessage);
            }

            // If this type is the Array type, the [<type>] means we need to
            // construct and return an array type of the given element type.
            if (type.Value == typeof(Array))
            {
                if (Runtime.PyTuple_Check(idx))
                {
                    return Exceptions.RaiseTypeError("type expected");
                }
                var c = GetManagedObject(idx) as ClassBase;
                Type? t = c != null ? c.type.Value : Converter.GetTypeByAlias(idx);
                if (t == null)
                {
                    return Exceptions.RaiseTypeError("type expected");
                }
                Type a = t.MakeArrayType();
                PyType o = ClassManager.GetClass(a);
                return new NewReference(o);
            }

            // If there are generics in our namespace with the same base name
            // as the current type, then [<type>] means the caller wants to
            // bind the generic type matching the given type parameters.
            Type[]? types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            Type gtype = AssemblyManager.LookupTypes($"{type.Value.FullName}`{types.Length}").FirstOrDefault();
            if (gtype != null)
            {
                var g = (GenericType)ClassManager.GetClassImpl(gtype);
                return g.type_subscript(idx);
            }
            return Exceptions.RaiseTypeError("unsubscriptable object");
        }
    }
}
