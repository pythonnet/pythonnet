using System;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use
using static Python.Runtime.PythonDerivedType;
#pragma warning restore CS0618 // Type or member is obsolete

namespace Python.Runtime
{
    /// <summary>
    /// Attribute to mark dynamically created method, that directly calls into
    /// the base class implementation of that method
    /// </summary>
    public class OriginalMethod : Attribute { }

    /// <summary>
    /// Attribute to mark dynamically created method, that looks up any possible
    /// method overrides on the associated python object instance and jumps into
    /// that method if available (as virtual methods do). Otherwise it will call
    /// into the base class implementation instead
    /// </summary>
    public class RedirectedMethod : Attribute { }

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

    [Serializable]
    internal class ClassDerivedObject : ClassObject
    {
        static readonly BindingFlags s_flags = BindingFlags.Instance |
                                               BindingFlags.Public |
                                               BindingFlags.NonPublic |
                                               BindingFlags.GetProperty |
                                               BindingFlags.SetProperty;

        private static Dictionary<string, AssemblyBuilder> assemblyBuilders;
        private static Dictionary<Tuple<string, string>, ModuleBuilder> moduleBuilders;

        /// <summary>
        /// Cache stores generated derived types. An instance of these types
        /// holds a reference to the python object instance and dynamically
        /// looks up attributes, so they can be reused when python class is
        /// modified on the python side.
        /// </summary>
        static Dictionary<string, Type> cache = new Dictionary<string, Type>();

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

        protected override NewReference NewObjectToPython(object obj, BorrowedReference tp)
        {
            var self = base.NewObjectToPython(obj, tp);

            SetPyObj((IPythonDerivedType)obj, self.Borrow());

            // Decrement the python object's reference count.
            // This doesn't actually destroy the object, it just sets the reference to this object
            // to be a weak reference and it will be destroyed when the C# object is destroyed.
            Runtime.XDecref(self.Steal());

            return Converter.ToPython(obj, type.Value);
        }

        public override void InitializeSlots(BorrowedReference pyType, SlotsHolder slotsHolder)
        {
            base.InitializeSlots(pyType, slotsHolder);

            if (indexer is not null)
            {
                if (indexer.CanGet)
                {
                    TypeManager.InitializeSlot(pyType, TypeOffset.mp_subscript, new Interop.BB_N(mp_subscript_impl), slotsHolder);
                }
                if (indexer.CanSet)
                {
                    TypeManager.InitializeSlot(pyType, TypeOffset.mp_ass_subscript, new Interop.BBB_I32(mp_ass_subscript_impl), slotsHolder);
                }
            }
        }

        static NewReference mp_subscript_impl(BorrowedReference ob, BorrowedReference idx)
            => Substrict(ob, idx, allowRedirected: true);

        static int mp_ass_subscript_impl(BorrowedReference ob, BorrowedReference idx, BorrowedReference v)
            => Ass_Substrict(ob, idx, v, allowRedirected: true);

        public new static void tp_dealloc(NewReference ob)
        {
            var self = (CLRObject?)GetManagedObject(ob.Borrow());

            // don't let the python GC destroy this object
            Runtime.PyObject_GC_UnTrack(ob.Borrow());

            // self may be null after Shutdown begun
            if (self is not null)
            {
                // The python should now have a ref count of 0, but we don't actually want to
                // deallocate the object until the C# object that references it is destroyed.
                // So we don't call PyObject_GC_Del here and instead we set the python
                // reference to a weak reference so that the C# object can be collected.
                GCHandle oldHandle = GetGCHandle(ob.Borrow());
                GCHandle gc = GCHandle.Alloc(self, GCHandleType.Weak);
                SetGCHandle(ob.Borrow(), gc);
                oldHandle.Free();
            }
        }

        /// <summary>
        /// No-op clear. Real cleanup happens in <seealso cref="Finalize(IntPtr)"/>
        /// </summary>
        public new static int tp_clear(BorrowedReference ob) => 0;

