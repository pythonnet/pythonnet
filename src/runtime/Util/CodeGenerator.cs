using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private readonly AssemblyBuilder aBuilder;
        private readonly ModuleBuilder mBuilder;

        const string NamePrefix = "__Python_Runtime_Generated_";

        internal CodeGenerator()
        {
            var aname = new AssemblyName { Name = GetUniqueAssemblyName(NamePrefix + "Assembly") };
            var aa = AssemblyBuilderAccess.Run;

            aBuilder = Thread.GetDomain().DefineDynamicAssembly(aname, aa);
            mBuilder = aBuilder.DefineDynamicModule(NamePrefix + "Module");
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

        /// <summary>
        /// Generates code, that copies potentially modified objects in args array
        /// back to the corresponding byref arguments
        /// </summary>
        internal static void GenerateMarshalByRefsBack(ILGenerator il, IReadOnlyList<Type> argTypes)
        {
            // assumes argument array is in loc_0
            for (int i = 0; i < argTypes.Count; ++i)
            {
                var type = argTypes[i];
                if (type.IsByRef)
                {
                    type = type.GetElementType();

                    il.Emit(OpCodes.Ldarg, i + 1); // for stobj/stind later at the end

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldelem_Ref);

                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Unbox_Any, type);
                        il.Emit(OpCodes.Stobj, type);
                    }
                    else
                    {
                        il.Emit(OpCodes.Castclass, type);
                        il.Emit(OpCodes.Stind_Ref);
                    }
                }
            }
        }

        static string GetUniqueAssemblyName(string name)
        {
            var taken = new HashSet<string>(AppDomain.CurrentDomain
                                                     .GetAssemblies()
                                                     .Select(a => a.GetName().Name));
            for (int i = 0; i < int.MaxValue; i++)
            {
                string candidate = name + i.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate))
                    return candidate;
            }

            throw new NotSupportedException("Too many assemblies");
        }
    }
}
