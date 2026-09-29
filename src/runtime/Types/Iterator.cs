using System;
using System.Collections;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a generic Python iterator for IEnumerable objects and
    /// managed array objects. This supports 'for i in object:' in Python.
    /// </summary>
    internal class Iterator : ExtensionType
    {
        private readonly IEnumerator iter;
        private readonly Type elemType;

        public Iterator(IEnumerator e, Type elemType)
        {
            iter = e;
            this.elemType = elemType;
        }


        /// <summary>
        /// Implements support for the Python iteration protocol.
        /// </summary>
        public static NewReference tp_iternext(BorrowedReference ob)
        {
            var self = (Iterator)GetManagedObject(ob)!;
            try
            {
                if (!self.iter.MoveNext())
                {
                    Exceptions.SetError(Exceptions.StopIteration, Runtime.PyNone);
                    return default;
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return default;
            }
            // Current and the conversion can both throw - the latter for a type
            // refused by an IClrTypeFilter - and must not unwind through Python
            try
            {
                object item = self.iter.Current;
                return Converter.ToPython(item, self.elemType);
            }
            catch (Exception e)
            {
                Exceptions.SetError(e);
                return default;
            }
        }

        public static NewReference tp_iter(BorrowedReference ob) => new (ob);
    }
}
