using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Python.Runtime
{
    /// <summary>
    /// Several places in the runtime generate code on the fly to support
    /// dynamic functionality. The CodeGenerator class manages the dynamic
    /// assembly used for code generation and provides utility methods for
    /// certain repetitive tasks.
    /// </summary>
    internal class CodeGenerator
    {
        private AssemblyBuilder aBuilder;
        private ModuleBuilder mBuilder;

        internal CodeGenerator()
        {
            var aname = new AssemblyName { Name = "__CodeGenerator_Assembly" };
            var aa = AssemblyBuilderAccess.Run;

            aBuilder = Thread.GetDomain().DefineDynamicAssembly(aname, aa);
            mBuilder = aBuilder.DefineDynamicModule("__CodeGenerator_Module");
        }

        /// <summary>
        /// DefineType is a shortcut utility to get a new TypeBuilder.
        /// </summary>
        internal TypeBuilder DefineType(string name)
        {
            var attrs = TypeAttributes.Public;
            return mBuilder.DefineType(name, attrs);
        }

        /// <summary>
        /// DefineType is a shortcut utility to get a new TypeBuilder.
        /// </summary>
        internal TypeBuilder DefineType(string name, Type basetype)
        {
            var attrs = TypeAttributes.Public;
            return mBuilder.DefineType(name, attrs, basetype);
        }
    }
}
