using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;

using RuntimeBinder = Microsoft.CSharp.RuntimeBinder;

namespace Python.Runtime
{
    /// <summary>
    /// Managed class that provides the implementation for reflected dynamic types.
    /// This has the usage as ClassObject but for the dynamic types special case,
    /// that is, classes implementing IDynamicMetaObjectProvider interface.
    /// This adds support for using dynamic properties of the C# object.
    /// </summary>
    [Serializable]
    internal class DynamicClassObject : ClassObject
    {
        internal DynamicClassObject(Type tp) : base(tp)
        {
        }

        private static Dictionary<ValueTuple<Type, string>, CallSite<Func<CallSite, object, object>>> _getAttrCallSites = new();
        private static Dictionary<ValueTuple<Type, string>, CallSite<Func<CallSite, object, object, object>>> _setAttrCallSites = new();

        private static CallSite<Func<CallSite, object, object>> GetAttrCallSite(string name, Type objectType)
        {
            var key = ValueTuple.Create(objectType, name);
            if (!_getAttrCallSites.TryGetValue(key, out var callSite))
            {
                var binder = RuntimeBinder.Binder.GetMember(
                    RuntimeBinder.CSharpBinderFlags.None,
                    name,
                    objectType,
                    new[] { RuntimeBinder.CSharpArgumentInfo.Create(RuntimeBinder.CSharpArgumentInfoFlags.None, null) });
                callSite = CallSite<Func<CallSite, object, object>>.Create(binder);
                _getAttrCallSites[key] = callSite;
            }

            return callSite;
        }

        private static CallSite<Func<CallSite, object, object, object>> SetAttrCallSite(string name, Type objectType)
        {
            var key = ValueTuple.Create(objectType, name);
            if (!_setAttrCallSites.TryGetValue(key, out var callSite))
            {
                var binder = RuntimeBinder.Binder.SetMember(
                    RuntimeBinder.CSharpBinderFlags.None,
                    name,
                    objectType,
                    new[]
                    {
                        RuntimeBinder.CSharpArgumentInfo.Create(RuntimeBinder.CSharpArgumentInfoFlags.None, null),
                        RuntimeBinder.CSharpArgumentInfo.Create(RuntimeBinder.CSharpArgumentInfoFlags.None, null)
                    });
                callSite = CallSite<Func<CallSite, object, object, object>>.Create(binder);
                _setAttrCallSites[key] = callSite;
            }
            return callSite;
        }

        /// <summary>
        /// Type __getattro__ implementation.
        /// </summary>
        public static NewReference tp_getattro(BorrowedReference ob, BorrowedReference key)
        {
            var result = Runtime.PyObject_GenericGetAttr(ob, key);

            // Property not found, but it can still be a dynamic one if the object is an IDynamicMetaObjectProvider
            if (result.IsNull())
            {
                var clrObj = (CLRObject)GetManagedObject(ob)!;
                if (clrObj?.inst is IDynamicMetaObjectProvider)
                {

                    // The call to Runtime.PyObject_GenericGetAttr above ended up with an AttributeError
                    // for dynamic properties since they are not found in the C# object definition.
                    if (Exceptions.ExceptionMatches(Exceptions.AttributeError))
                    {
                        Exceptions.Clear();
                    }

                    var name = Runtime.GetManagedString(key);
                    var callSite = GetAttrCallSite(name, clrObj.inst.GetType());

                    try
                    {
                        var res = callSite.Target(callSite, clrObj.inst);
                        return Converter.ToPython(res);
                    }
                    catch (RuntimeBinder.RuntimeBinderException)
                    {
                        Exceptions.SetError(Exceptions.AttributeError, $"'{clrObj?.inst.GetType()}' object has no attribute '{name}'");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Type __setattr__ implementation.
        /// </summary>
        public static int tp_setattro(BorrowedReference ob, BorrowedReference key, BorrowedReference val)
        {
            var clrObj = (CLRObject)GetManagedObject(ob)!;
            var name = Runtime.GetManagedString(key);

            // If the key corresponds to a member of the class, we let the default implementation handle it.
            var clrObjectType = clrObj.inst.GetType();
            if (clrObjectType.GetMember(name).Length != 0)
            {
                return Runtime.PyObject_GenericSetAttr(ob, key, val);
            }

            // If the value is a managed object, we get it from the reference. If it is a Python object, we assign it as is.
            var value = ((CLRObject)GetManagedObject(val))?.inst ?? PyObject.FromNullableReference(val);

            var callsite = SetAttrCallSite(name, clrObjectType);
            callsite.Target(callsite, clrObj.inst, value);

            return 0;
        }
    }
}
