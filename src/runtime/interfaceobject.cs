using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Provides the implementation for reflected interface types. Managed
    /// interfaces are represented in Python by actual Python type objects.
    /// Each of those type objects is associated with an instance of this
    /// class, which provides the implementation for the Python type.
    /// </summary>
    [Serializable]
    internal class InterfaceObject : ClassBase
    {
        internal ConstructorInfo ctor;

        internal InterfaceObject(Type tp) : base(tp)
        {
            var coclass = (CoClassAttribute)Attribute.GetCustomAttribute(tp, cc_attr);
            if (coclass != null)
            {
                ctor = coclass.CoClass.GetConstructor(Type.EmptyTypes);
            }
        }

        private static Type cc_attr;

        static InterfaceObject()
        {
            cc_attr = typeof(CoClassAttribute);
        }

        /// <summary>
        /// Implements __new__ for reflected interface types.
        /// </summary>
        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            var self = (InterfaceObject)GetManagedObject(tp);
            var nargs = Runtime.PyTuple_Size(args);
            Type type = self.type;
            object obj;

            if (nargs == 1)
            {
                IntPtr inst = Runtime.PyTuple_GetItem(args, 0);
                var co = GetManagedObject(inst) as CLRObject;

                if (co == null || !type.IsInstanceOfType(co.inst))
                {
                    Exceptions.SetError(Exceptions.TypeError, $"object does not implement {type.Name}");
                    return IntPtr.Zero;
                }

                obj = co.inst;
            }

            else if (nargs == 0 && self.ctor != null)
            {
                obj = self.ctor.Invoke(null);

                if (obj == null || !type.IsInstanceOfType(obj))
                {
                    Exceptions.SetError(Exceptions.TypeError, "CoClass default constructor failed");
                    return IntPtr.Zero;
                }
            }

            else
            {
                Exceptions.SetError(Exceptions.TypeError, "interface takes exactly one argument");
                return IntPtr.Zero;
            }

            return self.WrapObject(obj);
        }

        /// <summary>
        /// Wrap the given object in an interface object, so that only methods
        /// of the interface are available.
        /// </summary>
        public IntPtr WrapObject(object impl)
        {
            var objPtr = CLRObject.GetInstHandle(impl, pyHandle);
            return objPtr;
        }

        /// <summary>
        /// Expose the wrapped implementation through attributes in both
        /// converted/encoded (__implementation__) and raw (__raw_implementation__) form.
        /// </summary>
        public static IntPtr tp_getattro(IntPtr ob, IntPtr key)
        {
            var clrObj = (CLRObject)GetManagedObject(ob);

            if (!Runtime.PyString_Check(key))
            {
                return Exceptions.RaiseTypeError("string expected");
            }

            string name = Runtime.GetManagedString(key);
            if (name == "__implementation__")
            {
                return Converter.ToPython(clrObj.inst);
            }
            else if (name == "__raw_implementation__")
            {
                return CLRObject.GetInstHandle(clrObj.inst);
            }

            return Runtime.PyObject_GenericGetAttr(ob, key);
        }
    }
}
