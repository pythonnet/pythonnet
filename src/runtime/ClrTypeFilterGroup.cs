using System;
using System.Collections.Generic;
using System.Linq;

namespace Python.Runtime
{
    /// <summary>
    /// Composes the registered filters: a type is reflected only if every filter
    /// permits it. An empty group permits everything, which is the default.
    /// </summary>
    class ClrTypeFilterGroup : List<IClrTypeFilter>, IClrTypeFilter
    {
        public bool ShouldReflect(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            foreach (var filter in this)
                if (!filter.ShouldReflect(type))
                    return false;

            return true;
        }
    }
}
