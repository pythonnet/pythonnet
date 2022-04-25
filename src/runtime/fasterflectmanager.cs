using System;
using System.Collections.Generic;

using Fasterflect;

namespace Python.Runtime
{
    public static class FasterflectManager
    {
        private static Dictionary<Type, bool> _isValueTypeCache = new();
        private static Dictionary<string, MemberGetter> _memberGetterCache = new();
        private static Dictionary<string, MemberSetter> _memberSetterCache = new();

        public static bool IsValueType(Type type)
        {
            bool isValueType;
            if (_isValueTypeCache.TryGetValue(type, out isValueType))
            {
                return isValueType;
            }

            isValueType = type.IsValueType;
            _isValueTypeCache[type] = isValueType;

            return isValueType;
        }

        public static MemberGetter GetPropertyGetter(Type type, string propertyName)
        {
            var cacheKey = GetCacheKey(type, propertyName);

            MemberGetter memberGetter;
            if (_memberGetterCache.TryGetValue(cacheKey, out memberGetter))
            {
                return memberGetter;
            }

            memberGetter = type.DelegateForGetPropertyValue(propertyName);
            _memberGetterCache[cacheKey] = memberGetter;

            return memberGetter;
        }

        public static MemberSetter GetPropertySetter(Type type, string propertyName)
        {
            var cacheKey = GetCacheKey(type, propertyName);

            MemberSetter memberSetter;
            if (_memberSetterCache.TryGetValue(cacheKey, out memberSetter))
            {
                return memberSetter;
            }

            memberSetter = type.DelegateForSetPropertyValue(propertyName);
            _memberSetterCache[cacheKey] = memberSetter;

            return memberSetter;
        }

        public static MemberGetter GetFieldGetter(Type type, string fieldName)
        {
            var cacheKey = GetCacheKey(type, fieldName);

            MemberGetter memberGetter;
            if (_memberGetterCache.TryGetValue(cacheKey, out memberGetter))
            {
                return memberGetter;
            }

            memberGetter = type.DelegateForGetFieldValue(fieldName);
            _memberGetterCache[cacheKey] = memberGetter;

            return memberGetter;
        }

        public static MemberSetter GetFieldSetter(Type type, string fieldName)
        {
            var cacheKey = GetCacheKey(type, fieldName);

            MemberSetter memberSetter;
            if (_memberSetterCache.TryGetValue(cacheKey, out memberSetter))
            {
                return memberSetter;
            }

            memberSetter = type.DelegateForSetFieldValue(fieldName);
            _memberSetterCache[cacheKey] = memberSetter;

            return memberSetter;
        }

        private static string GetCacheKey(Type type, string memberName)
        {
            return $"{type} {memberName}";
        }
    }
}
