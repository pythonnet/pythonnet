using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Python.Test;

public class DynamicMappingObject : DynamicObject
{
    Dictionary<string, object> storage;

    Dictionary<string, object> Storage => storage ??= [];

    // Native members for testing that regular CLR access is unaffected.
    public string Label = "default";
    public int Multiplier { get; set; } = 1;
    public int Multiply(int value) => value * Multiplier;

    // Test helper: bypass normal member binding and write directly to dynamic storage.
    public void SetDynamicValue(string name, object value) => Storage[name] = value;

    // Test helper: retrieve the actual value stored in C# (for verification that None was stored as null)
    public object GetDynamicValue(string name) => Storage.TryGetValue(name, out var value) ? value : null;



    public override bool TryGetMember(GetMemberBinder binder, out object result)
        => Storage.TryGetValue(binder.Name, out result);

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        Storage[binder.Name] = value;
        return true;
    }

    public override bool TryDeleteMember(DeleteMemberBinder binder)
        => binder is not null && Storage.Remove(binder.Name);

    public override IEnumerable<string> GetDynamicMemberNames() => Storage.Keys;
}
