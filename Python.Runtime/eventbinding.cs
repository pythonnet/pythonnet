using System;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python event binding type, similar to a method binding.
    /// </summary>
    internal class EventBinding : ExtensionType
    {
        private EventObject e;
        private IntPtr target;

        public EventBinding(EventObject e, IntPtr target)
        {
            Runtime.XIncref(target);
            this.target = target;
            this.e = e;
        }


        /// <summary>
        /// EventBinding += operator implementation.
        /// </summary>
        public static IntPtr nb_inplace_add(IntPtr ob, IntPtr arg)
        {
            var self = (EventBinding)GetManagedObject(ob);

            if (Runtime.PyCallable_Check(arg) < 1)
            {
                Exceptions.SetError(Exceptions.TypeError, "event handlers must be callable");
                return IntPtr.Zero;
            }

            if (!self.e.AddEventHandler(self.target, arg))
            {
                return IntPtr.Zero;
            }

            Runtime.XIncref(self.pyHandle);
            return self.pyHandle;
        }


        /// <summary>
        /// EventBinding -= operator implementation.
        /// </summary>
        public static IntPtr nb_inplace_subtract(IntPtr ob, IntPtr arg)
        {
            var self = (EventBinding)GetManagedObject(ob);

            if (Runtime.PyCallable_Check(arg) < 1)
            {
                Exceptions.SetError(Exceptions.TypeError, "invalid event handler");
                return IntPtr.Zero;
            }

            if (!self.e.RemoveEventHandler(self.target, arg))
            {
                return IntPtr.Zero;
            }

            Runtime.XIncref(self.pyHandle);
            return self.pyHandle;
        }


        /// <summary>
        /// EventBinding  __hash__ implementation.
        /// </summary>
        public static IntPtr tp_hash(IntPtr ob)
        {
            var self = (EventBinding)GetManagedObject(ob);
            long x = 0;
            long y = 0;

            if (self.target != IntPtr.Zero)
            {
                x = Runtime.PyObject_Hash(self.target).ToInt64();
                if (x == -1)
                {
                    return new IntPtr(-1);
                }
            }

            y = Runtime.PyObject_Hash(self.e.pyHandle).ToInt64();
            if (y == -1)
            {
                return new IntPtr(-1);
            }

            x ^= y;

            if (x == -1)
            {
                x = -1;
            }

            return new IntPtr(x);
        }


        /// <summary>
        /// EventBinding __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr ob)
        {
            var self = (EventBinding)GetManagedObject(ob);
            string type = self.target == IntPtr.Zero ? "unbound" : "bound";
            string s = string.Format("<{0} event '{1}'>", type, self.e.name);
            return Runtime.PyString_FromString(s);
        }


        /// <summary>
        /// EventBinding dealloc implementation.
        /// </summary>
        public new static void tp_dealloc(IntPtr ob)
        {
            var self = (EventBinding)GetManagedObject(ob);
            Runtime.XDecref(self.target);
            ExtensionType.FinalizeObject(self);
        }
    }
}
