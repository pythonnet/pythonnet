using System.Collections.Generic;

namespace Python.Runtime;

public interface ICLRObjectStorer
{
    ICollection<CLRMappedItem> Store(CLRWrapperCollection wrappers, RuntimeDataStorage storage);
    CLRWrapperCollection Restore(RuntimeDataStorage storage);
}
