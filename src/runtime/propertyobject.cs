using System;
using System.Reflection;
using System.Security.Permissions;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python descriptor type that manages CLR properties.
    /// </summary>
    internal class PropertyObject : ExtensionType
    {
        private PropertyInfo info;
        private MethodInfo getter;
        private MethodInfo setter;

        [StrongNameIdentityPermission(SecurityAction.Assert)]
        public PropertyObject(PropertyInfo md)
        {
            getter = md.GetGetMethod(true);
            setter = md.GetSetMethod(true);
            info = md;
        }


        /// <summary>
        /// Descriptor __get__ implementation. This method returns the
        /// value of the property on the given object. The returned value
        /// is converted to an appropriately typed Python object.
        /// </summary>
        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp)
        {
            var self = (PropertyObject)GetManagedObject(ds);
            MethodInfo getter = self.getter;
            object result;


            if (getter == null)
            {
                return Exceptions.RaiseTypeError("property cannot be read");
            }

            if (ob == IntPtr.Zero || ob == Runtime.PyNone)
            {
                if (!getter.IsStatic)
                {
                    Exceptions.SetError(Exceptions.TypeError,
                        "instance property must be accessed through a class instance");
                    return IntPtr.Zero;
                }

                try
                {
                    result = self.info.GetValue(null, null);
                    return Converter.ToPython(result, self.info.PropertyType);
                }
                catch (Exception e)
                {
                    return Exceptions.RaiseTypeError(e.Message);
                }
            }

            var co = GetManagedObject(ob) as CLRObject;
            if (co == null)
            {
                return Exceptions.RaiseTypeError("invalid target");
            }

            try
            {
                result = self.info.GetValue(co.inst, null);
                return Converter.ToPython(result, self.info.PropertyType);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }


        /// <summary>
        /// Descriptor __set__ implementation. This method sets the value of
        /// a property based on the given Python value. The Python value must
        /// be convertible to the type of the property.
        /// </summary>
        public new static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val)
        {
            var self = (PropertyObject)GetManagedObject(ds);
            MethodInfo setter = self.setter;
            object newval;

            if (val == IntPtr.Zero)
            {
                Exceptions.RaiseTypeError("cannot delete property");
                return -1;
            }

            if (setter == null)
            {
                Exceptions.RaiseTypeError("property is read-only");
                return -1;
            }


            if (!Converter.ToManaged(val, self.info.PropertyType, out newval, true))
            {
                return -1;
            }

            bool is_static = setter.IsStatic;

            if (ob == IntPtr.Zero || ob == Runtime.PyNone)
            {
                if (!is_static)
                {
                    Exceptions.RaiseTypeError("instance property must be set on an instance");
                    return -1;
                }
            }

            try
            {
                if (!is_static)
                {
                    var co = GetManagedObject(ob) as CLRObject;
                    if (co == null)
                    {
                        Exceptions.RaiseTypeError("invalid target");
                        return -1;
                    }
                    self.info.SetValue(co.inst, newval, null);
                }
                else
                {
                    self.info.SetValue(null, newval, null);
                }
                return 0;
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return -1;
            }
        }


        /// <summary>
        /// Descriptor __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr ob)
        {
            var self = (PropertyObject)GetManagedObject(ob);
            return Runtime.PyString_FromString($"<property '{self.info.Name}'>");
        }
    }
}