        /// <summary>
        /// Called from Converter.ToPython for types that are python subclasses of managed types.
        /// The referenced python object is returned instead of a new wrapper.
        /// </summary>
        internal static NewReference ToPython(IPythonDerivedType obj)
        {
            // derived types have a __pyobj__ field that gets set to the python
            // object in the overridden constructor
            BorrowedReference self;
            try
            {
                self = GetPyObj(obj).CheckRun();
            }
            catch (RuntimeShutdownException e)
            {
                Exceptions.SetError(e);
                return default;
            }

            var result = new NewReference(self);

            // when the C# constructor creates the python object it starts as a weak
            // reference with a reference count of 0. Now we're passing this object
            // to Python the reference count needs to be incremented and the reference
            // needs to be replaced with a strong reference to stop the C# object being
            // collected while Python still has a reference to it.
            if (Runtime.Refcount(self) == 1)
            {
                Runtime._Py_NewReference(self);
                GCHandle weak = GetGCHandle(self);
                var clrObject = GetManagedObject(self);
                GCHandle gc = GCHandle.Alloc(clrObject, GCHandleType.Normal);
                SetGCHandle(self, gc);
                weak.Free();

                // now the object has a python reference it's safe for the python GC to track it
                Runtime.PyObject_GC_Track(self);
            }

            return result;
        }

        /// <summary>
        /// Creates a new managed type derived from a base type with any virtual
        /// methods overridden to call out to python if the associated python
        /// object has overridden the method.
        /// </summary>
        internal static Type CreateDerivedType(string typeName,
            Type baseType,
            IEnumerable<Type> interfaces,
            BorrowedReference pyDict,
            string? namespaceName,
            string? assemblyName,
            string moduleName = "Python.Runtime.Dynamic.dll")
        {
            assemblyName = assemblyName ?? "Python.Runtime.Dynamic";

            // if we have already created a derived type, return that
            // this avoids exceptions when a script defining a type within assembly.namespace
            // is executed more that once. we just keep using the same type created before
            // since the dotnet implementation of that type does not change during runtime
            typeName = CreateUniqueTypeName(namespaceName, typeName, baseType, interfaces);
            if (cache.TryGetValue(typeName, out Type derivedType))
                return derivedType;

            ModuleBuilder moduleBuilder = GetModuleBuilder(assemblyName, moduleName);

            var baseInterfaces = new HashSet<Type> { typeof(IPythonDerivedType) };
            baseInterfaces.UnionWith(interfaces);

            // __clr_abstract__ is used to create an abstract class.
            bool isAbstract = false;
            if (pyDict != null && Runtime.PyDict_Check(pyDict))
            {
                using var dict = new PyDict(pyDict);
                if (dict.HasKey("__clr_abstract__"))
                    isAbstract = true;
            }

            TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName,
                TypeAttributes.Public | TypeAttributes.Class | (isAbstract ? TypeAttributes.Abstract : 0),
                baseType,
                baseInterfaces.ToArray());

            if (baseType.GetField(PyObjName, PyObjFlags) == null)
            {
                // FIXME: fb not used
                FieldBuilder fb = typeBuilder.DefineField(PyObjName,
#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use.
                    typeof(UnsafeReferenceWithRun),
#pragma warning restore CS0618 // Type or member is obsolete
                    FieldAttributes.Public);
            }

            // override any constructors
            ConstructorInfo[] constructors = baseType.GetConstructors(s_flags);
            if (constructors.Any())
            {
                foreach (ConstructorInfo ctor in constructors)
                {
                    if (IsMethod<OriginalMethod>(ctor) || IsMethod<RedirectedMethod>(ctor))
                        continue;

                    AddConstructor(ctor, baseType, typeBuilder);
                }
            }
            else
                AddConstructor(null, baseType, typeBuilder);


