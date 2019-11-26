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
        private MethodObject m;
        private IntPtr target;

        public OverloadMapper(MethodObject m, IntPtr target)
        {
            Runtime.XIncref(target);
            this.target = target;
            this.m = m;
        }

        /// <summary>
        /// Implement explicit overload selection using subscript syntax ([]).
        /// </summary>
        public static IntPtr mp_subscript(IntPtr tp, IntPtr idx)
        {
            var self = (OverloadMapper)GetManagedObject(tp);

            // Note: if the type provides a non-generic method with N args
            // and a generic method that takes N params, then we always
            // prefer the non-generic version in doing overload selection.

            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            MethodInfo mi = MethodBinder.MatchSignature(self.m.info, types);
            if (mi == null)
            {
                var e = "No match found for signature";
                return Exceptions.RaiseTypeError(e);
            }

            var mb = new MethodBinding(self.m, self.target) { info = mi };
            return mb.pyHandle;
        }

        /// <summary>
        /// OverloadMapper  __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr op)
        {
            var self = (OverloadMapper)GetManagedObject(op);
            IntPtr doc = self.m.GetDocString();
            Runtime.XIncref(doc);
            return doc;
        }

        /// <summary>
        /// OverloadMapper dealloc implementation.
        /// </summary>
        public new static void tp_dealloc(IntPtr ob)
        {
            var self = (OverloadMapper)GetManagedObject(ob);
            Runtime.XDecref(self.target);
            FinalizeObject(self);
        }
    }
}
