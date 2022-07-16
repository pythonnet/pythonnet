using System;
using System.Diagnostics;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python event binding type, similar to a method binding.
    /// </summary>
    [Serializable]
    internal class EventBinding : ExtensionType
    {
        private readonly string name;
        private readonly EventHandlerCollection e;
        private readonly PyObject? target;

        public EventBinding(string name, EventHandlerCollection e, PyObject? target)
        {
            this.name = name;
            this.target = target;
            this.e = e;
        }

        public EventBinding(EventInfo @event) : this(@event.Name, new EventHandlerCollection(@event), target: null)
        {
            Debug.Assert(@event.AddMethod.IsStatic);
        }


        /// <summary>
        /// EventBinding += operator implementation.
        /// </summary>
        public static NewReference nb_inplace_add(BorrowedReference ob, BorrowedReference arg)
        {
            var self = (EventBinding)GetManagedObject(ob)!;

            if (Runtime.PyCallable_Check(arg) < 1)
            {
                Exceptions.SetError(Exceptions.TypeError, "event handlers must be callable");
                return default;
            }

            if (!self.e.AddEventHandler(self.target.BorrowNullable(), new PyObject(arg)))
            {
                return default;
            }

            return new NewReference(ob);
        }


        /// <summary>
        /// EventBinding -= operator implementation.
        /// </summary>
        public static NewReference nb_inplace_subtract(BorrowedReference ob, BorrowedReference arg)
        {
            var self = (EventBinding)GetManagedObject(ob)!;

            if (Runtime.PyCallable_Check(arg) < 1)
            {
                Exceptions.SetError(Exceptions.TypeError, "invalid event handler");
                return default;
            }

            if (!self.e.RemoveEventHandler(self.target.BorrowNullable(), arg))
            {
                return default;
            }

            return new NewReference(ob);
        }

        public static int tp_descr_set(BorrowedReference ds, BorrowedReference ob, BorrowedReference val)
            => EventObject.tp_descr_set(ds, ob, val);


        /// <summary>
        /// EventBinding  __hash__ implementation.
        /// </summary>
        public static nint tp_hash(BorrowedReference ob)
        {
            var self = (EventBinding)GetManagedObject(ob)!;
            nint x = 0;

            if (self.target != null)
            {
                x = Runtime.PyObject_Hash(self.target);
                if (x == -1)
                {
                    return x;
                }
            }

            nint y = self.e.GetHashCode();
            return x ^ y;
        }


        /// <summary>
        /// EventBinding __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (EventBinding)GetManagedObject(ob)!;
            string type = self.target == null ? "unbound" : "bound";
            string s = string.Format("<{0} event '{1}'>", type, self.name);
            return Runtime.PyString_FromString(s);
        }
    }
}
