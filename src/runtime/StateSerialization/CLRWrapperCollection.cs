using System.Collections.ObjectModel;

namespace Python.Runtime;

public class CLRWrapperCollection : KeyedCollection<object, CLRMappedItem>
{
    public bool TryGetValue(object key, out CLRMappedItem value)
    {
        if (Dictionary == null)
        {
            value = null;
            return false;
        }
        return Dictionary.TryGetValue(key, out value);
    }

    protected override object GetKeyForItem(CLRMappedItem item)
    {
        return item.Instance;
    }
}
