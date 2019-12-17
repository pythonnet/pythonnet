using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Python.Runtime.Slots
{
    internal static class mp_length_slot
    {
        /// <summary>
        /// Implements __len__ for classes that implement ICollection
        /// (this includes any IList implementer or Array subclass)
        /// </summary>
        public static int mp_length(IntPtr ob)
        {
            var co = ManagedType.GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                Exceptions.RaiseTypeError("invalid object");
            }

            // first look for ICollection implementation directly
            if (co.inst is ICollection c)
            {
                return c.Count;
            }

            Type clrType = co.inst.GetType();

            // now look for things that implement ICollection<T> directly (non-explicitly)
            PropertyInfo p = clrType.GetProperty("Count");
            if (p != null && clrType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                return (int)p.GetValue(co.inst, null);
            }

            // finally look for things that implement the interface explicitly
            var iface = clrType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
            if (iface != null)
            {
                p = iface.GetProperty(nameof(ICollection<int>.Count));
                return (int)p.GetValue(co.inst, null);
            }

            Exceptions.SetError(Exceptions.TypeError, $"object of type '{clrType.Name}' has no len()");
            return -1;
        }
    }
}
