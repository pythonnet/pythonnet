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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Python.Runtime
{

    /// <summary>
    /// Managed class that provides the implementation for reflected types.
    /// Managed classes and value types are represented in Python by actual 
    /// Python type objects. Each of those type objects is associated with 
    /// an instance of ClassObject, which provides its implementation.
    /// </summary>

    // interface used to idenfity which C# types were dynamically created as python subclasses
    public interface IPythonDerivedType
    {
    }

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
            ClassDerivedObject cls = GetManagedObject(tp) as ClassDerivedObject;

            // call the managed constructor
            Object obj = cls.binder.InvokeRaw(IntPtr.Zero, args, kw);
            if (obj == null)
                return IntPtr.Zero;

            // return the pointer to the python object
            // (this indirectly calls ClassDerivedObject.ToPython)
            return Converter.ToPython(obj, cls.GetType());
        }

        new public static void tp_dealloc(IntPtr ob)
        {
            CLRObject self = (CLRObject)GetManagedObject(ob);

            // don't let the python GC destroy this object
            Runtime.PyObject_GC_UnTrack(self.pyHandle);

            // The python should now have a ref count of 0, but we don't actually want to
            // deallocate the object until the C# object that references it is destroyed.
            // So we don't call PyObject_GC_Del here and instead we set the python
            // reference to a weak reference so that the C# object can be collected.
            GCHandle gc = GCHandle.Alloc(self, GCHandleType.Weak);
            Marshal.WriteIntPtr(self.pyHandle, ObjectOffset.magic(self.tpHandle), (IntPtr)gc);
            self.gcHandle.Free();
            self.gcHandle = gc;
        }

        // Called from Converter.ToPython for types that are python subclasses of managed types.
        // The referenced python object is returned instead of a new wrapper.
        internal static IntPtr ToPython(IPythonDerivedType obj)
        {
            // derived types have a __pyobj__ field that gets set to the python
            // object in the overriden constructor
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            CLRObject self = (CLRObject)fi.GetValue(obj);

            Runtime.Incref(self.pyHandle);

            // when the C# constructor creates the python object it starts as a weak
            // reference with a reference count of 0. Now we're passing this object
            // to Python the reference count needs to be incremented and the reference
            // needs to be replaced with a strong reference to stop the C# object being
            // collected while Python still has a reference to it.
            if (Runtime.Refcount(self.pyHandle) == 1)
            {
                GCHandle gc = GCHandle.Alloc(self, GCHandleType.Normal);
                Marshal.WriteIntPtr(self.pyHandle, ObjectOffset.magic(self.tpHandle), (IntPtr)gc);
                self.gcHandle.Free();
                self.gcHandle = gc;

                // now the object has a python reference it's safe for the python GC to track it
                Runtime.PyObject_GC_Track(self.pyHandle);
            }

            return self.pyHandle;
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
            TypeBuilder typeBuilder;

            Type baseClass = baseType;
            List<Type> interfaces = new List<Type> { typeof(IPythonDerivedType) };

            // if the base type is an interface then use System.Object as the base class
            // and add the base type to the list of interfaces this new class will implement.
            if (baseType.IsInterface)
            {
                interfaces.Add(baseType);
                baseClass = typeof(System.Object);
            }

            typeBuilder = moduleBuilder.DefineType(name,
                                                    TypeAttributes.Public | TypeAttributes.Class,
                                                    baseClass,
                                                    interfaces.ToArray());

            ILGenerator il;
            MethodBuilder mb;

            // add a field for storing the python object pointer
            FieldBuilder fb = typeBuilder.DefineField("__pyobj__", typeof(CLRObject), FieldAttributes.Public);

            // override any constructors
            ConstructorInfo[] constructors = baseClass.GetConstructors();
            foreach (ConstructorInfo ctor in constructors)
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

                // create a method for calling the original constructor
                string baseCtorName = "_" + baseType.Name + "__cinit__";
                mb = typeBuilder.DefineMethod(baseCtorName,
                                                MethodAttributes.Public |
                                                    MethodAttributes.Final |
                                                    MethodAttributes.HideBySig,
                                                typeof(void),
                                                parameterTypes);

                // emit the assembly for calling the original method using call instead of callvirt
                il = mb.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < parameters.Length; ++i)
                    il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Call, ctor);
                il.Emit(OpCodes.Ret);

                // override the original method with a new one that dispatches to python
                ConstructorBuilder cb = typeBuilder.DefineConstructor(MethodAttributes.Public | 
                                                                        MethodAttributes.ReuseSlot |
                                                                        MethodAttributes.HideBySig,
                                                                      ctor.CallingConvention,
                                                                      parameterTypes);
                il = cb.GetILGenerator();
                il.DeclareLocal(typeof(Object[]));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, baseCtorName);
                il.Emit(OpCodes.Ldc_I4, parameters.Length);
                il.Emit(OpCodes.Newarr, typeof(System.Object));
                il.Emit(OpCodes.Stloc_0);
                for (int i = 0; i < parameters.Length; ++i)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, i + 1);
                    if (parameterTypes[i].IsValueType)
                        il.Emit(OpCodes.Box, parameterTypes[i]);
                    il.Emit(OpCodes.Stelem, typeof(Object));
                }
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeCtor"));
                il.Emit(OpCodes.Ret);
            }

            // override any virtual methods
            MethodInfo[] methods = baseType.GetMethods();
            foreach (MethodInfo method in methods)
            {
                if (!method.Attributes.HasFlag(MethodAttributes.Virtual) | method.Attributes.HasFlag(MethodAttributes.Final))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

                // create a method for calling the original method
                string baseMethodName = "_" + baseType.Name + "__" + method.Name;
                mb = typeBuilder.DefineMethod(baseMethodName,
                                                MethodAttributes.Public |
                                                    MethodAttributes.Final |
                                                    MethodAttributes.HideBySig,
                                                method.ReturnType,
                                                parameterTypes);

                // emit the assembly for calling the original method using call instead of callvirt
                il = mb.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < parameters.Length; ++i)
                    il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Call, method);
                il.Emit(OpCodes.Ret);

                // override the original method with a new one that dispatches to python
                mb = typeBuilder.DefineMethod(method.Name,
                                                MethodAttributes.Public |
                                                    MethodAttributes.ReuseSlot |
                                                    MethodAttributes.Virtual |
                                                    MethodAttributes.HideBySig,
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
                    if (parameterTypes[i].IsValueType)
                        il.Emit(OpCodes.Box, parameterTypes[i]);
                    il.Emit(OpCodes.Stelem, typeof(Object));
                }
                il.Emit(OpCodes.Ldloc_0);
                if (method.ReturnType == typeof(void))
                {
                    il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeMethodVoid"));
                }
                else
                {
                    il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeMethod").MakeGenericMethod(method.ReturnType));
                }
                il.Emit(OpCodes.Ret);
            }

            // add the destructor so the python object created in the constructor gets destroyed
            mb = typeBuilder.DefineMethod("Finalize",
                                            MethodAttributes.Family |
                                                MethodAttributes.Virtual |
                                                MethodAttributes.HideBySig,
                                            CallingConventions.Standard,
                                            typeof(void),
                                            Type.EmptyTypes);
            il = mb.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("Finalize"));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseClass.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance));
            il.Emit(OpCodes.Ret);

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

    //
    // PythonDerivedType contains static methods used by the dynamically created
    // derived type that allow it to call back into python from overriden virtual
    // methods, and also handle the construction and destruction of the python
    // object.
    //
    // This has to be public as it's called from methods on dynamically built classes
    // potentially in other assemblies.
    //
    public class PythonDerivedType
    {
        //====================================================================
        // This is the implementaion of the overriden methods in the derived
        // type. It looks for a python method with the same name as the method
        // on the managed base class and if it exists and isn't the managed
        // method binding (ie it has been overriden in the derived python
        // class) it calls it, otherwise it calls the base method.
        //====================================================================
        public static T InvokeMethod<T>(IPythonDerivedType obj, string methodName, string origMethodName, Object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            CLRObject self = (CLRObject)fi.GetValue(obj);

            if (null != self)
            {
                List<PyObject> disposeList = new List<PyObject>();
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    Runtime.Incref(self.pyHandle);
                    PyObject pyself = new PyObject(self.pyHandle);
                    disposeList.Add(pyself);

                    Runtime.Incref(Runtime.PyNone);
                    PyObject pynone = new PyObject(Runtime.PyNone);
                    disposeList.Add(pynone);

                    PyObject method = pyself.GetAttr(methodName, pynone);
                    disposeList.Add(method);
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
                                disposeList.Add(pyargs[i]);
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            disposeList.Add(py_result);
                            return (T)py_result.AsManagedObject(typeof(T));
                        }
                    }
                }
                finally
                {
                    foreach (PyObject x in disposeList) {
                        if (x != null)
                            x.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            return (T)obj.GetType().InvokeMember(origMethodName,
                                                 BindingFlags.InvokeMethod,
                                                 null,
                                                 obj,
                                                 args);
        }

        public static void InvokeMethodVoid(IPythonDerivedType obj, string methodName, string origMethodName, Object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            CLRObject self = (CLRObject)fi.GetValue(obj);
            if (null != self)
            {
                List<PyObject> disposeList = new List<PyObject>();
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    Runtime.Incref(self.pyHandle);
                    PyObject pyself = new PyObject(self.pyHandle);
                    disposeList.Add(pyself);

                    Runtime.Incref(Runtime.PyNone);
                    PyObject pynone = new PyObject(Runtime.PyNone);
                    disposeList.Add(pynone);

                    PyObject method = pyself.GetAttr(methodName, pynone);
                    disposeList.Add(method);
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
                                disposeList.Add(pyargs[i]);
                            }

                            PyObject py_result = method.Invoke(pyargs);
                            disposeList.Add(py_result);
                            return;
                        }
                    }
                }
                finally
                {
                    foreach (PyObject x in disposeList) {
                        if (x != null)
                            x.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            obj.GetType().InvokeMember(origMethodName,
                                       BindingFlags.InvokeMethod,
                                       null,
                                       obj,
                                       args);
        }

        public static void InvokeCtor(IPythonDerivedType obj, string origCtorName, Object[] args)
        {
            // call the base constructor
            obj.GetType().InvokeMember(origCtorName,
                                       BindingFlags.InvokeMethod,
                                       null,
                                       obj,
                                       args);

            List<PyObject> disposeList = new List<PyObject>();
            CLRObject self = null;
            IntPtr gs = Runtime.PyGILState_Ensure();
            try
            {
                // create the python object
                IntPtr type = TypeManager.GetTypeHandle(obj.GetType());
                self = new CLRObject(obj, type);

                // set __pyobj__ to self and deref the python object which will allow this
                // object to be collected.
                FieldInfo fi = obj.GetType().GetField("__pyobj__");
                fi.SetValue(obj, self);

                Runtime.Incref(self.pyHandle);
                PyObject pyself = new PyObject(self.pyHandle);
                disposeList.Add(pyself);

                Runtime.Incref(Runtime.PyNone);
                PyObject pynone = new PyObject(Runtime.PyNone);
                disposeList.Add(pynone);

                // call __init__
                PyObject init = pyself.GetAttr("__init__", pynone);
                disposeList.Add(init);
                if (init.Handle != Runtime.PyNone)
                {
                    // if __init__ hasn't been overriden then it will be a managed object
                    ManagedType managedMethod = ManagedType.GetManagedObject(init.Handle);
                    if (null == managedMethod)
                    {
                        PyObject[] pyargs = new PyObject[args.Length];
                        for (int i = 0; i < args.Length; ++i)
                        {
                            pyargs[i] = new PyObject(Converter.ToPython(args[i], args[i].GetType()));
                            disposeList.Add(pyargs[i]);
                        }

                        disposeList.Add(init.Invoke(pyargs));
                    }
                }
            }
            finally
            {
                foreach (PyObject x in disposeList) {
                    if (x != null)
                        x.Dispose();
                }

                // Decrement the python object's reference count.
                // This doesn't actually destroy the object, it just sets the reference to this object
                // to be a weak reference and it will be destroyed when the C# object is destroyed.
                if (null != self)
                    Runtime.Decref(self.pyHandle);

                Runtime.PyGILState_Release(gs);
            }
        }

        public static void Finalize(IPythonDerivedType obj)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            CLRObject self = (CLRObject)fi.GetValue(obj);

            // If python's been terminated then just free the gchandle.
            lock (Runtime.IsFinalizingLock)
            {
                if (0 == Runtime.Py_IsInitialized() || Runtime.IsFinalizing)
                {
                    self.gcHandle.Free();
                    return;
                }
            }

            // delete the python object in an asnyc task as we may not be able to acquire
            // the GIL immediately and we don't want to block the GC thread.
            var t = Task.Factory.StartNew(() =>
            {
                lock (Runtime.IsFinalizingLock)
                {
                    // If python's been terminated then just free the gchandle.
                    if (0 == Runtime.Py_IsInitialized() || Runtime.IsFinalizing)
                    {
                        self.gcHandle.Free();
                        return;
                    }

                    IntPtr gs = Runtime.PyGILState_Ensure();
                    try
                    {
                        // the C# object is being destroyed which must mean there are no more
                        // references to the Python object as well so now we can dealloc the
                        // python object.
                        IntPtr dict = Marshal.ReadIntPtr(self.pyHandle, ObjectOffset.DictOffset(self.pyHandle));
                        if (dict != IntPtr.Zero)
                            Runtime.Decref(dict);
                        Runtime.PyObject_GC_Del(self.pyHandle);
                        self.gcHandle.Free();
                    }
                    finally
                    {
                        Runtime.PyGILState_Release(gs);
                    }
                }
            });
        }
    }
}