            // Override any properties explicitly overridden in python
            var pyProperties = new HashSet<string>();
            if (pyDict != null && Runtime.PyDict_Check(pyDict))
            {
                using var dict = new PyDict(pyDict);
                using var keys = dict.Keys();
                foreach (PyObject pyKey in keys)
                {
                    using var value = dict[pyKey];
                    if (value.HasAttr("_clr_property_type_"))
                    {
                        string propertyName = pyKey.ToString()!;
                        pyProperties.Add(propertyName);

                        // Add the property to the type
                        AddPythonProperty(propertyName, value, typeBuilder);
                    }
                    pyKey.Dispose();
                }
            }

            // override any virtual methods not already overridden by the properties above
            var redirectedMethods = new HashSet<string>();
            var virtualMethods = baseType.GetMethods(s_flags)
                                         .Where(m =>
                                         {
                                             // skip if this property has already been overridden
                                             bool alreadyOverriden =
                                                 ((m.Name.StartsWith("get_") || m.Name.StartsWith("set_"))
                                                     && pyProperties.Contains(m.Name.Substring(4)));

                                             return !alreadyOverriden
                                                && !m.IsPrivate
                                                && !m.IsAssembly
                                                && m.Attributes.HasFlag(MethodAttributes.Virtual)
                                                    && !m.Attributes.HasFlag(MethodAttributes.Final)
                                                    // overriding generic virtual methods is not supported
                                                    // so a call to that should be deferred to the base class method.
                                                    && !m.IsGenericMethod
                                                    && !(IsMethod<OriginalMethod>(m) || IsMethod<RedirectedMethod>(m));
                                         })
                                        .Concat(baseInterfaces.SelectMany(x => x.GetMethods()))
                                        .ToList();
            foreach (MethodInfo method in virtualMethods)
            {
                // override the virtual method to call out to the python method, if there is one.
                AddVirtualMethod(method, baseType, typeBuilder);
                redirectedMethods.Add(method.Name);
            }

            // Add any additional methods and properties explicitly exposed from Python.
            if (pyDict != null && Runtime.PyDict_Check(pyDict))
            {
                using var dict = new PyDict(pyDict);

                using var keys = dict.Keys();
                foreach (PyObject pyKey in keys)
                {
                    using var value = dict[pyKey];
                    if (value.HasAttr("_clr_return_type_") && value.HasAttr("_clr_arg_types_"))
                    {
                        string methodName = pyKey.ToString()!;

                        // if this method has already been redirected to the python method skip it
                        if (redirectedMethods.Contains(methodName))
                        {
                            continue;
                        }

                        // Add the method to the type
                        AddPythonMethod(methodName, value, typeBuilder);
                    }
                    pyKey.Dispose();
                }
            }

