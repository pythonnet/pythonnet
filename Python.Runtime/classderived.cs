using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
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
    /// <remarks>
    /// interface used to identify which C# types were dynamically created as python subclasses
    /// </remarks>
    public interface IPythonDerivedType
    {
    }

    internal class ClassDerivedObject : ClassObject
    {
        private static Dictionary<string, AssemblyBuilder> assemblyBuilders;
        private static Dictionary<Tuple<string, string>, ModuleBuilder> moduleBuilders;

        static ClassDerivedObject()
        {
            assemblyBuilders = new Dictionary<string, AssemblyBuilder>();
            moduleBuilders = new Dictionary<Tuple<string, string>, ModuleBuilder>();
        }

        public static void Reset()
        {
            assemblyBuilders = new Dictionary<string, AssemblyBuilder>();
            moduleBuilders = new Dictionary<Tuple<string, string>, ModuleBuilder>();
        }

        internal ClassDerivedObject(Type tp) : base(tp)
        {
        }

        /// <summary>
        /// Implements __new__ for derived classes of reflected classes.
        /// </summary>
        public new static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw)
        {
            var cls = GetManagedObject(tp) as ClassDerivedObject;

            // call the managed constructor
            object obj = cls.binder.InvokeRaw(IntPtr.Zero, args, kw);
            if (obj == null)
            {
                return IntPtr.Zero;
            }

            // return the pointer to the python object
            // (this indirectly calls ClassDerivedObject.ToPython)
            return Converter.ToPython(obj, cls.GetType());
        }

        public new static void tp_dealloc(IntPtr ob)
        {
            var self = (CLRObject)GetManagedObject(ob);

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

        /// <summary>
        /// Called from Converter.ToPython for types that are python subclasses of managed types.
        /// The referenced python object is returned instead of a new wrapper.
        /// </summary>
        internal static IntPtr ToPython(IPythonDerivedType obj)
        {
            // derived types have a __pyobj__ field that gets set to the python
            // object in the overridden constructor
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);

            Runtime.XIncref(self.pyHandle);

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

        /// <summary>
        /// Creates a new managed type derived from a base type with any virtual
        /// methods overridden to call out to python if the associated python
        /// object has overridden the method.
        /// </summary>
        internal static Type CreateDerivedType(string name,
            Type baseType,
            IntPtr py_dict,
            string namespaceStr,
            string assemblyName,
            string moduleName = "Python.Runtime.Dynamic.dll")
        {
            if (null != namespaceStr)
            {
                name = namespaceStr + "." + name;
            }

            if (null == assemblyName)
            {
                assemblyName = Assembly.GetExecutingAssembly().FullName;
            }

            ModuleBuilder moduleBuilder = GetModuleBuilder(assemblyName, moduleName);

            Type baseClass = baseType;
            var interfaces = new List<Type> { typeof(IPythonDerivedType) };

            // if the base type is an interface then use System.Object as the base class
            // and add the base type to the list of interfaces this new class will implement.
            if (baseType.IsInterface)
            {
                interfaces.Add(baseType);
                baseClass = typeof(object);
            }

            TypeBuilder typeBuilder = moduleBuilder.DefineType(name,
                TypeAttributes.Public | TypeAttributes.Class,
                baseClass,
                interfaces.ToArray());

            // add a field for storing the python object pointer
            // FIXME: fb not used
            FieldBuilder fb = typeBuilder.DefineField("__pyobj__", typeof(CLRObject), FieldAttributes.Public);

            // override any constructors
            ConstructorInfo[] constructors = baseClass.GetConstructors();
            foreach (ConstructorInfo ctor in constructors)
            {
                AddConstructor(ctor, baseType, typeBuilder);
            }

            // Override any properties explicitly overridden in python
            var pyProperties = new HashSet<string>();
            if (py_dict != IntPtr.Zero && Runtime.PyDict_Check(py_dict))
            {
                Runtime.XIncref(py_dict);
                using (var dict = new PyDict(py_dict))
                using (PyObject keys = dict.Keys())
                {
                    foreach (PyObject pyKey in keys)
                    {
                        using (PyObject value = dict[pyKey])
                        {
                            if (value.HasAttr("_clr_property_type_"))
                            {
                                string propertyName = pyKey.ToString();
                                pyProperties.Add(propertyName);

                                // Add the property to the type
                                AddPythonProperty(propertyName, value, typeBuilder);
                            }
                        }
                    }
                }
            }

            // override any virtual methods not already overridden by the properties above
            MethodInfo[] methods = baseType.GetMethods();
            var virtualMethods = new HashSet<string>();
            foreach (MethodInfo method in methods)
            {
                if (!method.Attributes.HasFlag(MethodAttributes.Virtual) |
                    method.Attributes.HasFlag(MethodAttributes.Final))
                {
                    continue;
                }

                // skip if this property has already been overridden
                if ((method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
                    && pyProperties.Contains(method.Name.Substring(4)))
                {
                    continue;
                }

                // keep track of the virtual methods redirected to the python instance
                virtualMethods.Add(method.Name);

                // override the virtual method to call out to the python method, if there is one.
                AddVirtualMethod(method, baseType, typeBuilder);
            }

            // Add any additional methods and properties explicitly exposed from Python.
            if (py_dict != IntPtr.Zero && Runtime.PyDict_Check(py_dict))
            {
                Runtime.XIncref(py_dict);
                using (var dict = new PyDict(py_dict))
                using (PyObject keys = dict.Keys())
                {
                    foreach (PyObject pyKey in keys)
                    {
                        using (PyObject value = dict[pyKey])
                        {
                            if (value.HasAttr("_clr_return_type_") && value.HasAttr("_clr_arg_types_"))
                            {
                                string methodName = pyKey.ToString();

                                // if this method has already been redirected to the python method skip it
                                if (virtualMethods.Contains(methodName))
                                {
                                    continue;
                                }

                                // Add the method to the type
                                AddPythonMethod(methodName, value, typeBuilder);
                            }
                        }
                    }
                }
            }

            // add the destructor so the python object created in the constructor gets destroyed
            MethodBuilder methodBuilder = typeBuilder.DefineMethod("Finalize",
                MethodAttributes.Family |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                CallingConventions.Standard,
                typeof(void),
                Type.EmptyTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("Finalize"));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseClass.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance));
            il.Emit(OpCodes.Ret);

            Type type = typeBuilder.CreateType();

            // scan the assembly so the newly added class can be imported
            Assembly assembly = Assembly.GetAssembly(type);
            AssemblyManager.ScanAssembly(assembly);

            // FIXME: assemblyBuilder not used
            AssemblyBuilder assemblyBuilder = assemblyBuilders[assemblyName];

            return type;
        }

        /// <summary>
        /// Add a constructor override that calls the python ctor after calling the base type constructor.
        /// </summary>
        /// <param name="ctor">constructor to be called before calling the python ctor</param>
        /// <param name="baseType">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the ctor is to be added to</param>
        private static void AddConstructor(ConstructorInfo ctor, Type baseType, TypeBuilder typeBuilder)
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

            // create a method for calling the original constructor
            string baseCtorName = "_" + baseType.Name + "__cinit__";
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(baseCtorName,
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig,
                typeof(void),
                parameterTypes);

            // emit the assembly for calling the original method using call instead of callvirt
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }
            il.Emit(OpCodes.Call, ctor);
            il.Emit(OpCodes.Ret);

            // override the original method with a new one that dispatches to python
            ConstructorBuilder cb = typeBuilder.DefineConstructor(MethodAttributes.Public |
                                                                  MethodAttributes.ReuseSlot |
                                                                  MethodAttributes.HideBySig,
                ctor.CallingConvention,
                parameterTypes);
            il = cb.GetILGenerator();
            il.DeclareLocal(typeof(object[]));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, baseCtorName);
            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameterTypes[i].IsValueType)
                {
                    il.Emit(OpCodes.Box, parameterTypes[i]);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeCtor"));
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Add a virtual method override that checks for an override on the python instance
        /// and calls it, otherwise fall back to the base class method.
        /// </summary>
        /// <param name="method">virtual method to be overridden</param>
        /// <param name="baseType">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method is to be added to</param>
        private static void AddVirtualMethod(MethodInfo method, Type baseType, TypeBuilder typeBuilder)
        {
            ParameterInfo[] parameters = method.GetParameters();
            Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

            // If the method isn't abstract create a method for calling the original method
            string baseMethodName = null;
            if (!method.IsAbstract)
            {
                baseMethodName = "_" + baseType.Name + "__" + method.Name;
                MethodBuilder baseMethodBuilder = typeBuilder.DefineMethod(baseMethodName,
                    MethodAttributes.Public |
                    MethodAttributes.Final |
                    MethodAttributes.HideBySig,
                    method.ReturnType,
                    parameterTypes);

                // emit the assembly for calling the original method using call instead of callvirt
                ILGenerator baseIl = baseMethodBuilder.GetILGenerator();
                baseIl.Emit(OpCodes.Ldarg_0);
                for (var i = 0; i < parameters.Length; ++i)
                {
                    baseIl.Emit(OpCodes.Ldarg, i + 1);
                }
                baseIl.Emit(OpCodes.Call, method);
                baseIl.Emit(OpCodes.Ret);
            }

            // override the original method with a new one that dispatches to python
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name,
                MethodAttributes.Public |
                MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                method.CallingConvention,
                method.ReturnType,
                parameterTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            il.DeclareLocal(typeof(object[]));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, method.Name);

            // don't fall back to the base type's method if it's abstract
            if (null != baseMethodName)
            {
                il.Emit(OpCodes.Ldstr, baseMethodName);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameterTypes[i].IsValueType)
                {
                    il.Emit(OpCodes.Box, parameterTypes[i]);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }
            il.Emit(OpCodes.Ldloc_0);
            if (method.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeMethodVoid"));
            }
            else
            {
                il.Emit(OpCodes.Call,
                    typeof(PythonDerivedType).GetMethod("InvokeMethod").MakeGenericMethod(method.ReturnType));
            }
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Python method may have the following function attributes set to control how they're exposed:
        /// - _clr_return_type_    - method return type (required)
        /// - _clr_arg_types_      - list of method argument types (required)
        /// - _clr_method_name_    - method name, if different from the python method name (optional)
        /// </summary>
        /// <param name="methodName">Method name to add to the type</param>
        /// <param name="func">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method/property is to be added to</param>
        private static void AddPythonMethod(string methodName, PyObject func, TypeBuilder typeBuilder)
        {
            if (func.HasAttr("_clr_method_name_"))
            {
                using (PyObject pyMethodName = func.GetAttr("_clr_method_name_"))
                {
                    methodName = pyMethodName.ToString();
                }
            }

            using (PyObject pyReturnType = func.GetAttr("_clr_return_type_"))
            using (PyObject pyArgTypes = func.GetAttr("_clr_arg_types_"))
            {
                var returnType = pyReturnType.AsManagedObject(typeof(Type)) as Type;
                if (returnType == null)
                {
                    returnType = typeof(void);
                }

                if (!pyArgTypes.IsIterable())
                {
                    throw new ArgumentException("_clr_arg_types_ must be a list or tuple of CLR types");
                }

                var argTypes = new List<Type>();
                foreach (PyObject pyArgType in pyArgTypes)
                {
                    var argType = pyArgType.AsManagedObject(typeof(Type)) as Type;
                    if (argType == null)
                    {
                        throw new ArgumentException("_clr_arg_types_ must be a list or tuple of CLR types");
                    }
                    argTypes.Add(argType);
                }

                // add the method to call back into python
                MethodAttributes methodAttribs = MethodAttributes.Public |
                                                 MethodAttributes.Virtual |
                                                 MethodAttributes.ReuseSlot |
                                                 MethodAttributes.HideBySig;

                MethodBuilder methodBuilder = typeBuilder.DefineMethod(methodName,
                    methodAttribs,
                    returnType,
                    argTypes.ToArray());

                ILGenerator il = methodBuilder.GetILGenerator();
                il.DeclareLocal(typeof(object[]));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, methodName);
                il.Emit(OpCodes.Ldnull); // don't fall back to the base type's method
                il.Emit(OpCodes.Ldc_I4, argTypes.Count);
                il.Emit(OpCodes.Newarr, typeof(object));
                il.Emit(OpCodes.Stloc_0);
                for (var i = 0; i < argTypes.Count; ++i)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, i + 1);
                    if (argTypes[i].IsValueType)
                    {
                        il.Emit(OpCodes.Box, argTypes[i]);
                    }
                    il.Emit(OpCodes.Stelem, typeof(object));
                }
                il.Emit(OpCodes.Ldloc_0);
                if (returnType == typeof(void))
                {
                    il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod("InvokeMethodVoid"));
                }
                else
                {
                    il.Emit(OpCodes.Call,
                        typeof(PythonDerivedType).GetMethod("InvokeMethod").MakeGenericMethod(returnType));
                }
                il.Emit(OpCodes.Ret);
            }
        }

        /// <summary>
        /// Python properties may have the following function attributes set to control how they're exposed:
        /// - _clr_property_type_     - property type (required)
        /// </summary>
        /// <param name="propertyName">Property name to add to the type</param>
        /// <param name="func">Python property object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the method/property is to be added to</param>
        private static void AddPythonProperty(string propertyName, PyObject func, TypeBuilder typeBuilder)
        {
            // add the method to call back into python
            MethodAttributes methodAttribs = MethodAttributes.Public |
                                             MethodAttributes.Virtual |
                                             MethodAttributes.ReuseSlot |
                                             MethodAttributes.HideBySig |
                                             MethodAttributes.SpecialName;

            using (PyObject pyPropertyType = func.GetAttr("_clr_property_type_"))
            {
                var propertyType = pyPropertyType.AsManagedObject(typeof(Type)) as Type;
                if (propertyType == null)
                {
                    throw new ArgumentException("_clr_property_type must be a CLR type");
                }

                PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName,
                    PropertyAttributes.None,
                    propertyType,
                    null);

                if (func.HasAttr("fget"))
                {
                    using (PyObject pyfget = func.GetAttr("fget"))
                    {
                        if (pyfget.IsTrue())
                        {
                            MethodBuilder methodBuilder = typeBuilder.DefineMethod("get_" + propertyName,
                                methodAttribs,
                                propertyType,
                                null);

                            ILGenerator il = methodBuilder.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldstr, propertyName);
                            il.Emit(OpCodes.Call,
                                typeof(PythonDerivedType).GetMethod("InvokeGetProperty").MakeGenericMethod(propertyType));
                            il.Emit(OpCodes.Ret);

                            propertyBuilder.SetGetMethod(methodBuilder);
                        }
                    }
                }

                if (func.HasAttr("fset"))
                {
                    using (PyObject pyset = func.GetAttr("fset"))
                    {
                        if (pyset.IsTrue())
                        {
                            MethodBuilder methodBuilder = typeBuilder.DefineMethod("set_" + propertyName,
                                methodAttribs,
                                null,
                                new[] { propertyType });

                            ILGenerator il = methodBuilder.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldstr, propertyName);
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Call,
                                typeof(PythonDerivedType).GetMethod("InvokeSetProperty").MakeGenericMethod(propertyType));
                            il.Emit(OpCodes.Ret);

                            propertyBuilder.SetSetMethod(methodBuilder);
                        }
                    }
                }
            }
        }

        private static ModuleBuilder GetModuleBuilder(string assemblyName, string moduleName)
        {
            // find or create a dynamic assembly and module
            AppDomain domain = AppDomain.CurrentDomain;
            ModuleBuilder moduleBuilder;

            if (moduleBuilders.ContainsKey(Tuple.Create(assemblyName, moduleName)))
            {
                moduleBuilder = moduleBuilders[Tuple.Create(assemblyName, moduleName)];
            }
            else
            {
                AssemblyBuilder assemblyBuilder;
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

    /// <summary>
    /// PythonDerivedType contains static methods used by the dynamically created
    /// derived type that allow it to call back into python from overridden virtual
    /// methods, and also handle the construction and destruction of the python
    /// object.
    /// </summary>
    /// <remarks>
    /// This has to be public as it's called from methods on dynamically built classes
    /// potentially in other assemblies.
    /// </remarks>
    public class PythonDerivedType
    {
        /// <summary>
        /// This is the implementation of the overridden methods in the derived
        /// type. It looks for a python method with the same name as the method
        /// on the managed base class and if it exists and isn't the managed
        /// method binding (i.e. it has been overridden in the derived python
        /// class) it calls it, otherwise it calls the base method.
        /// </summary>
        public static T InvokeMethod<T>(IPythonDerivedType obj, string methodName, string origMethodName, object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);

            if (null != self)
            {
                var disposeList = new List<PyObject>();
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    Runtime.XIncref(self.pyHandle);
                    var pyself = new PyObject(self.pyHandle);
                    disposeList.Add(pyself);

                    Runtime.XIncref(Runtime.PyNone);
                    var pynone = new PyObject(Runtime.PyNone);
                    disposeList.Add(pynone);

                    PyObject method = pyself.GetAttr(methodName, pynone);
                    disposeList.Add(method);
                    if (method.Handle != Runtime.PyNone)
                    {
                        // if the method hasn't been overridden then it will be a managed object
                        ManagedType managedMethod = ManagedType.GetManagedObject(method.Handle);
                        if (null == managedMethod)
                        {
                            var pyargs = new PyObject[args.Length];
                            for (var i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = new PyObject(Converter.ToPythonImplicit(args[i]));
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
                    foreach (PyObject x in disposeList)
                    {
                        x?.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            if (origMethodName == null)
            {
                throw new NotImplementedException("Python object does not have a '" + methodName + "' method");
            }

            return (T)obj.GetType().InvokeMember(origMethodName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        public static void InvokeMethodVoid(IPythonDerivedType obj, string methodName, string origMethodName,
            object[] args)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);
            if (null != self)
            {
                var disposeList = new List<PyObject>();
                IntPtr gs = Runtime.PyGILState_Ensure();
                try
                {
                    Runtime.XIncref(self.pyHandle);
                    var pyself = new PyObject(self.pyHandle);
                    disposeList.Add(pyself);

                    Runtime.XIncref(Runtime.PyNone);
                    var pynone = new PyObject(Runtime.PyNone);
                    disposeList.Add(pynone);

                    PyObject method = pyself.GetAttr(methodName, pynone);
                    disposeList.Add(method);
                    if (method.Handle != Runtime.PyNone)
                    {
                        // if the method hasn't been overridden then it will be a managed object
                        ManagedType managedMethod = ManagedType.GetManagedObject(method.Handle);
                        if (null == managedMethod)
                        {
                            var pyargs = new PyObject[args.Length];
                            for (var i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = new PyObject(Converter.ToPythonImplicit(args[i]));
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
                    foreach (PyObject x in disposeList)
                    {
                        x?.Dispose();
                    }
                    Runtime.PyGILState_Release(gs);
                }
            }

            if (origMethodName == null)
            {
                throw new NotImplementedException($"Python object does not have a '{methodName}' method");
            }

            obj.GetType().InvokeMember(origMethodName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        public static T InvokeGetProperty<T>(IPythonDerivedType obj, string propertyName)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);

            if (null == self)
            {
                throw new NullReferenceException("Instance must be specified when getting a property");
            }

            IntPtr gs = Runtime.PyGILState_Ensure();
            try
            {
                Runtime.XIncref(self.pyHandle);
                using (var pyself = new PyObject(self.pyHandle))
                using (PyObject pyvalue = pyself.GetAttr(propertyName))
                {
                    return (T)pyvalue.AsManagedObject(typeof(T));
                }
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeSetProperty<T>(IPythonDerivedType obj, string propertyName, T value)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);

            if (null == self)
            {
                throw new NullReferenceException("Instance must be specified when setting a property");
            }

            IntPtr gs = Runtime.PyGILState_Ensure();
            try
            {
                Runtime.XIncref(self.pyHandle);
                using (var pyself = new PyObject(self.pyHandle))
                using (var pyvalue = new PyObject(Converter.ToPythonImplicit(value)))
                {
                    pyself.SetAttr(propertyName, pyvalue);
                }
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeCtor(IPythonDerivedType obj, string origCtorName, object[] args)
        {
            // call the base constructor
            obj.GetType().InvokeMember(origCtorName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);

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
            }
            finally
            {
                // Decrement the python object's reference count.
                // This doesn't actually destroy the object, it just sets the reference to this object
                // to be a weak reference and it will be destroyed when the C# object is destroyed.
                if (null != self)
                {
                    Runtime.XDecref(self.pyHandle);
                }

                Runtime.PyGILState_Release(gs);
            }
        }

        public static void Finalize(IPythonDerivedType obj)
        {
            FieldInfo fi = obj.GetType().GetField("__pyobj__");
            var self = (CLRObject)fi.GetValue(obj);

            // If python's been terminated then just free the gchandle.
            lock (Runtime.IsFinalizingLock)
            {
                if (0 == Runtime.Py_IsInitialized() || Runtime.IsFinalizing)
                {
                    self.gcHandle.Free();
                    return;
                }
            }

            // delete the python object in an async task as we may not be able to acquire
            // the GIL immediately and we don't want to block the GC thread.
            // FIXME: t isn't used
            Task t = Task.Factory.StartNew(() =>
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
                        {
                            Runtime.XDecref(dict);
                        }
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
