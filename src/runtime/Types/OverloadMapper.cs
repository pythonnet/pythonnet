using System;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements the __overloads__ attribute of method objects. This object
    /// supports the [] syntax to explicitly select an overload by signature.
    /// </summary>
    internal class OverloadMapper : ExtensionType
    {
        private readonly MethodObject m;
        private readonly PyObject? target;

        public OverloadMapper(MethodObject m, PyObject? target)
        {
            this.target = target;
            this.m = m;
        }

        /// <summary>
        /// Implement explicit overload selection using subscript syntax ([]).
        /// </summary>
        public static NewReference mp_subscript(BorrowedReference tp, BorrowedReference idx)
        {
            var self = (OverloadMapper)GetManagedObject(tp)!;

            // Note: if the type provides a non-generic method with N args
            // and a generic method that takes N params, then we always
            // prefer the non-generic version in doing overload selection.

            Type[]? types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            MethodBase? mi = MethodBinder.MatchSignature(self.m.info, types);
            if (mi == null)
            {
                var e = "No match found for signature";
                return Exceptions.RaiseTypeError(e);
            }

            var mb = new MethodBinding(self.m, self.target) { info = mi };
            return mb.Alloc();
        }

        /// <summary>
        /// OverloadMapper  __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference op)
        {
            var self = (OverloadMapper)GetManagedObject(op)!;
            return self.m.GetDocString();
        }
    }
}
