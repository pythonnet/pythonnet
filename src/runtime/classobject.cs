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
        internal ConstructorInfo[] ctors;

        internal ClassObject(Type tp) : base(tp)
        {
            ctors = type.GetConstructors();
            binder = new ConstructorBinder(type);

            foreach (ConstructorInfo t in ctors)
            {
                binder.AddMethod(t);
            }
        }


        /// <summary>
        /// Helper to get docstring from reflected constructor info.
        /// </summary>
        internal IntPtr GetDocString()
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
            return Runtime.PyString_FromString(str);
        }


        /// <summary>
        /// Implements __new__ for reflected classes and value types.
        /// </summary>
        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            var self = GetManagedObject(tp) as ClassObject;

            // Sanity check: this ensures a graceful error if someone does
            // something intentially wrong like use the managed metatype for
            // a class that is not really derived from a managed class.
            if (self == null)
            {
                return Exceptions.RaiseTypeError("invalid object");
            }

            Type type = self.type;

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

                return CLRObject.GetInstHandle(result, tp);
            }

            if (type.IsAbstract)
            {
                Exceptions.SetError(Exceptions.TypeError, "cannot instantiate abstract class");
                return IntPtr.Zero;
            }

            if (type.IsEnum)
            {
                Exceptions.SetError(Exceptions.TypeError, "cannot instantiate enumeration");
                return IntPtr.Zero;
            }

            object obj = self.binder.InvokeRaw(IntPtr.Zero, args, kw);
            if (obj == null)
            {
                return IntPtr.Zero;
            }

            return CLRObject.GetInstHandle(obj, tp);
        }


        /// <summary>
        /// Implementation of [] semantics for reflected types. This exists
        /// both to implement the Array[int] syntax for creating arrays and
        /// to support generic name overload resolution using [].
        /// </summary>
        public override IntPtr type_subscript(IntPtr idx)
        {
            // If this type is the Array type, the [<type>] means we need to
            // construct and return an array type of the given element type.
            if (type == typeof(Array))
            {
                if (Runtime.PyTuple_Check(idx))
                {
                    return Exceptions.RaiseTypeError("type expected");
                }
                var c = GetManagedObject(idx) as ClassBase;
                Type t = c != null ? c.type : Converter.GetTypeByAlias(idx);
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

            Type gtype = AssemblyManager.LookupType($"{type.FullName}`{types.Length}");
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
