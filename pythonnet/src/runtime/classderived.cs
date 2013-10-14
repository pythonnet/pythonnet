// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace Python.Runtime
{

    /// <summary>
    /// Managed class that provides the implementation for reflected types.
    /// Managed classes and value types are represented in Python by actual 
    /// Python type objects. Each of those type objects is associated with 
    /// an instance of ClassObject, which provides its implementation.
    /// </summary>

    internal class ClassDerivedObject : ClassObject
    {
        static private Dictionary<string, AssemblyBuilder> assemblyBuilders;
        static private Dictionary<Tuple<string, string>, ModuleBuilder> moduleBuilders;

        static ClassDerivedObject()
        {
            assemblyBuilders = new Dictionary<string, AssemblyBuilder>();
            moduleBuilders = new Dictionary<Tuple<string, string>, ModuleBuilder>();
        }

        internal ClassDerivedObject(Type tp)
            : base(tp)
        {
        }

        //====================================================================
        // Implements __new__ for derived classes of reflected classes.
        //====================================================================
        new public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            // derived classes have a __pyobj__ field that points back to the python object
            // (see Trampoline.InvokeMethod and CreateDerivedType)
            IntPtr pyobj = ClassObject.tp_new(tp, args, kw);
            CLRObject obj = (CLRObject)ManagedType.GetManagedObject(pyobj);
            FieldInfo fi = obj.inst.GetType().GetField("__pyobj__");
            fi.SetValue(obj.inst, pyobj);
            return pyobj;
        }

        //====================================================================
        // Creates a new managed type derived from a base type with any virtual
        // methods overriden to call out to python if the associated python
        // object has overriden the method.
        //====================================================================
        internal static Type CreateDerivedType(string name,
                                               Type baseType,
                                               string namespaceStr,
                                               string assemblyName,
                                               string moduleName="Python.Runtime.Dynamic.dll")
        {
            if (null != namespaceStr)
                name = namespaceStr + "." + name;

            if (null == assemblyName)
                assemblyName = Assembly.GetExecutingAssembly().FullName;

            ModuleBuilder moduleBuilder = GetModuleBuilder(assemblyName, moduleName);
            TypeBuilder typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Class);
            typeBuilder.SetParent(baseType);

            // add a field for storing the python object pointer
            FieldBuilder fb = typeBuilder.DefineField("__pyobj__", typeof(IntPtr), FieldAttributes.Public);

            // override any virtual methods
            MethodInfo[] methods = baseType.GetMethods();
            List<string> baseMethodNames = new List<string>();
            foreach (MethodInfo method in methods)
            {
                if (!method.Attributes.HasFlag(MethodAttributes.Virtual) | method.Attributes.HasFlag(MethodAttributes.Final))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

                // create a method for calling the original method
                string baseMethodName = "_" + baseType.Name + "__" + method.Name;
                baseMethodNames.Add(baseMethodName);
                MethodBuilder mb = typeBuilder.DefineMethod(baseMethodName,
                                                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig,
                                                method.ReturnType,
                                                parameterTypes);

                // emit the assembly for calling the original method using call instead of callvirt
                ILGenerator il = mb.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < parameters.Length; ++i)
                    il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Call, method);
                il.Emit(OpCodes.Ret);

                // override the original method with a new one that dispatches to python
                mb = typeBuilder.DefineMethod(method.Name,
                                                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                                                    MethodAttributes.Virtual | MethodAttributes.HideBySig,
                                                method.CallingConvention,
                                                method.ReturnType,
                                                parameterTypes);

                il = mb.GetILGenerator();
                il.DeclareLocal(typeof(Object[]));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, method.Name);
                il.Emit(OpCodes.Ldstr, baseMethodName);
                il.Emit(OpCodes.Ldc_I4, parameters.Length);
                il.Emit(OpCodes.Newarr, typeof(System.Object));
                il.Emit(OpCodes.Stloc_0);
                for (int i = 0; i < parameters.Length; ++i)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, i + 1);
                    if (parameterTypes[i].IsPrimitive)
                        il.Emit(OpCodes.Box, parameterTypes[i]);
                    il.Emit(OpCodes.Stelem, typeof(Object));
                }
                il.Emit(OpCodes.Ldloc_0);
                if (method.ReturnType == typeof(void))
                {
                    il.Emit(OpCodes.Call, typeof(Trampoline).GetMethod("InvokeMethodVoid"));
                }
                else
                {
                    il.Emit(OpCodes.Call, typeof(Trampoline).GetMethod("InvokeMethod").MakeGenericMethod(method.ReturnType));
                }
                il.Emit(OpCodes.Ret);
            }

            Type type = typeBuilder.CreateType();

            // scan the assembly so the newly added class can be imported
            Assembly assembly = Assembly.GetAssembly(type);
            AssemblyManager.ScanAssembly(assembly);

            return type;
        }


        private static ModuleBuilder GetModuleBuilder(string assemblyName, string moduleName)
        {
            // find or create a dynamic assembly and module
            AppDomain domain = AppDomain.CurrentDomain;
            ModuleBuilder moduleBuilder = null;

            if (moduleBuilders.ContainsKey(Tuple.Create(assemblyName, moduleName)))
            {
                moduleBuilder = moduleBuilders[Tuple.Create(assemblyName, moduleName)];
            }
            else
            {
                AssemblyBuilder assemblyBuilder = null;
                if (assemblyBuilders.ContainsKey(assemblyName))
                {
                    assemblyBuilder = assemblyBuilders[assemblyName];
                }
                else
                {
                    assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName),
                                                                   AssemblyBuilderAccess.Run);
                    assemblyBuilders[assemblyName] = assemblyBuilder;
                }

                moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName);
                moduleBuilders[Tuple.Create(assemblyName, moduleName)] = moduleBuilder;
            }

            return moduleBuilder;
        }

    }

    // This has to be public as it's called from methods on dynamically built classes
    // potentially in other assemblies
    public class Trampoline
    {
        //====================================================================
        // This is the implementaion of the overriden methods in the derived
        // type. It looks for a python method with the same name as the method
        // on the managed base class and if it exists and isn't the managed
        // method binding (ie it has been overriden in the derived python
        // class) it calls it, otherwise it calls the base method.
        //====================================================================
        public static T InvokeMethod<T>(Object obj, string methodName, string origMethodName, Object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            IntPtr ptr = (IntPtr)fi.GetValue(obj);
            if (null != ptr)
            {
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    PyObject pyobj = new PyObject(ptr);
                    PyObject method = pyobj.GetAttr(methodName, new PyObject(Runtime.PyNone));
                    if (method.Handle != Runtime.PyNone)
                    {
                        // if the method hasn't been overriden then it will be a managed object
                        ManagedType managedMethod = ManagedType.GetManagedObject(method.Handle);
                        if (null == managedMethod)
                        {
                            PyObject[] pyargs = new PyObject[args.Length];
                            for (int i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = new PyObject(Converter.ToPython(args[i], args[i].GetType()));
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            return (T)py_result.AsManagedObject(typeof(T));
                        }
                    }
                }
                finally
                {
                    Runtime.PyGILState_Release(gs);
                }
            }

            return (T)obj.GetType().InvokeMember(origMethodName,
                                                 BindingFlags.InvokeMethod,
                                                 null,
                                                 obj,
                                                 args);
        }

        public static void InvokeMethodVoid(Object obj, string methodName, string origMethodName, Object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            IntPtr ptr = (IntPtr)fi.GetValue(obj);
            if (null != ptr)
            {
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    PyObject pyobj = new PyObject(ptr);
                    PyObject method = pyobj.GetAttr(methodName, new PyObject(Runtime.PyNone));
                    if (method.Handle != Runtime.PyNone)
                    {
                        // if the method hasn't been overriden then it will be a managed object
                        ManagedType managedMethod = ManagedType.GetManagedObject(method.Handle);
                        if (null == managedMethod)
                        {
                            PyObject[] pyargs = new PyObject[args.Length];
                            for (int i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = new PyObject(Converter.ToPython(args[i], args[i].GetType()));
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            return;
                        }
                    }
                }
                finally
                {
                    Runtime.PyGILState_Release(gs);
                }
            }

            obj.GetType().InvokeMember(origMethodName,
                                       BindingFlags.InvokeMethod,
                                       null,
                                       obj,
                                       args);
        }
    }
}
