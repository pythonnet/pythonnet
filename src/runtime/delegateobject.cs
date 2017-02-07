using System;

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected delegate
    /// types. Delegates are represented in Python by generated type objects.
    /// Each of those type objects is associated an instance of this class,
    /// which provides its implementation.
    /// </summary>
    internal class DelegateObject : ClassBase
    {
        private MethodBinder binder;

        internal DelegateObject(Type tp) : base(tp)
        {
            binder = new MethodBinder(tp.GetMethod("Invoke"));
        }


        /// <summary>
        /// Given a PyObject pointer to an instance of a delegate type, return
        /// the true managed delegate the Python object represents (or null).
        /// </summary>
        private static Delegate GetTrueDelegate(IntPtr op)
        {
            var o = GetManagedObject(op) as CLRObject;
            if (o != null)
            {
                var d = o.inst as Delegate;
                return d;
            }
            return null;
        }


        internal override bool CanSubclass()
        {
            return false;
        }


        /// <summary>
        /// DelegateObject __new__ implementation. The result of this is a new
        /// PyObject whose type is DelegateObject and whose ob_data is a handle
        /// to an actual delegate instance. The method wrapped by the actual
        /// delegate instance belongs to an object generated to relay the call
        /// to the Python callable passed in.
        /// </summary>
        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            var self = (DelegateObject)GetManagedObject(tp);

            if (Runtime.PyTuple_Size(args) != 1)
            {
                return Exceptions.RaiseTypeError("class takes exactly one argument");
            }

            IntPtr method = Runtime.PyTuple_GetItem(args, 0);

            if (Runtime.PyCallable_Check(method) != 1)
            {
                return Exceptions.RaiseTypeError("argument must be callable");
            }

            Delegate d = PythonEngine.DelegateManager.GetDelegate(self.type, method);
            return CLRObject.GetInstHandle(d, self.pyHandle);
        }


        /// <summary>
        /// Implements __call__ for reflected delegate types.
        /// </summary>
        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw)
        {
            // TODO: add fast type check!
            IntPtr pytype = Runtime.PyObject_TYPE(ob);
            var self = (DelegateObject)GetManagedObject(pytype);
            var o = GetManagedObject(ob) as CLRObject;

            if (o == null)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            var d = o.inst as Delegate;

            if (d == null)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }
            return self.binder.Invoke(ob, args, kw);
        }


        /// <summary>
        /// Implements __cmp__ for reflected delegate types.
        /// </summary>
#if PYTHON3 // TODO: Doesn't PY2 implement tp_richcompare too?
        public new static IntPtr tp_richcompare(IntPtr ob, IntPtr other, int op)
        {
            if (op != Runtime.Py_EQ && op != Runtime.Py_NE)
            {
                Runtime.XIncref(Runtime.PyNotImplemented);
                return Runtime.PyNotImplemented;
            }

            IntPtr pytrue = Runtime.PyTrue;
            IntPtr pyfalse = Runtime.PyFalse;

            // swap true and false for NE
            if (op != Runtime.Py_EQ)
            {
                pytrue = Runtime.PyFalse;
                pyfalse = Runtime.PyTrue;
            }

            Delegate d1 = GetTrueDelegate(ob);
            Delegate d2 = GetTrueDelegate(other);
            if (d1 == d2)
            {
                Runtime.XIncref(pytrue);
                return pytrue;
            }

            Runtime.XIncref(pyfalse);
            return pyfalse;
        }
#elif PYTHON2
        public static int tp_compare(IntPtr ob, IntPtr other)
        {
            Delegate d1 = GetTrueDelegate(ob);
            Delegate d2 = GetTrueDelegate(other);
            return d1 == d2 ? 0 : -1;
        }
#endif
    }
}
