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
        /// Finds a get_Count (Count property) that is private and
        /// is either ICollection or ICollection&lt;T&gt; explicit
        /// implementation
        /// </summary>
        static MethodInfo GetCountGetter(Type t)
        {
            foreach (var info in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (info.IsFinal && info.IsPrivate && (info.Name.Contains("System.Collections.Generic.ICollection") || info.Name.Contains("System.Collections.ICollection")) && info.Name.Contains("get_Count"))
                {
                    return info;
                }
            }
            return null;
        }

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

            // finally look for things that explicitly implement ICollection or ICollection<T>
            MethodInfo m = GetCountGetter(clrType);
            if (m != null)
            {
                return (int)m.Invoke(co.inst, null);
            }

            Exceptions.SetError(Exceptions.TypeError, $"object of type '{clrType.Name}' has no len()");
            return -1;
        }
    }
}
