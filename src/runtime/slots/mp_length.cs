using System;
using System.Reflection;

namespace Python.Runtime.Slots
{
    static class mp_length_slot
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

            Type t = co.inst.GetType();
            PropertyInfo p = t.GetProperty("Count");
            if (p != null)
            {
                return (int)p.GetValue(co.inst, null);
            }

            Exceptions.SetError(Exceptions.TypeError, $"object of type '{co.inst.GetType().Name}' has no len()");
            return -1;
        }
    }
}
