using System.Runtime.CompilerServices;
using System.Reflection;
using System;
using System.Collections.Generic;

using Mono.Cecil;

namespace Python.Loader
{

    class NetStandardResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            if (name.Name == "netstandard" || name.Name == "mscorlib") {
                var asm = Assembly.Load(name.FullName);
                // Inject facade directory
                AddSearchDirectory(asm.Location + "/..");
            }
            return base.Resolve(name);
        }
    }
}