            // add the destructor so the python object created in the constructor gets destroyed
            if (baseType.GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance) is MethodInfo finalizeMethod)
            {
                MethodBuilder methodBuilder = typeBuilder.DefineMethod("Finalize",
                    MethodAttributes.Family |
                    MethodAttributes.Virtual |
                    MethodAttributes.HideBySig,
                    CallingConventions.Standard,
                    typeof(void),
                    Type.EmptyTypes);
                ILGenerator il = methodBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(PyFinalize)));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, finalizeMethod);
                il.Emit(OpCodes.Ret);
            }

            Type type = typeBuilder.CreateType();


            // scan the assembly so the newly added class can be imported
            Assembly assembly = Assembly.GetAssembly(type);
            AssemblyManager.ScanAssembly(assembly);

            // FIXME: assemblyBuilder not used
            AssemblyBuilder assemblyBuilder = assemblyBuilders[assemblyName];

            cache[typeName] = type;
            return type;
        }

        /// <summary>
        /// Create a unique type name for the derived class
        /// Current implementation creates unique ids like:
        /// Python.Runtime.Dynamic.BaseClass__BaseInterface1__BaseInterface2__main__SubClass
        /// </summary>
        private static string CreateUniqueTypeName(string? namespaceName, string typeName, Type baseType, IEnumerable<Type> interfaces)
        {
            var sb = new StringBuilder();
            if (namespaceName != null)
                sb.Append(namespaceName + ".");
            sb.Append($"{baseType.FullName}");
            foreach (Type i in interfaces)
                sb.Append($"__{i.FullName}");
            sb.Append($"__{typeName}");
            return sb.ToString();
        }

        /// <summary>
        /// Create name for derived constructor
        /// </summary>
        internal static string CreateDerivedCtorName(Type type) => $"_{type.Name}__cinit__";

        /// <summary>
        /// Create name for derived virtual method of a specific type
        /// </summary>
        internal static string CreateDerivedVirtualName(Type type, string name) => $"_{type.Name}__{name}";

        /// <summary>
        /// Create name for derived virtual method
        /// </summary>
        internal static string CreateDerivedVirtualName(string name) => $"_BASEVIRTUAL__{name}";

        /// <summary>
        /// Add a constructor override that calls the python ctor after calling the base type constructor.
        /// </summary>
        /// <param name="ctor">constructor to be called before calling the python ctor. This can be null if there is no constructor. </param>
        /// <param name="baseType">Python callable object</param>
        /// <param name="typeBuilder">TypeBuilder for the new type the ctor is to be added to</param>
        private static void AddConstructor(ConstructorInfo? ctor, Type baseType, TypeBuilder typeBuilder)
        {
            ParameterInfo[] parameters = ctor?.GetParameters() ?? Array.Empty<ParameterInfo>();
            Type[] parameterTypes = (from param in parameters select param.ParameterType).ToArray();

            // create a method for calling the original constructor
            string baseCtorName = CreateDerivedCtorName(baseType);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(baseCtorName,
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig,
                typeof(void),
                parameterTypes);

            MarkMethodAs<OriginalMethod>(methodBuilder);

            // emit the assembly for calling the original method using call instead of callvirt
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            for (var i = 0; i < parameters.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }
            if (ctor != null)
                il.Emit(OpCodes.Call, ctor);
            il.Emit(OpCodes.Ret);

            // override the original method with a new one that dispatches to python
            ConstructorBuilder cb = typeBuilder.DefineConstructor(MethodAttributes.Public |
                                                                  MethodAttributes.ReuseSlot |
                                                                  MethodAttributes.HideBySig,
                ctor?.CallingConvention ?? CallingConventions.Any,
                parameterTypes);

            MarkMethodAs<RedirectedMethod>(cb);

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
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeCtor)));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
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
            string? baseMethodName = null;
            if (!method.IsAbstract)
            {
                baseMethodName = CreateDerivedVirtualName(method.Name);
                if (baseType.GetMethod(baseMethodName) == null)
                {
                    MethodBuilder baseMethodBuilder = typeBuilder.DefineMethod(baseMethodName,
                        MethodAttributes.Public |
                        MethodAttributes.Final |
                        MethodAttributes.HideBySig,
                        method.ReturnType,
                        parameterTypes);

                    MarkMethodAs<OriginalMethod>(baseMethodBuilder);

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

            MarkMethodAs<RedirectedMethod>(methodBuilder);

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
                var type = parameterTypes[i];
                if (type.IsByRef)
                {
                    type = type.GetElementType();
                    il.Emit(OpCodes.Ldobj, type);
                }
                if (type.IsValueType)
                {
                    il.Emit(OpCodes.Box, type);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }
            il.Emit(OpCodes.Ldloc_0);

            il.Emit(OpCodes.Ldtoken, method);
            il.Emit(OpCodes.Ldtoken, method.DeclaringType);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
            if (method.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeMethodVoid)));
            }
            else
            {
                il.Emit(OpCodes.Call,
                    typeof(PythonDerivedType).GetMethod(nameof(InvokeMethod)).MakeGenericMethod(method.ReturnType));
            }
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
            CodeGenerator.GenerateMarshalByRefsBack(il, parameterTypes);
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
            const string methodNameAttribute = "_clr_method_name_";
            if (func.HasAttr(methodNameAttribute))
            {
                using PyObject pyMethodName = func.GetAttr(methodNameAttribute);
                methodName = pyMethodName.As<string>() ?? throw new ArgumentNullException(methodNameAttribute);
            }

            using var pyReturnType = func.GetAttr("_clr_return_type_");
            using var pyArgTypes = func.GetAttr("_clr_arg_types_");
            using var pyArgTypesIter = PyIter.GetIter(pyArgTypes);
            var returnType = pyReturnType.AsManagedObject(typeof(Type)) as Type;
            if (returnType == null)
            {
                returnType = typeof(void);
            }

            var argTypes = new List<Type>();
            foreach (PyObject pyArgType in pyArgTypesIter)
            {
                var argType = pyArgType.AsManagedObject(typeof(Type)) as Type;
                if (argType == null)
                {
                    throw new ArgumentException("_clr_arg_types_ must be a list or tuple of CLR types");
                }
                argTypes.Add(argType);
                pyArgType.Dispose();
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
            il.DeclareLocal(typeof(RuntimeMethodHandle));
            il.DeclareLocal(typeof(RuntimeTypeHandle));

            // this
            il.Emit(OpCodes.Ldarg_0);

            // Python method to call
            il.Emit(OpCodes.Ldstr, methodName);

            // original method name
            il.Emit(OpCodes.Ldnull); // don't fall back to the base type's method

            // create args array
            il.Emit(OpCodes.Ldc_I4, argTypes.Count);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc_0);

            // fill args array
            for (var i = 0; i < argTypes.Count; ++i)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                var type = argTypes[i];
                if (type.IsByRef)
                {
                    type = type.GetElementType();
                    il.Emit(OpCodes.Ldobj, type);
                }
                if (type.IsValueType)
                {
                    il.Emit(OpCodes.Box, type);
                }
                il.Emit(OpCodes.Stelem, typeof(object));
            }

            // args array
            il.Emit(OpCodes.Ldloc_0);

            // method handle for the base method is null
            il.Emit(OpCodes.Ldloca_S, 1);
            il.Emit(OpCodes.Initobj, typeof(RuntimeMethodHandle));
            il.Emit(OpCodes.Ldloc_1);

            // type handle is also not required
            il.Emit(OpCodes.Ldloca_S, 2);
            il.Emit(OpCodes.Initobj, typeof(RuntimeTypeHandle));
            il.Emit(OpCodes.Ldloc_2);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only

            // invoke the method
            if (returnType == typeof(void))
            {
                il.Emit(OpCodes.Call, typeof(PythonDerivedType).GetMethod(nameof(InvokeMethodVoid)));
            }
            else
            {
                il.Emit(OpCodes.Call,
                    typeof(PythonDerivedType).GetMethod(nameof(InvokeMethod)).MakeGenericMethod(returnType));
            }

            CodeGenerator.GenerateMarshalByRefsBack(il, argTypes);

