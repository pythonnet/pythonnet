using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Python.Runtime.Slots
{
    internal static class mp_length_slot
    {
        private static MethodInfo _lengthMethod;
        public static MethodInfo Method
        {
            get
            {
                if (_lengthMethod != null)
                {
                    return _lengthMethod;
                }
                _lengthMethod = typeof(mp_length_slot).GetMethod(
                    nameof(mp_length_slot.mp_length),
                    BindingFlags.Static | BindingFlags.NonPublic);
                Debug.Assert(_lengthMethod != null);
                return _lengthMethod;
            }
        }

        public static bool CanAssign(Type clrType)
        {
            if (typeof(ICollection).IsAssignableFrom(clrType))
            {
                return true;
            }
            if (clrType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                return true;
            }
            if (clrType.IsInterface && clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Implements __len__ for classes that implement ICollection
        /// (this includes any IList implementer or Array subclass)
        /// </summary>
        private static int mp_length(IntPtr ob)
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
