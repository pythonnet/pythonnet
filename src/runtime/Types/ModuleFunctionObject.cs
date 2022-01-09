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
        public static NewReference tp_call(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            var self = (ModuleFunctionObject)GetManagedObject(ob)!;
            return self.Invoke(ob, args, kw);
        }

        /// <summary>
        /// __repr__ implementation.
        /// </summary>
        public new static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (ModuleFunctionObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<CLRModuleFunction '{self.name}'>");
        }
    }
}