#pragma warning restore CS0618 // PythonDerivedType is for internal use only
            il.Emit(OpCodes.Ret);
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

            using var pyPropertyType = func.GetAttr("_clr_property_type_");
            var pyNativeType = new PyType(pyPropertyType);
            Converter.ToManaged(pyPropertyType, typeof(Type), out var result, false);
            var propertyType = result as Type;
            string pyTypeName = null;
            string pyTypeModule = null;
            // if the property type is null, we assume that it is a python type
            // and not a C# type, in this case the property is just a PyObject type instead.
            if (propertyType == null)
            {
                propertyType = typeof(PyObject);
                pyTypeModule = pyNativeType.GetAttr("__module__").ToString();
                pyTypeName = pyNativeType.Name;
            }

            PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName,
                PropertyAttributes.None,
                propertyType,
                null);

            if (func.HasAttr("fget"))
            {
                using var pyfget = func.GetAttr("fget");
                if (pyfget.IsTrue())
                {
                    MethodBuilder methodBuilder = typeBuilder.DefineMethod("get_" + propertyName,
                        methodAttribs,
                        propertyType,
                        null);

                    ILGenerator il = methodBuilder.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, propertyName);
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
                    il.Emit(OpCodes.Call,
                        typeof(PythonDerivedType).GetMethod("InvokeGetProperty").MakeGenericMethod(propertyType));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                    il.Emit(OpCodes.Ret);

                    propertyBuilder.SetGetMethod(methodBuilder);
                }
            }

            if (func.HasAttr("fset"))
            {
                using var pyset = func.GetAttr("fset");
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
#pragma warning disable CS0618 // PythonDerivedType is for internal use only
                    il.Emit(OpCodes.Call,
                        typeof(PythonDerivedType).GetMethod("InvokeSetProperty").MakeGenericMethod(propertyType));
#pragma warning restore CS0618 // PythonDerivedType is for internal use only
                    il.Emit(OpCodes.Ret);

                    propertyBuilder.SetSetMethod(methodBuilder);
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

        /// <summary>
        /// Get original method of this method if available on given type
        /// </summary>
        [return: MaybeNull]
        internal static MethodBase GetOriginalMethod(MethodBase method, Type type)
        {
            Type[] types = method.GetParameters().Select(p => p.ParameterType).ToArray();
            if (type.GetMethod(ClassDerivedObject.CreateDerivedVirtualName(method.Name), types) is MethodBase rm
                    && IsMethod<OriginalMethod>(rm))
            {
                return rm;
            }
            return default;
        }

        /// <summary>
        /// Get redirected method of this method if available on given type
        /// </summary>
        [return: MaybeNull]
        internal static MethodBase GetRedirectedMethod(MethodBase method, Type type)
        {
            Type[] types = method.GetParameters().Select(p => p.ParameterType).ToArray();
            if (type.GetMethod(method.Name, types) is MethodBase rm
                    && IsMethod<RedirectedMethod>(rm))
            {
                return rm;
            }
            return default;
        }

        /// <summary>
        /// Check if given method is marked as Attribute T
        /// </summary>
        internal static bool IsMethod<T>(MethodBase method) where T : Attribute => method.GetCustomAttributes<T>().Any();

        /// <summary>
        /// Add Attribute T to given contstructor
        /// </summary>
        private static void MarkMethodAs<T>(ConstructorBuilder ctorBuilder) where T : Attribute
        {
            ConstructorInfo ctorInfo = typeof(T).GetConstructor(Array.Empty<Type>());
            CustomAttributeBuilder cabuilder = new CustomAttributeBuilder(ctorInfo, Array.Empty<object>());
            ctorBuilder.SetCustomAttribute(cabuilder);
        }

        /// <summary>
        /// Add Attribute T to given method
        /// </summary>
        private static void MarkMethodAs<T>(MethodBuilder methodBuilder) where T : Attribute
        {
            ConstructorInfo ctorInfo = typeof(T).GetConstructor(Array.Empty<Type>());
            CustomAttributeBuilder cabuilder = new CustomAttributeBuilder(ctorInfo, Array.Empty<object>());
            methodBuilder.SetCustomAttribute(cabuilder);
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(Util.InternalUseOnly)]
    public class PythonDerivedType
    {
        /// <summary>
        /// Represents Void as a generic tyoe for InvokeMethod
        /// </summary>
        class Void { }

        internal const string PyObjName = "__pyobj__";
        internal const BindingFlags PyObjFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// This is the implementation of the overridden methods in the derived
        /// type. It looks for a python method with the same name as the method
        /// on the managed base class and if it exists and isn't the managed
        /// method binding (i.e. it has been overridden in the derived python
        /// class) it calls it, otherwise it calls the base method and converts
        /// and returns the value return from python or base method
        /// </summary>
        public static T? InvokeMethod<T>(IPythonDerivedType obj, string methodName, string origMethodName,
            object[] args, RuntimeMethodHandle methodHandle, RuntimeTypeHandle declaringTypeHandle)
        {
            var self = GetPyObj(obj);

            if (null != self.Ref)
            {
                var disposeList = new List<PyObject>();
                PyGILState gs = Runtime.PyGILState_Ensure();
                try
                {
                    string pyMethodName;
                    if (!Indexer.TryGetPropertyMethodName(methodName, out pyMethodName))
                        pyMethodName = methodName;

                    using var pyself = new PyObject(self.CheckRun());
                    using PyObject method = pyself.GetAttr(pyMethodName, Runtime.None);
                    BorrowedReference dt = Runtime.PyObject_TYPE(method);
                    if (method.Reference != Runtime.PyNone && dt != Runtime.PyMethodWrapperType)
                    {
                        // if the method hasn't been overridden then it will be a managed object
                        ManagedType? managedMethod = ManagedType.GetManagedObject(method.Reference);
                        if (null == managedMethod)
                        {
                            var pyargs = new PyObject[args.Length];
                            for (var i = 0; i < args.Length; ++i)
                            {
                                pyargs[i] = Converter.ToPythonImplicit(args[i]).MoveToPyObject();
                                disposeList.Add(pyargs[i]);
                            }


                            PyObject py_result = method.Invoke(pyargs);
                            var clrMethod = methodHandle != default
                                ? MethodBase.GetMethodFromHandle(methodHandle, declaringTypeHandle)
                                : null;
                            PyTuple? result_tuple = MarshalByRefsBack(args, clrMethod, py_result, outsOffset: 1);

                            if (typeof(T) == typeof(Void))
                                return default;
                            else
                                return result_tuple is not null ? result_tuple[0].As<T>() : py_result.As<T>();
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

        /// <summary>
        /// This is the implementation of the overridden methods in the derived
        /// type. It looks for a python method with the same name as the method
        /// on the managed base class and if it exists and isn't the managed
        /// method binding (i.e. it has been overridden in the derived python
        /// class) it calls it, otherwise it calls the base method.
        /// </summary>
        public static void InvokeMethodVoid(IPythonDerivedType obj, string methodName, string origMethodName,
            object[] args, RuntimeMethodHandle methodHandle, RuntimeTypeHandle declaringTypeHandle)
        {
            InvokeMethod<Void>(obj, methodName, origMethodName, args, methodHandle, declaringTypeHandle);
        }

        /// <summary>
        /// If the method has byref arguments, reinterprets Python return value
        /// as a tuple of new values for those arguments, and updates corresponding
        /// elements of <paramref name="args"/> array.
        /// </summary>
        private static PyTuple? MarshalByRefsBack(object?[] args, MethodBase? method, PyObject pyResult, int outsOffset)
        {
            if (method is null) return null;

            var parameters = method.GetParameters();
            PyTuple? outs = null;
            int byrefIndex = 0;
            for (int i = 0; i < parameters.Length; ++i)
            {
                Type type = parameters[i].ParameterType;
                if (!type.IsByRef)
                {
                    continue;
                }

                type = type.GetElementType();

                if (outs is null)
                {
                    outs = new PyTuple(pyResult);
                    pyResult.Dispose();
                }

                args[i] = outs[byrefIndex + outsOffset].AsManagedObject(type);
                byrefIndex++;
            }
            if (byrefIndex > 0 && outs!.Length() > byrefIndex + outsOffset)
                throw new ArgumentException("Too many output parameters");

            return outs;
        }

        public static T? InvokeGetProperty<T>(IPythonDerivedType obj, string propertyName)
        {
            var self = GetPyObj(obj);

            if (null == self.Ref)
            {
                throw new NullReferenceException("Instance must be specified when getting a property");
            }

            PyGILState gs = Runtime.PyGILState_Ensure();
            try
            {
                using var pyself = new PyObject(self.CheckRun());
                using var pyvalue = pyself.GetAttr(propertyName);
                return pyvalue.As<T>();
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeSetProperty<T>(IPythonDerivedType obj, string propertyName, T value)
        {
            var self = GetPyObj(obj);

            if (null == self.Ref)
            {
                throw new NullReferenceException("Instance must be specified when setting a property");
            }

            PyGILState gs = Runtime.PyGILState_Ensure();
            try
            {
                using var pyself = new PyObject(self.CheckRun());
                using var pyvalue = Converter.ToPythonImplicit(value).MoveToPyObject();
                pyself.SetAttr(propertyName, pyvalue);
            }
            finally
            {
                Runtime.PyGILState_Release(gs);
            }
        }

        public static void InvokeCtor(IPythonDerivedType obj, string origCtorName, object[] args)
        {
            var selfRef = GetPyObj(obj);
            if (selfRef.Ref == null)
            {
                // this might happen when the object is created from .NET
                var disposeList = new List<PyObject>();
                PyGILState gs = Runtime.PyGILState_Ensure();
                try
                {
                    PyType cc = ReflectedClrType.GetOrCreate(obj.GetType());

                    var pyargs = new PyObject[args.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        pyargs[i] = args[i].ToPython();
                        disposeList.Add(pyargs[i]);
                    }
                    // create an instance of the class and steal the reference.
                    using var newObj = cc.Invoke(pyargs, null);
                    // somehow if this is not done this object never gets a reference count
                    // and things breaks later.
                    // I am not sure what it does though.
                    GCHandle gc = GCHandle.Alloc(obj);
                    // hand over the reference.
                    var py = newObj.NewReferenceOrNull();
                    SetPyObj(obj, py.Borrow());
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

            // call the base constructor
            obj.GetType().InvokeMember(origCtorName,
                BindingFlags.InvokeMethod,
                null,
                obj,
                args);
        }

        public static void PyFinalize(IPythonDerivedType obj)
        {
            // the C# object is being destroyed which must mean there are no more
            // references to the Python object as well
            var self = GetPyObj(obj);
            Finalizer.Instance.AddDerivedFinalizedObject(ref self.RawObj, self.Run);
        }

        internal static void Finalize(IntPtr derived)
        {
            var @ref = NewReference.DangerousFromPointer(derived);

            ClassBase.tp_clear(@ref.Borrow());

            var type = Runtime.PyObject_TYPE(@ref.Borrow());

            if (!Runtime.HostedInPython || Runtime.TypeManagerInitialized)
            {
                // rare case when it's needed
                // matches correspdonging PyObject_GC_UnTrack
                // in ClassDerivedObject.tp_dealloc
                Runtime.PyObject_GC_Del(@ref.Steal());

                // must decref our type
                Runtime.XDecref(StolenReference.DangerousFromPointer(type.DangerousGetAddress()));
            }
        }

        /// <summary>
        /// Check if given type represents a python type that is dervided from
        /// a clr type anywhere in its chain of bases
        /// </summary>
        internal static bool IsPythonDerivedType(Type type) => null != GetPyObjField(type);

        internal static FieldInfo? GetPyObjField(Type type) => type.GetField(PyObjName, PyObjFlags);

        internal static UnsafeReferenceWithRun GetPyObj(IPythonDerivedType obj)
        {
            FieldInfo fi = GetPyObjField(obj.GetType())!;
            return (UnsafeReferenceWithRun)fi.GetValue(obj);
        }

        internal static void SetPyObj(IPythonDerivedType obj, BorrowedReference pyObj)
        {
            FieldInfo fi = GetPyObjField(obj.GetType())!;
            fi.SetValue(obj, new UnsafeReferenceWithRun(pyObj));
        }
    }
}
