using System;
using System.Collections;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python descriptor type that provides access to CLR events.
    /// </summary>
    internal class EventObject : ExtensionType
    {
        internal string name;
        internal EventBinding unbound;
        internal EventInfo info;
        internal Hashtable reg;

        public EventObject(EventInfo info)
        {
            this.name = info.Name;
            this.info = info;
        }


        /// <summary>
        /// Register a new Python object event handler with the event.
        /// </summary>
        internal bool AddEventHandler(IntPtr target, IntPtr handler)
        {
            object obj = null;
            if (target != IntPtr.Zero)
            {
                var co = (CLRObject)GetManagedObject(target);
                obj = co.inst;
            }

            // Create a true delegate instance of the appropriate type to
            // wrap the Python handler. Note that wrapper delegate creation
            // always succeeds, though calling the wrapper may fail.
            Type type = info.EventHandlerType;
            Delegate d = PythonEngine.DelegateManager.GetDelegate(type, handler);

            // Now register the handler in a mapping from instance to pairs
            // of (handler hash, delegate) so we can lookup to remove later.
            // All this is done lazily to avoid overhead until an event is
            // actually subscribed to by a Python event handler.
            if (reg == null)
            {
                reg = new Hashtable();
            }
            object key = obj ?? info.ReflectedType;
            var list = reg[key] as ArrayList;
            if (list == null)
            {
                list = new ArrayList();
                reg[key] = list;
            }
            list.Add(new Handler(Runtime.PyObject_Hash(handler), d));

            // Note that AddEventHandler helper only works for public events,
            // so we have to get the underlying add method explicitly.
            object[] args = { d };
            MethodInfo mi = info.GetAddMethod(true);
            mi.Invoke(obj, BindingFlags.Default, null, args, null);

            return true;
        }


        /// <summary>
        /// Remove the given Python object event handler.
        /// </summary>
        internal bool RemoveEventHandler(IntPtr target, IntPtr handler)
        {
            object obj = null;
            if (target != IntPtr.Zero)
            {
                var co = (CLRObject)GetManagedObject(target);
                obj = co.inst;
            }

            IntPtr hash = Runtime.PyObject_Hash(handler);
            if (Exceptions.ErrorOccurred() || reg == null)
            {
                Exceptions.SetError(Exceptions.ValueError, "unknown event handler");
                return false;
            }

            object key = obj ?? info.ReflectedType;
            var list = reg[key] as ArrayList;

            if (list == null)
            {
                Exceptions.SetError(Exceptions.ValueError, "unknown event handler");
                return false;
            }

            object[] args = { null };
            MethodInfo mi = info.GetRemoveMethod(true);

            for (var i = 0; i < list.Count; i++)
            {
                var item = (Handler)list[i];
                if (item.hash != hash)
                {
                    continue;
                }
                args[0] = item.del;
                try
                {
                    mi.Invoke(obj, BindingFlags.Default, null, args, null);
                }
                catch
                {
                    continue;
                }
                list.RemoveAt(i);
                return true;
            }

            Exceptions.SetError(Exceptions.ValueError, "unknown event handler");
            return false;
        }


        /// <summary>
        /// Descriptor __get__ implementation. A getattr on an event returns
        /// a "bound" event that keeps a reference to the object instance.
        /// </summary>
        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp)
        {
            var self = GetManagedObject(ds) as EventObject;
            EventBinding binding;

            if (self == null)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            // If the event is accessed through its type (rather than via
            // an instance) we return an 'unbound' EventBinding that will
            // be cached for future accesses through the type.

            if (ob == IntPtr.Zero)
            {
                if (self.unbound == null)
                {
                    self.unbound = new EventBinding(self, IntPtr.Zero);
                }
                binding = self.unbound;
                Runtime.XIncref(binding.pyHandle);
                return binding.pyHandle;
            }

            if (Runtime.PyObject_IsInstance(ob, tp) < 1)
            {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            binding = new EventBinding(self, ob);
            return binding.pyHandle;
        }


        /// <summary>
        /// Descriptor __set__ implementation. This actually never allows you
        /// to set anything; it exists solely to support the '+=' spelling of
        /// event handler registration. The reason is that given code like:
        /// 'ob.SomeEvent += method', Python will attempt to set the attribute
        /// SomeEvent on ob to the result of the '+=' operation.
        /// </summary>
        public new static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val)
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
        public static IntPtr tp_repr(IntPtr ob)
        {
            var self = (EventObject)GetManagedObject(ob);
            return Runtime.PyString_FromString($"<event '{self.name}'>");
        }


        /// <summary>
        /// Descriptor dealloc implementation.
        /// </summary>
        public new static void tp_dealloc(IntPtr ob)
        {
            var self = (EventObject)GetManagedObject(ob);
            if (self.unbound != null)
            {
                Runtime.XDecref(self.unbound.pyHandle);
            }
            FinalizeObject(self);
        }
    }


    internal class Handler
    {
        public IntPtr hash;
        public Delegate del;

        public Handler(IntPtr hash, Delegate d)
        {
            this.hash = hash;
            this.del = d;
        }
    }
}
