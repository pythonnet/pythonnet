using System;

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected delegate
    /// types. Delegates are represented in Python by generated type objects.
    /// Each of those type objects is associated an instance of this class,
    /// which provides its implementation.
    /// </summary>
    [Serializable]
    internal class DelegateObject : ClassBase
    {
        private readonly MethodBinder binder;

        internal DelegateObject(Type tp) : base(tp)
        {
            binder = new MethodBinder(tp.GetMethod("Invoke"));
        }


        /// <summary>
        /// Given a PyObject pointer to an instance of a delegate type, return
        /// the true managed delegate the Python object represents (or null).
        /// </summary>
        private static Delegate? GetTrueDelegate(BorrowedReference op)
        {
            if (GetManagedObject(op) is CLRObject o)
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
        public static NewReference tp_new(BorrowedReference tp, BorrowedReference args, BorrowedReference kw)
        {
            var self = (DelegateObject)GetManagedObject(tp)!;

            if (!self.type.Valid)
            {
                return Exceptions.RaiseTypeError(self.type.DeletedMessage);
            }
            Type type = self.type.Value;

            if (Runtime.PyTuple_Size(args) != 1)
            {
                return Exceptions.RaiseTypeError("class takes exactly one argument");
            }

            BorrowedReference method = Runtime.PyTuple_GetItem(args, 0);

            if (Runtime.PyCallable_Check(method) != 1)
            {
                return Exceptions.RaiseTypeError("argument must be callable");
            }

            Delegate d = PythonEngine.DelegateManager.GetDelegate(type, new PyObject(method));
            return CLRObject.GetReference(d, ClassManager.GetClass(type));
        }


        /// <summary>
        /// Implements __call__ for reflected delegate types.
        /// </summary>
        public static NewReference tp_call(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            // TODO: add fast type check!
            BorrowedReference pytype = Runtime.PyObject_TYPE(ob);
            var self = (DelegateObject)GetManagedObject(pytype)!;

            if (GetManagedObject(ob) is CLRObject o && o.inst is Delegate _)
            {
                return self.binder.Invoke(ob, args, kw);
            }
            return Exceptions.RaiseTypeError("invalid argument");
        }


        /// <summary>
        /// Implements __cmp__ for reflected delegate types.
        /// </summary>
        public new static NewReference tp_richcompare(BorrowedReference ob, BorrowedReference other, int op)
        {
            if (op != Runtime.Py_EQ && op != Runtime.Py_NE)
            {
                return new NewReference(Runtime.PyNotImplemented);
            }

            BorrowedReference pytrue = Runtime.PyTrue;
            BorrowedReference pyfalse = Runtime.PyFalse;

            // swap true and false for NE
            if (op != Runtime.Py_EQ)
            {
                pytrue = Runtime.PyFalse;
                pyfalse = Runtime.PyTrue;
            }

            Delegate? d1 = GetTrueDelegate(ob);
            Delegate? d2 = GetTrueDelegate(other);

            return new NewReference(d1 == d2 ? pytrue : pyfalse);
        }
    }
}
