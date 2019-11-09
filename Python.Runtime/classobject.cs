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


        /// <summary>
        /// Implements __getitem__ for reflected classes and value types.
        /// </summary>
        public static IntPtr mp_subscript(IntPtr ob, IntPtr idx)
        {
            //ManagedType self = GetManagedObject(ob);
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp);

            if (cls.indexer == null || !cls.indexer.CanGet)
            {
                Exceptions.SetError(Exceptions.TypeError, "unindexable object");
                return IntPtr.Zero;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            IntPtr args = idx;
            var free = false;

            if (!Runtime.PyTuple_Check(idx))
            {
                args = Runtime.PyTuple_New(1);
                Runtime.XIncref(idx);
                Runtime.PyTuple_SetItem(args, 0, idx);
                free = true;
            }

            IntPtr value;

            try
            {
                value = cls.indexer.GetItem(ob, args);
            }
            finally
            {
                if (free)
                {
                    Runtime.XDecref(args);
                }
            }
            return value;
        }


        /// <summary>
        /// Implements __setitem__ for reflected classes and value types.
        /// </summary>
        public static int mp_ass_subscript(IntPtr ob, IntPtr idx, IntPtr v)
        {
            //ManagedType self = GetManagedObject(ob);
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var cls = (ClassBase)GetManagedObject(tp);

            if (cls.indexer == null || !cls.indexer.CanSet)
            {
                Exceptions.SetError(Exceptions.TypeError, "object doesn't support item assignment");
                return -1;
            }

            // Arg may be a tuple in the case of an indexer with multiple
            // parameters. If so, use it directly, else make a new tuple
            // with the index arg (method binders expect arg tuples).
            IntPtr args = idx;
            var free = false;

            if (!Runtime.PyTuple_Check(idx))
            {
                args = Runtime.PyTuple_New(1);
                Runtime.XIncref(idx);
                Runtime.PyTuple_SetItem(args, 0, idx);
                free = true;
            }

            // Get the args passed in.
            var i = Runtime.PyTuple_Size(args);
            IntPtr defaultArgs = cls.indexer.GetDefaultArgs(args);
            var numOfDefaultArgs = Runtime.PyTuple_Size(defaultArgs);
            var temp = i + numOfDefaultArgs;
            IntPtr real = Runtime.PyTuple_New(temp + 1);
            for (var n = 0; n < i; n++)
            {
                IntPtr item = Runtime.PyTuple_GetItem(args, n);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(real, n, item);
            }

            // Add Default Args if needed
            for (var n = 0; n < numOfDefaultArgs; n++)
            {
                IntPtr item = Runtime.PyTuple_GetItem(defaultArgs, n);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(real, n + i, item);
            }
            // no longer need defaultArgs
            Runtime.XDecref(defaultArgs);
            i = temp;

            // Add value to argument list
            Runtime.XIncref(v);
            Runtime.PyTuple_SetItem(real, i, v);

            try
            {
                cls.indexer.SetItem(ob, real);
            }
            finally
            {
                Runtime.XDecref(real);

                if (free)
                {
                    Runtime.XDecref(args);
                }
            }

            if (Exceptions.ErrorOccurred())
            {
                return -1;
            }

            return 0;
        }


        /// <summary>
        /// This is a hack. Generally, no managed class is considered callable
        /// from Python - with the exception of System.Delegate. It is useful
        /// to be able to call a System.Delegate instance directly, especially
        /// when working with multicast delegates.
        /// </summary>
        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw)
        {
            //ManagedType self = GetManagedObject(ob);
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var cb = (ClassBase)GetManagedObject(tp);

            if (cb.type != typeof(Delegate))
            {
                Exceptions.SetError(Exceptions.TypeError, "object is not callable");
                return IntPtr.Zero;
            }

            var co = (CLRObject)GetManagedObject(ob);
            var d = co.inst as Delegate;
            BindingFlags flags = BindingFlags.Public |
                                 BindingFlags.NonPublic |
                                 BindingFlags.Instance |
                                 BindingFlags.Static;

            MethodInfo method = d.GetType().GetMethod("Invoke", flags);
            var binder = new MethodBinder(method);
            return binder.Invoke(ob, args, kw);
        }
    }
}
