using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace Python.Runtime;

class DynamicObjectMemberAccessor
{
    const int MaxCacheEntries = 1000;

    readonly ConcurrentLruCache<MemberKey, Func<object, object>> getters = new(MaxCacheEntries);
    readonly ConcurrentLruCache<MemberKey, Action<object, object?>> setters = new(MaxCacheEntries);
    readonly ConcurrentLruCache<MemberKey, Func<object, bool>> deleters = new(MaxCacheEntries);

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
            if (typeof(DynamicObject).IsAssignableFrom(key.Type))
            {
                var getBinder = new GetMemberNameBinder(key.MemberName);
                return obj =>
                {
                    if (((DynamicObject)obj).TryGetMember(getBinder, out object? result))
                    {
                        return result;
                    }

                    throw new RuntimeBinderException($"Could not get member '{key.MemberName}'");
                };
            }

            var binder = Binder.GetMember(CSharpBinderFlags.None, key.MemberName, key.Type, getArgumentInfo);
            var callSite = CallSite<Func<CallSite, IDynamicMetaObjectProvider, object>>.Create(binder);
            return obj => callSite.Target(callSite, (IDynamicMetaObjectProvider)obj);
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
            if (typeof(DynamicObject).IsAssignableFrom(key.Type))
            {
                var setBinder = new SetMemberNameBinder(key.MemberName);
                return (obj, value) =>
                {
                    if (!((DynamicObject)obj).TrySetMember(setBinder, value))
                    {
                        throw new RuntimeBinderException($"Could not set member '{key.MemberName}'");
                    }
                };
            }

            var binder = Binder.SetMember(CSharpBinderFlags.None, key.MemberName, key.Type, setArgumentInfo);
            var callSite = CallSite<Action<CallSite, IDynamicMetaObjectProvider, object?>>.Create(binder);
            return (obj, value) => callSite.Target(callSite, (IDynamicMetaObjectProvider)obj, value);
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

    public bool TryDeleteMember(IDynamicMetaObjectProvider obj, string memberName)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));
        if (memberName is null)
            throw new ArgumentNullException(nameof(memberName));

        var deleter = deleters.GetOrAdd(new MemberKey(obj.GetType(), memberName), static key =>
        {
            if (typeof(DynamicObject).IsAssignableFrom(key.Type))
            {
                var binder = new DeleteMemberNameBinder(key.MemberName);
                return obj => ((DynamicObject)obj).TryDeleteMember(binder);
            }

            if (typeof(ExpandoObject).IsAssignableFrom(key.Type))
            {
                return obj => ((IDictionary<string, object>)(ExpandoObject)obj).Remove(key.MemberName);
            }

            return _ => false;
        });

        try
        {
            return deleter(obj);
        }
        catch (RuntimeBinderException)
        {
            return false;
        }
    }

    public IReadOnlyCollection<string> GetDynamicMemberNames(IDynamicMetaObjectProvider obj)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));

        if (obj is ExpandoObject expandoObject)
        {
            return ((IDictionary<string, object>)expandoObject).Keys.ToArray();
        }

        if (obj is DynamicObject dynamicObject)
        {
            return dynamicObject.GetDynamicMemberNames().ToArray();
        }

        var metaObject = obj.GetMetaObject(Expression.Constant(obj));
        return metaObject.GetDynamicMemberNames().ToArray();
    }

    readonly record struct MemberKey(Type Type, string MemberName);

    sealed class DeleteMemberNameBinder : DeleteMemberBinder
    {
        public DeleteMemberNameBinder(string name)
            : base(name, ignoreCase: false)
        {
        }

        public override DynamicMetaObject FallbackDeleteMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
            => errorSuggestion ?? throw new RuntimeBinderException($"Could not delete member '{Name}'");
    }

    sealed class GetMemberNameBinder : GetMemberBinder
    {
        public GetMemberNameBinder(string name)
            : base(name, ignoreCase: false)
        {
        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
            => errorSuggestion ?? throw new RuntimeBinderException($"Could not get member '{Name}'");
    }

    sealed class SetMemberNameBinder : SetMemberBinder
    {
        public SetMemberNameBinder(string name)
            : base(name, ignoreCase: false)
        {
        }

        public override DynamicMetaObject FallbackSetMember(
            DynamicMetaObject target,
            DynamicMetaObject value,
            DynamicMetaObject? errorSuggestion)
            => errorSuggestion ?? throw new RuntimeBinderException($"Could not set member '{Name}'");
    }

    public void Clear()
    {
        getters.Clear();
        setters.Clear();
        deleters.Clear();
    }
}
