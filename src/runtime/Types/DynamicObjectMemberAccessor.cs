using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace Python.Runtime;

class DynamicObjectMemberAccessor
{
    const int MaxCacheEntries = 1000;

    readonly ConcurrentLruCache<MemberKey, Func<object, object>> getters = new(MaxCacheEntries);
    readonly ConcurrentLruCache<MemberKey, Action<object, object?>> setters = new(MaxCacheEntries);

    static readonly CSharpArgumentInfo[] getArgumentInfo =
    {
        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
    };

    static readonly CSharpArgumentInfo[] setArgumentInfo =
    {
        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
    };

    public bool TryGetMember(IDynamicMetaObjectProvider obj, string memberName, out object? value)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));
        if (memberName is null)
            throw new ArgumentNullException(nameof(memberName));

        var getter = getters.GetOrAdd(new MemberKey(obj.GetType(), memberName), static key =>
        {
            var binder = Binder.GetMember(CSharpBinderFlags.None, key.MemberName, key.Type, getArgumentInfo);
            var callSite = CallSite<Func<CallSite, object, object>>.Create(binder);
            return obj => callSite.Target(callSite, obj);
        });

        try
        {
            value = getter(obj);
            return true;
        }
        catch (RuntimeBinderException)
        {
            value = null;
            return false;
        }
    }

    public bool TrySetMember(IDynamicMetaObjectProvider obj, string memberName, object? value)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));
        if (memberName is null)
            throw new ArgumentNullException(nameof(memberName));

        var setter = setters.GetOrAdd(new MemberKey(obj.GetType(), memberName), static key =>
        {
            var binder = Binder.SetMember(CSharpBinderFlags.None, key.MemberName, key.Type, setArgumentInfo);
            var callSite = CallSite<Action<CallSite, object, object?>>.Create(binder);
            return (obj, value) => callSite.Target(callSite, obj, value);
        });

        try
        {
            setter(obj, value);
            return true;
        }
        catch (RuntimeBinderException)
        {
            return false;
        }
    }

    readonly record struct MemberKey(Type Type, string MemberName);

    public void Clear()
    {
        getters.Clear();
        setters.Clear();
    }
}
