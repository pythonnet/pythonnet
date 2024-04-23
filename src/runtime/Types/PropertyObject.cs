using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

using Fasterflect;

namespace Python.Runtime
{
    using MaybeMethodInfo = MaybeMethodBase<MethodInfo>;
    /// <summary>
    /// Implements a Python descriptor type that manages CLR properties.
    /// </summary>
    [Serializable]
    internal class PropertyObject : ExtensionType, IDeserializationCallback
    {
        internal MaybeMemberInfo<PropertyInfo> info;
        [NonSerialized]
        private MethodInfo? getter;
        [NonSerialized]
        private MethodInfo? setter;

        private MemberGetter _memberGetter;
        private Type _memberGetterType;

        private MemberSetter _memberSetter;
        private Type _memberSetterType;

        private bool _isValueType;
        private Type _isValueTypeType;

        public PropertyObject(PropertyInfo md)
        {
            info = new MaybeMemberInfo<PropertyInfo>(md);
            CacheAccessors();
        }

        void CacheAccessors()
        {
            PropertyInfo md = info.Value;
            getter = md.GetGetMethod(true) ?? md.GetBaseGetMethod(true);
            setter = md.GetSetMethod(true) ?? md.GetBaseSetMethod(true);
        }


        /// <summary>
        /// Descriptor __get__ implementation. This method returns the
        /// value of the property on the given object. The returned value
        /// is converted to an appropriately typed Python object.
        /// </summary>
        public static NewReference tp_descr_get(BorrowedReference ds, BorrowedReference ob, BorrowedReference tp)
        {
            var self = (PropertyObject)GetManagedObject(ds)!;
            if (!self.info.Valid)
            {
                return Exceptions.RaiseTypeError(self.info.DeletedMessage);
            }
            var info = self.info.Value;
            MethodInfo? getter = self.getter;
            object result;


            if (getter == null)
            {
                return Exceptions.RaiseTypeError("property cannot be read");
            }

            if (ob == null || ob == Runtime.PyNone)
            {
                if (!getter.IsStatic)
                {
                    Exceptions.SetError(Exceptions.TypeError,
                        "instance property must be accessed through a class instance");
                    return default;
                }

                try
                {
                    var getterFunc = self.GetMemberGetter(info.DeclaringType);
                    using (Py.AllowThreads())
                    {
                        result = getterFunc(info.DeclaringType);
                    }
                    return Converter.ToPython(result, info.PropertyType);
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
                using (Py.AllowThreads())
                {
                    result = getter.Invoke(co.inst, Array.Empty<object>());
                }
                return Converter.ToPython(result, info.PropertyType);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return default;
            }
        }


        /// <summary>
        /// Descriptor __set__ implementation. This method sets the value of
        /// a property based on the given Python value. The Python value must
        /// be convertible to the type of the property.
        /// </summary>
        public static int tp_descr_set(BorrowedReference ds, BorrowedReference ob, BorrowedReference val)
        {
            var self = (PropertyObject)GetManagedObject(ds)!;
            if (!self.info.Valid)
            {
                Exceptions.RaiseTypeError(self.info.DeletedMessage);
                return -1;
            }
            var info = self.info.Value;

            MethodInfo? setter = self.setter;

            if (val == null)
            {
                Exceptions.RaiseTypeError("cannot delete property");
                return -1;
            }

            if (setter == null)
            {
                Exceptions.RaiseTypeError("property is read-only");
                return -1;
            }


            if (!Converter.ToManaged(val, info.PropertyType, out var newval, true))
            {
                return -1;
            }

            bool is_static = setter.IsStatic;

            if (ob == null || ob == Runtime.PyNone)
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
                    setter.Invoke(co.inst, new object?[] { newval });
                }
                else
                {
                    self.GetMemberSetter(info.DeclaringType)(info.DeclaringType, newval);
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
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (PropertyObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<property '{self.info}'>");
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (info.Valid)
            {
                CacheAccessors();
            }
        }


        private MemberGetter GetMemberGetter(Type type)
        {
            if (type != _memberGetterType)
            {
                _memberGetter = FasterflectManager.GetPropertyGetter(type, info.Value.Name);
                _memberGetterType = type;
            }

            return _memberGetter;
        }

        private MemberSetter GetMemberSetter(Type type)
        {
            if (type != _memberSetterType)
            {
                _memberSetter = FasterflectManager.GetPropertySetter(type, info.Value.Name);
                _memberSetterType = type;
            }

            return _memberSetter;
        }

        private bool IsValueType(Type type)
        {
            if (type != _isValueTypeType)
            {
                _isValueType = FasterflectManager.IsValueType(type);
                _isValueTypeType = type;
            }

            return _isValueType;
        }
    }
}
