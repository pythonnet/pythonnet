using System;
using System.Reflection;

namespace Python.Runtime
{
    using MaybeFieldInfo = MaybeMemberInfo<FieldInfo>;
    /// <summary>
    /// Implements a Python descriptor type that provides access to CLR fields.
    /// </summary>
    [Serializable]
    internal class FieldObject : ExtensionType
    {
        private MaybeFieldInfo info;

        public FieldObject(FieldInfo info)
        {
            this.info = new MaybeFieldInfo(info);
        }

        /// <summary>
        /// Descriptor __get__ implementation. This method returns the
        /// value of the field on the given object. The returned value
        /// is converted to an appropriately typed Python object.
        /// </summary>
        public static NewReference tp_descr_get(BorrowedReference ds, BorrowedReference ob, BorrowedReference tp)
        {
            var self = (FieldObject?)GetManagedObject(ds);
            object result;

            if (self == null)
            {
                Exceptions.SetError(Exceptions.AssertionError, "attempting to access destroyed object");
                return default;
            }
            else if (!self.info.Valid)
            {
                Exceptions.SetError(Exceptions.AttributeError, self.info.DeletedMessage);
                return default;
            }

            FieldInfo info = self.info.Value;

            if (ob == null || ob == Runtime.PyNone)
            {
                if (!info.IsStatic)
                {
                    Exceptions.SetError(Exceptions.TypeError,
                        "instance attribute must be accessed through a class instance");
                    return default;
                }
                try
                {
                    result = info.GetValue(null);
                    return Converter.ToPython(result, info.FieldType);
                }
                catch (Exception e)
                {
                    Exceptions.SetError(Exceptions.TypeError, e.Message);
                    return default;
                }
            }

            try
            {
                var co = (CLRObject?)GetManagedObject(ob);
                if (co == null)
                {
                    Exceptions.SetError(Exceptions.TypeError, "instance is not a clr object");
                    return default;
                }
                result = info.GetValue(co.inst);
                return Converter.ToPython(result, info.FieldType);
            }
            catch (Exception e)
            {
                Exceptions.SetError(Exceptions.TypeError, e.Message);
                return default;
            }
        }

        /// <summary>
        /// Descriptor __set__ implementation. This method sets the value of
        /// a field based on the given Python value. The Python value must be
        /// convertible to the type of the field.
        /// </summary>
        public static int tp_descr_set(BorrowedReference ds, BorrowedReference ob, BorrowedReference val)
        {
            var self = (FieldObject?)GetManagedObject(ds);
            if (self == null)
            {
                Exceptions.SetError(Exceptions.AssertionError, "attempting to access destroyed object");
                return -1;
            }
            else if (!self.info.Valid)
            {
                Exceptions.SetError(Exceptions.AttributeError, self.info.DeletedMessage);
                return -1;
            }

            if (val == null)
            {
                Exceptions.SetError(Exceptions.TypeError, "cannot delete field");
                return -1;
            }

            FieldInfo info = self.info.Value;

            if (info.IsLiteral || info.IsInitOnly)
            {
                Exceptions.SetError(Exceptions.TypeError, "field is read-only");
                return -1;
            }

            bool is_static = info.IsStatic;

            if (ob == null || ob == Runtime.PyNone)
            {
                if (!is_static)
                {
                    Exceptions.SetError(Exceptions.TypeError, "instance attribute must be set through a class instance");
                    return -1;
                }
            }

            if (!Converter.ToManaged(val, info.FieldType, out var newval, true))
            {
                return -1;
            }

            try
            {
                if (!is_static)
                {
                    var co = (CLRObject?)GetManagedObject(ob);
                    if (co == null)
                    {
                        Exceptions.SetError(Exceptions.TypeError, "instance is not a clr object");
                        return -1;
                    }
                    info.SetValue(co.inst, newval);
                }
                else
                {
                    info.SetValue(null, newval);
                }
                return 0;
            }
            catch (Exception e)
            {
                Exceptions.SetError(Exceptions.TypeError, e.Message);
                return -1;
            }
        }

        /// <summary>
        /// Descriptor __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (FieldObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<field '{self.info}'>");
        }
    }
}
