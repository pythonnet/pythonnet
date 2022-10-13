using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Python.Runtime;

[Serializable]
internal class EventHandlerCollection: Dictionary<object, List<Handler>>
{
    readonly EventInfo info;
    public EventHandlerCollection(EventInfo @event)
    {
        info = @event;
    }

    /// <summary>
    /// Register a new Python object event handler with the event.
    /// </summary>
    internal bool AddEventHandler(BorrowedReference target, PyObject handler)
    {
        object? obj = null;
        if (target != null)
        {
            var co = (CLRObject)ManagedType.GetManagedObject(target)!;
            obj = co.inst;
        }

        // Create a true delegate instance of the appropriate type to
        // wrap the Python handler. Note that wrapper delegate creation
        // always succeeds, though calling the wrapper may fail.
        Type type = info.EventHandlerType;
        Delegate d = PythonEngine.DelegateManager.GetDelegate(type, handler);

        // Now register the handler in a mapping from instance to pairs
        // of (handler hash, delegate) so we can lookup to remove later.
        object key = obj ?? info.ReflectedType;
        if (!TryGetValue(key, out var list))
        {
            list = new List<Handler>();
            this[key] = list;
        }
        list.Add(new Handler(Runtime.PyObject_Hash(handler), d));

        // Note that AddEventHandler helper only works for public events,
        // so we have to get the underlying add method explicitly.
        object[] args = { d };
        MethodInfo mi = info.GetAddMethod(true);
        mi.Invoke(obj, BindingFlags.Default, null, args, null);

        return true;
    }


    /// <summary>
    /// Remove the given Python object event handler.
    /// </summary>
    internal bool RemoveEventHandler(BorrowedReference target, BorrowedReference handler)
    {
        object? obj = null;
        if (target != null)
        {
            var co = (CLRObject)ManagedType.GetManagedObject(target)!;
            obj = co.inst;
        }

        nint hash = Runtime.PyObject_Hash(handler);
        if (hash == -1 && Exceptions.ErrorOccurred())
        {
            return false;
        }

        object key = obj ?? info.ReflectedType;

        if (!TryGetValue(key, out var list))
        {
            Exceptions.SetError(Exceptions.ValueError, "unknown event handler");
            return false;
        }

        object?[] args = { null };
        MethodInfo mi = info.GetRemoveMethod(true);

        for (var i = 0; i < list.Count; i++)
        {
            var item = (Handler)list[i];
            if (item.hash != hash)
            {
                continue;
            }
            args[0] = item.del;
            try
            {
                mi.Invoke(obj, BindingFlags.Default, null, args, null);
            }
            catch
            {
                continue;
            }
            list.RemoveAt(i);
            if (list.Count == 0)
            {
                Remove(key);
            }
            return true;
        }

        Exceptions.SetError(Exceptions.ValueError, "unknown event handler");
        return false;
    }

    #region Serializable
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    protected EventHandlerCollection(SerializationInfo info, StreamingContext context)
            : base(info, context)
    {
        this.info = (EventInfo)info.GetValue("event", typeof(EventInfo));
    }
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);

        info.AddValue("event", this.info);
    }
    #endregion
}
