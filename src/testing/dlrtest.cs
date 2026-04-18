using System.Collections.Generic;
using System.Dynamic;

namespace Python.Test;

public class DynamicMappingObject : DynamicObject
{
    readonly Dictionary<string, object> storage = [];

    // Native members for testing that regular CLR access is unaffected.
    public string Label = "default";
    public int Multiplier { get; set; } = 1;
    public int Multiply(int value) => value * Multiplier;

    // Test helper: bypass normal member binding and write directly to dynamic storage.
    public void SetDynamicValue(string name, object value) => storage[name] = value;

    public override bool TryGetMember(GetMemberBinder binder, out object result)
        => storage.TryGetValue(binder.Name, out result);

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        storage[binder.Name] = value;
        return true;
    }

    public override bool TryDeleteMember(DeleteMemberBinder binder)
        => storage.Remove(binder.Name);

    public override IEnumerable<string> GetDynamicMemberNames() => storage.Keys;
}
