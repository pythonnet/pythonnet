using System.Linq;
using System;
using System.Reflection;

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
        internal ConstructorBinder binder;
        internal int NumCtors = 0;

        internal ClassObject(Type tp) : base(tp)
        {
            var _ctors = type.Value.GetConstructors();
            NumCtors = _ctors.Length;
            binder = new ConstructorBinder(type.Value);
            foreach (ConstructorInfo t in _ctors)
            {
                binder.AddMethod(t);
            }
        }


        /// <summary>
        /// Helper to get docstring from reflected constructor info.
        /// </summary>
        internal NewReference GetDocString()
        {
            MethodBase[] methods = binder.GetMethods();
            var str = "";
            foreach (MethodBase t in methods)
            {
                if (str.Length > 0)
                {
                    str += Environment.NewLine;
                }
                str += t.ToString();
            }
            return NewReference.DangerousFromPointer(Runtime.PyString_FromString(str));
        }


        /// <summary>
        /// Implements __new__ for reflected classes and value types.
        /// </summary>
        public static IntPtr tp_new(IntPtr tpRaw, IntPtr args, IntPtr kw)
        {
            var tp = new BorrowedReference(tpRaw);
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
                    return IntPtr.Zero;
                }

                IntPtr op = Runtime.PyTuple_GetItem(args, 0);
                object result;

                if (!Converter.ToManaged(op, type, out result, true))
                {
                    return IntPtr.Zero;
                }

                return CLRObject.GetInstHandle(result, tp).DangerousMoveToPointerOrNull();
            }

            if (type.IsAbstract)
            {
                Exceptions.SetError(Exceptions.TypeError, "cannot instantiate abstract class");
                return IntPtr.Zero;
            }

            if (type.IsEnum)
            {
                return NewEnum(type, new BorrowedReference(args), tp).DangerousMoveToPointerOrNull();
            }

            object obj = self.binder.InvokeRaw(IntPtr.Zero, args, kw);
            if (obj == null)
            {
                return IntPtr.Zero;
            }

            return CLRObject.GetInstHandle(obj, tp).DangerousMoveToPointerOrNull();
        }

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
            if (!Converter.ToManaged(op, type.GetEnumUnderlyingType(), out object result, true))
            {
                return default;
            }

            if (!allowUnchecked && !Enum.IsDefined(type, result) && !type.IsFlagsEnum())
            {
                Exceptions.SetError(Exceptions.ValueError, "Invalid enumeration value. Pass True as the second argument if unchecked conversion is desired");
                return default;
            }

            object enumValue = Enum.ToObject(type, result);
            return CLRObject.GetInstHandle(enumValue, tp);
        }


        /// <summary>
        /// Implementation of [] semantics for reflected types. This exists
        /// both to implement the Array[int] syntax for creating arrays and
        /// to support generic name overload resolution using [].
        /// </summary>
        public override IntPtr type_subscript(IntPtr idx)
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
                Type t = c != null ? c.type.Value : Converter.GetTypeByAlias(idx);
                if (t == null)
                {
                    return Exceptions.RaiseTypeError("type expected");
                }
                Type a = t.MakeArrayType();
                ClassBase o = ClassManager.GetClass(a);
                Runtime.XIncref(o.pyHandle);
                return o.pyHandle;
            }

            // If there are generics in our namespace with the same base name
            // as the current type, then [<type>] means the caller wants to
            // bind the generic type matching the given type parameters.
            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            Type gtype = AssemblyManager.LookupTypes($"{type.Value.FullName}`{types.Length}").FirstOrDefault();
            if (gtype != null)
            {
                var g = ClassManager.GetClass(gtype) as GenericType;
                return g.type_subscript(idx);
                //Runtime.XIncref(g.pyHandle);
                //return g.pyHandle;
            }
            return Exceptions.RaiseTypeError("unsubscriptable object");
        }
    }
}
