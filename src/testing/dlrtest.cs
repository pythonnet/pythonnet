using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Python.Test;

/// <summary>
/// Base class for dynamic test helpers. Uses lazy storage initialization so that
/// Python-derived subclasses can safely call DynamicObject member hooks before
/// managed field initializers have run.
/// </summary>
public class DynamicStorageObject : DynamicObject
{
    Dictionary<string, object> storage;

    // Python-defined subclasses may reach this type without running managed field
    // initializers (see ClassDerivedObject.NewObjectToPython). Via the lazy init
    // we can ensure that the access is still safe, even when the constructor has
    // not run.
    protected Dictionary<string, object> Storage => storage ??= [];

    public void AddDynamicMember(string name, object value) => Storage[name] = value;

    public override bool TryGetMember(GetMemberBinder binder, out object result)
        => Storage.TryGetValue(binder.Name, out result);

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        Storage[binder.Name] = value;
        return true;
    }

    public override bool TryDeleteMember(DeleteMemberBinder binder)
        => Storage.Remove(binder.Name);

    public override IEnumerable<string> GetDynamicMemberNames() => Storage.Keys;
}

public class DynamicMappingObject : DynamicStorageObject
{
    // Native members for testing that regular CLR access is unaffected.
    public string Label = "default";
    public int Multiplier { get; set; } = 1;
    public int Multiply(int value) => value * Multiplier;

    // Test helper: bypass normal member binding and write directly to dynamic storage.
    public void SetDynamicValue(string name, object value) => Storage[name] = value;

    // Test helper: retrieve the actual value stored in C# (for verification that None was stored as null)
    public object GetDynamicValue(string name) => Storage.TryGetValue(name, out var value) ? value : null;
}

public class RejectingSetDynamicObject : DynamicStorageObject
{
    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        if (!Storage.ContainsKey(binder.Name))
            return false;

        Storage[binder.Name] = value;
        return true;
    }
}

public class ThrowingSetDynamicObject : DynamicStorageObject
{
    public override bool TrySetMember(SetMemberBinder binder, object value)
        => throw new InvalidOperationException($"TrySetMember failed for '{binder.Name}'");
}

public class RejectingDeleteDynamicObject : DynamicStorageObject
{
    public override bool TryDeleteMember(DeleteMemberBinder binder)
    {
        if (!Storage.ContainsKey(binder.Name))
            return false;

        return Storage.Remove(binder.Name);
    }
}

public class ThrowingDeleteDynamicObject : DynamicStorageObject
{
    public override bool TryDeleteMember(DeleteMemberBinder binder)
        => throw new InvalidOperationException($"TryDeleteMember failed for '{binder.Name}'");
}
