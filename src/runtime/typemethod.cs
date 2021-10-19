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

        public override NewReference Invoke(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            MethodInfo mi = info[0];
            var arglist = new object?[3];
            arglist[0] = PyObject.FromNullableReference(ob);
            arglist[1] = PyObject.FromNullableReference(args);
            arglist[2] = PyObject.FromNullableReference(kw);

            try
            {
                object? inst = null;
                if (ob != null)
                {
                    inst = GetManagedObject(ob);
                }
                var result = (PyObject)mi.Invoke(inst, BindingFlags.Default, null, arglist, null);
                return new NewReference(result);
            }
            catch (Exception e)
            {
                Exceptions.SetError(e);
                return default;
            }
        }
    }
}
