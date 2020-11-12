using System;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type that provides access to CLR object methods.
    /// </summary>
    internal class TypeMethod : MethodObject
    {
        public TypeMethod(Type type, string name, MethodInfo[] info) :
            base(type, name, info)
        {
        }

        public TypeMethod(Type type, string name, MethodInfo[] info, bool allow_threads) :
            base(type, name, info, allow_threads)
        {
        }

        public override IntPtr Invoke(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            MethodInfo mi = info[0];
            var arglist = new object[3];
            arglist[0] = ob.DangerousGetAddressOrNull();
            arglist[1] = args.DangerousGetAddress();
            arglist[2] = kw.DangerousGetAddressOrNull();

            try
            {
                object inst = ob.IsNull ? null : GetManagedObject(ob);
                return (IntPtr)mi.Invoke(inst, BindingFlags.Default, null, arglist, null);
            }
            catch (Exception e)
            {
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }
    }
}
