using System;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Module level functions
    /// </summary>
    [Serializable]
    internal class ModuleFunctionObject : MethodObject
    {
        public ModuleFunctionObject(Type type, string name, MethodInfo[] info, bool allow_threads)
            : base(type, name, info, allow_threads)
        {
            if (info.Any(item => !item.IsStatic))
            {
                throw new Exception("Module function must be static.");
            }
        }

        /// <summary>
        /// __call__ implementation.
        /// </summary>
        public static IntPtr tp_call(IntPtr obRaw, IntPtr args, IntPtr kw)
        {
            var ob = new BorrowedReference(obRaw);
            var self = (ModuleFunctionObject)GetManagedObject(ob);
            return self.Invoke(ob, new BorrowedReference(args), new BorrowedReference(kw));
        }

        /// <summary>
        /// __repr__ implementation.
        /// </summary>
        public new static IntPtr tp_repr(IntPtr ob)
        {
            var self = (ModuleFunctionObject)GetManagedObject(ob);
            return Runtime.PyString_FromString($"<CLRModuleFunction '{self.name}'>");
        }
    }
}
