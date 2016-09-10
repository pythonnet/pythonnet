using System;
using System.Collections;
using System.Reflection;
using System.Security.Permissions;

namespace Python.Runtime
{
    /// <summary>
    /// Module level properties (attributes)
    /// </summary>
    internal class ModulePropertyObject : ExtensionType
    {
        public ModulePropertyObject(PropertyInfo md) : base()
        {
            throw new NotImplementedException("ModulePropertyObject");
        }
    }
}