using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python descriptor type that provides access to CLR events.
    /// </summary>
    [Serializable]
    internal class EventObject : ExtensionType
    {
        internal readonly string name;
        internal readonly EventHandlerCollection reg;

        public EventObject(EventInfo info)
        {
            Debug.Assert(!info.AddMethod.IsStatic);
            this.name = info.Name;
            this.reg = new EventHandlerCollection(info);
        }

        /// <summary>
        /// Descriptor __get__ implementation. A getattr on an event returns
        /// a "bound" event that keeps a reference to the object instance.
        /// </summary>
        public static NewReference tp_descr_get(BorrowedReference ds, BorrowedReference ob, BorrowedReference tp)
        {
            var self = GetManagedObject(ds) as EventObject;

            if (self == null)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            if (ob == null)
            {
                return new NewReference(ds);
            }

            if (Runtime.PyObject_IsInstance(ob, tp) < 1)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            return new EventBinding(self.name, self.reg, new PyObject(ob)).Alloc();
        }


        /// <summary>
        /// Descriptor __set__ implementation. This actually never allows you
        /// to set anything; it exists solely to support the '+=' spelling of
        /// event handler registration. The reason is that given code like:
        /// 'ob.SomeEvent += method', Python will attempt to set the attribute
        /// SomeEvent on ob to the result of the '+=' operation.
        /// </summary>
        public static int tp_descr_set(BorrowedReference ds, BorrowedReference ob, BorrowedReference val)
        {
            var e = GetManagedObject(val) as EventBinding;

            if (e != null)
            {
                return 0;
            }

            Exceptions.RaiseTypeError("cannot set event attributes");
            return -1;
        }


        /// <summary>
        /// Descriptor __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (EventObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<event '{self.name}'>");
        }
    }


    internal class Handler
    {
        public readonly nint hash;
        public readonly Delegate del;

        public Handler(nint hash, Delegate d)
        {
            this.hash = hash;
            this.del = d;
        }
    }
}
