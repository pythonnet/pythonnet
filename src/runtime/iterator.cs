using System;
using System.Collections;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a generic Python iterator for IEnumerable objects and
    /// managed array objects. This supports 'for i in object:' in Python.
    /// </summary>
    internal class Iterator : ExtensionType
    {
        IEnumerator iter;

        public Iterator(IEnumerator e) : base()
        {
            this.iter = e;
        }


        /// <summary>
        /// Implements support for the Python iteration protocol.
        /// </summary>
        public static IntPtr tp_iternext(IntPtr ob)
        {
            Iterator self = GetManagedObject(ob) as Iterator;
            if (!self.iter.MoveNext())
            {
                Exceptions.SetError(Exceptions.StopIteration, Runtime.PyNone);
                return IntPtr.Zero;
            }
            object item = self.iter.Current;
            return Converter.ToPythonImplicit(item);
        }

        public static IntPtr tp_iter(IntPtr ob)
        {
            Runtime.XIncref(ob);
            return ob;
        }
    }
}
