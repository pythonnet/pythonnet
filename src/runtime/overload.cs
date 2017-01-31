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
        MethodObject m;
        IntPtr target;

        public OverloadMapper(MethodObject m, IntPtr target) : base()
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
            OverloadMapper self = (OverloadMapper)GetManagedObject(tp);

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
                string e = "No match found for signature";
                return Exceptions.RaiseTypeError(e);
            }

            MethodBinding mb = new MethodBinding(self.m, self.target);
            mb.info = mi;
            Runtime.XIncref(mb.pyHandle);
            return mb.pyHandle;
        }

        /// <summary>
        /// OverloadMapper  __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr op)
        {
            OverloadMapper self = (OverloadMapper)GetManagedObject(op);
            IntPtr doc = self.m.GetDocString();
            Runtime.XIncref(doc);
            return doc;
        }

        /// <summary>
        /// OverloadMapper dealloc implementation.
        /// </summary>
        public static new void tp_dealloc(IntPtr ob)
        {
            OverloadMapper self = (OverloadMapper)GetManagedObject(ob);
            Runtime.XDecref(self.target);
            ExtensionType.FinalizeObject(self);
        }
    }
}
