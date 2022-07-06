using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Python.Runtime.StateSerialization;

using static Python.Runtime.Runtime;

namespace Python.Runtime
{
    [System.Serializable]
    public sealed class NotSerializedException: SerializationException
    {
        static string _message = "The underlying C# object has been deleted.";
        public NotSerializedException() : base(_message){}
        private NotSerializedException(SerializationInfo info, StreamingContext context) : base(info, context){}
        override public void GetObjectData(SerializationInfo info, StreamingContext context) => base.GetObjectData(info, context);
    }

    [Serializable]
    internal static class NonSerializedTypeBuilder
    {
        
        internal static AssemblyName nonSerializedAssemblyName = 
            new AssemblyName("Python.Runtime.NonSerialized.dll, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        internal static AssemblyBuilder assemblyForNonSerializedClasses = 
            AppDomain.CurrentDomain.DefineDynamicAssembly(nonSerializedAssemblyName, AssemblyBuilderAccess.Run);
        internal static ModuleBuilder moduleBuilder = assemblyForNonSerializedClasses.DefineDynamicModule("NotSerializedModule");
        internal static HashSet<string> dontReimplementMethods = new(){"Finalize", "Dispose", "GetType", "ReferenceEquals", "GetHashCode", "Equals"};
        internal const string notSerializedSuffix = "_NotSerialized";
        // dummy field name to mark classes created by the "non-serializer" so we don't loop-inherit
        // on multiple cycles of de/serialization. We use a static field instead of an attribute
        // becaues of a bug in mono. Put a space in the name so users will be extremely unlikely
        // to create a field with the same name.
        internal const string notSerializedFieldName = "__PyNet NonSerialized";

        private static Func<Type, TypeAttributes, bool> hasVisibility = (tp, attr) => (tp.Attributes & TypeAttributes.VisibilityMask) == attr; 
        private static Func<Type, bool> isNestedType = (tp) => hasVisibility(tp, TypeAttributes.NestedPrivate) || hasVisibility(tp, TypeAttributes.NestedPublic) || hasVisibility(tp, TypeAttributes.NestedFamily) || hasVisibility(tp, TypeAttributes.NestedAssembly);
        private static Func<Type, bool> isPrivateType = (tp) => hasVisibility(tp, TypeAttributes.NotPublic) || hasVisibility(tp, TypeAttributes.NestedPrivate) || hasVisibility(tp, TypeAttributes.NestedFamily) || hasVisibility( tp, TypeAttributes.NestedAssembly);
        private static Func<Type, bool> isPublicType = (tp) => hasVisibility(tp, TypeAttributes.Public) || hasVisibility(tp,TypeAttributes.NestedPublic);

        public static object? CreateNewObject(Type baseType)
        {
            var myType = CreateType(baseType);
            if (myType is null)
            {
                return null;
            }
            var myObject = Activator.CreateInstance(myType);
            return myObject;
        }

        static void FillTypeMethods(TypeBuilder tb)
        {
            var constructors = tb.BaseType.GetConstructors();
            if (constructors.Count() == 0)
            {
                // no constructors defined, at least declare a default
                ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            }
            else
            {
                foreach (var ctor in constructors)
                {
                    var ctorParams = (from param in ctor.GetParameters() select param.ParameterType).ToArray();
                    var ctorbuilder = tb.DefineConstructor(ctor.Attributes, ctor.CallingConvention, ctorParams);
                    ctorbuilder.GetILGenerator().Emit(OpCodes.Ret);

                }
                var parameterless = tb.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard  | CallingConventions.HasThis, Type.EmptyTypes);
                parameterless.GetILGenerator().Emit(OpCodes.Ret);
            }

            var properties = tb.BaseType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var prop in properties)
            {
                CreateProperty(tb, prop);
            }

            var methods = tb.BaseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var meth in methods)
            {
                CreateMethod(tb, meth);
            }

            ImplementEqualityAndHash(tb);
        }

        static string MakeName(Type tp)
        {
            string @out = tp.Name + notSerializedSuffix;
            var parentType = tp.DeclaringType;
            while (parentType is not null)
            {
                // If we have a nested class, we need the whole nester/nestee 
                // chain with the suffix for each.
                @out = parentType.Name + notSerializedSuffix + "+" + @out;
                parentType = parentType.DeclaringType;
            }
            return @out;
        }

        public static Type? CreateType(Type tp)
        {
            if (!isPublicType(tp))
            {
                return null;
            }

            Type existingType = assemblyForNonSerializedClasses.GetType(MakeName(tp), throwOnError:false);
            if (existingType is not null)
            {
                return existingType;
            }
            var parentType = tp.DeclaringType;
            if (parentType is not null)
            {
                // parent types for nested types must be created first. Climb up the 
                // declaring type chain until we find a "top-level" class.
                while (parentType.DeclaringType is not null)
                {
                    parentType = parentType.DeclaringType;
                }
                CreateTypeInternal(parentType);
                Type nestedType = assemblyForNonSerializedClasses.GetType(MakeName(tp), throwOnError:true);
                return nestedType;
            }
            return CreateTypeInternal(tp);
        }

        private static Type? CreateTypeInternal(Type baseType)
        {
            if (!isPublicType(baseType))
            {
                // we can't derive from non-public types.
                return null;
            }
            Type existingType = assemblyForNonSerializedClasses.GetType(MakeName(baseType), throwOnError:false);
            if (existingType is not null)
            {
                return existingType;
            }

            TypeBuilder tb = GetTypeBuilder(baseType);
            SetNonSerializedAttr(tb);
            FillTypeMethods(tb);

            var nestedtypes = baseType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            List<TypeBuilder> nestedBuilders = new();
            foreach (var nested in nestedtypes)
            {
                if (isPrivateType(nested))
                {
                    continue;
                }
                var nestedBuilder = tb.DefineNestedType(nested.Name + notSerializedSuffix,
                TypeAttributes.NestedPublic,
                nested
                );
                nestedBuilders.Add(nestedBuilder);
            }
            var outTp = tb.CreateType();
            foreach(var builder in nestedBuilders)
            {
                FillTypeMethods(builder);
                SetNonSerializedAttr(builder);
                builder.CreateType();
            }
            return outTp;
        }

        private static void ImplementEqualityAndHash(TypeBuilder tb)
        {
            var hashCodeMb = tb.DefineMethod("GetHashCode", 
                                             MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.ReuseSlot,
                                             CallingConventions.Standard,
                                             typeof(int),
                                             Type.EmptyTypes
                                    );
            var getHashIlGen = hashCodeMb.GetILGenerator();
            getHashIlGen.Emit(OpCodes.Ldarg_0);
            getHashIlGen.EmitCall(OpCodes.Call, typeof(object).GetMethod("GetType"), Type.EmptyTypes);
            getHashIlGen.EmitCall(OpCodes.Call, typeof(Type).GetProperty("Name").GetMethod, Type.EmptyTypes);
            getHashIlGen.EmitCall(OpCodes.Call, typeof(string).GetMethod("GetHashCode", Type.EmptyTypes), Type.EmptyTypes);
            getHashIlGen.Emit(OpCodes.Ret);

            Type[] equalsArgs = new Type[] {typeof(object), typeof(object)};
            var equalsMb = tb.DefineMethod("Equals", 
                                           MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.ReuseSlot,
                                           CallingConventions.Standard,
                                           typeof(bool),
                                           equalsArgs
                                           );
            var equalsIlGen = equalsMb.GetILGenerator();
            equalsIlGen.Emit(OpCodes.Ldarg_0); // this
            equalsIlGen.Emit(OpCodes.Ldarg_1); // the other object
            equalsIlGen.EmitCall(OpCodes.Call, typeof(object).GetMethod("ReferenceEquals"), equalsArgs);
            equalsIlGen.Emit(OpCodes.Ret);
        }

        private static void SetNonSerializedAttr(TypeBuilder tb)
        {
            // Name of the function says we're adding an attribute, but for some
            // reason on Mono the attribute is not added, and no exceptions are
            // thrown.
            tb.DefineField(notSerializedFieldName, typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        }

        public static bool IsNonSerializedType(Type tp)
        {
            return tp.GetField(NonSerializedTypeBuilder.notSerializedFieldName, BindingFlags.Public | BindingFlags.Static) is not null;

        }

        private static TypeBuilder GetTypeBuilder(Type baseType)
        {
            string typeSignature = baseType.Name + notSerializedSuffix;
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    baseType.Attributes,
                    baseType,
                    baseType.GetInterfaces());
            return tb;
        }

        static ILGenerator GenerateExceptionILCode(dynamic builder)
        {
            ILGenerator ilgen = builder.GetILGenerator();
            var seriExc = typeof(NotSerializedException);
            var exCtorInfo = seriExc.GetConstructor(new Type[]{});
            ilgen.Emit(OpCodes.Newobj, exCtorInfo);
            ilgen.ThrowException(seriExc);
            return ilgen;
        }
        
        private static MethodAttributes GetMethodAttrs (MethodInfo minfo)
        {
            var methAttributes = minfo.Attributes;
            // Always implement/shadow the method
            methAttributes &=(~MethodAttributes.Abstract);
            methAttributes &=(~MethodAttributes.NewSlot);
            methAttributes |= MethodAttributes.ReuseSlot;
            methAttributes |= MethodAttributes.HideBySig;
            methAttributes |= MethodAttributes.Final;

            if (minfo.IsFinal)
            {
                // can't override a final method, new it instead.
                methAttributes &= (~MethodAttributes.Virtual);
                methAttributes |= MethodAttributes.NewSlot;
            }

            return methAttributes;
        }

        private static void CreateProperty(TypeBuilder tb, PropertyInfo pinfo)
        {
            string propertyName = pinfo.Name;
            Type propertyType = pinfo.PropertyType;
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, pinfo.Attributes, propertyType, null);
            if (pinfo.GetMethod is not null)
            {
                var methAttributes = GetMethodAttrs(pinfo.GetMethod);
                
                MethodBuilder getPropMthdBldr = 
                    tb.DefineMethod("get_" + propertyName, 
                                    methAttributes,
                                    propertyType, 
                                    Type.EmptyTypes);
                GenerateExceptionILCode(getPropMthdBldr);
                propertyBuilder.SetGetMethod(getPropMthdBldr);
            }
            if (pinfo.SetMethod is not null)
            {
                var methAttributes = GetMethodAttrs(pinfo.SetMethod);
                MethodBuilder setPropMthdBldr =
                    tb.DefineMethod("set_" + propertyName,
                                    methAttributes,
                                    null,
                                    new[] { propertyType });

                GenerateExceptionILCode(setPropMthdBldr);
                propertyBuilder.SetSetMethod(setPropMthdBldr);
            }
        }

        private static void CreateMethod(TypeBuilder tb, MethodInfo minfo)
        {
            string methodName = minfo.Name;
            
            if (dontReimplementMethods.Contains(methodName))
            {
                // Some methods must *not* be reimplemented (who wants to throw from Dispose?)
                // and some methods we need to implement in a more specific way (Equals, GetHashCode)
                return;
            }
            var methAttributes = GetMethodAttrs(minfo);
            var @params = (from paraminfo in minfo.GetParameters() select paraminfo.ParameterType).ToArray();
            MethodBuilder mbuilder = tb.DefineMethod(methodName, methAttributes, minfo.CallingConvention, minfo.ReturnType, @params);
            GenerateExceptionILCode(mbuilder);
        }
    }

    class NotSerializableSerializer : ISerializationSurrogate
    {

        public NotSerializableSerializer()
        {
        }

        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            // This type is private to System.Runtime.Serialization. We get an
            // object of this type when, amongst others, the type didn't exist (yet?)
            // (dll not loaded, type was removed/renamed) when we previously
            // deserialized the previous domain objects. Don't serialize this
            // object.
            if (obj.GetType().Name == "TypeLoadExceptionHolder")
            {
                obj = null!;
                return;
            }

            MaybeType type = obj.GetType();
            
            if (NonSerializedTypeBuilder.IsNonSerializedType(type.Value))
            {
                // Don't serialize a _NotSerialized. Serialize the base type, and deserialize as a _NotSerialized
                type = type.Value.BaseType;
                obj = null!;
            }

            info.AddValue("notSerialized_tp", type);

        }
    
        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            if (info is null)
            {
                // `obj` is of type TypeLoadExceptionHolder. This means the type 
                // we're trying to load doesn't exist anymore or we haven't created
                // it yet, and the runtime doesn't even gives us the chance to
                // recover from this as info is null. We may even get objects
                // this serializer did not serialize in a previous domain,
                // like in the case of the "namespace_rename" domain reload 
                // test: the object successfully serialized, but it cannot be
                // deserialized.
                // just return null.
                return null!;
            }

            object nameObj = null!;
            try
            {
                nameObj = info.GetValue("notSerialized_tp", typeof(object));
            }
            catch
            {
                // we didn't find the expected information. We don't know
                // what to do with this; return null.
                return null!;
            }
            Debug.Assert(nameObj.GetType() == typeof(MaybeType));
            MaybeType name = (MaybeType)nameObj;
            Debug.Assert(name.Valid);
            if (!name.Valid)
            {
                // The type couldn't be loaded
                return null!;
            }

            obj = NonSerializedTypeBuilder.CreateNewObject(name.Value)!;
            return obj;
        }
    }

    class NonSerializableSelector : SurrogateSelector
    {
        public override ISerializationSurrogate? GetSurrogate (Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            if (type is null)
            {
                throw new ArgumentNullException();
            }
            selector = this;
            if (type.IsSerializable)
            {
                return null; // use whichever default
            }
            else
            {
                return new NotSerializableSerializer();
            }
        }
    }

    public static class RuntimeData
    {
        private static Type? _formatterType;
        public static Type? FormatterType
        {
            get => _formatterType;
            set
            {
                if (!typeof(IFormatter).IsAssignableFrom(value))
                {
                    throw new ArgumentException("Not a type implemented IFormatter");
                }
                _formatterType = value;
            }
        }

        public static ICLRObjectStorer? WrappersStorer { get; set; }

        /// <summary>
        /// Clears the old "clr_data" entry if a previous one is present.
        /// </summary>
        static void ClearCLRData ()
        {
            BorrowedReference capsule = PySys_GetObject("clr_data");
            if (!capsule.IsNull)
            {
                IntPtr oldData = PyCapsule_GetPointer(capsule, IntPtr.Zero);
                PyMem_Free(oldData);
                PyCapsule_SetPointer(capsule, IntPtr.Zero);
            }
        }

        internal static void SerializeNonSerializableTypes ()
        {
            // Serialize the Types (Type objects) that couldn't be (de)serialized.
            // This needs to be done otherwise at deserialization time we get
            // TypeLoadExceptionHolder objects and we can't even recover.

            // We don't serialize the "_NotSerialized" Types, we serialize their base
            // to recreate the "_NotSerialized" versions on the next domain load.
            
            Dictionary<string, MaybeType> invalidTypes = new();
            foreach(var tp in NonSerializedTypeBuilder.assemblyForNonSerializedClasses.GetTypes())
            {
                invalidTypes[tp.FullName] = new MaybeType(tp.BaseType);
            }

            // delete previous data if any
            BorrowedReference oldCapsule = PySys_GetObject("clr_nonSerializedTypes");
            if (!oldCapsule.IsNull)
            {
                IntPtr oldData = PyCapsule_GetPointer(oldCapsule, IntPtr.Zero);
                PyMem_Free(oldData);
                PyCapsule_SetPointer(oldCapsule, IntPtr.Zero);
            }
            IFormatter formatter = CreateFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, invalidTypes);
            
            Debug.Assert(ms.Length <= int.MaxValue);
            byte[] data = ms.GetBuffer();
            
            IntPtr mem = PyMem_Malloc(ms.Length + IntPtr.Size);
            Marshal.WriteIntPtr(mem, (IntPtr)ms.Length);
            Marshal.Copy(data, 0, mem + IntPtr.Size, (int)ms.Length);

            using NewReference capsule = PyCapsule_New(mem, IntPtr.Zero, IntPtr.Zero);
            int res = PySys_SetObject("clr_nonSerializedTypes", capsule.BorrowOrThrow());
            PythonException.ThrowIfIsNotZero(res);

        }

        internal static void DeserializeNonSerializableTypes ()
        {
            BorrowedReference capsule = PySys_GetObject("clr_nonSerializedTypes");
            if (capsule.IsNull)
            {
                // nothing to do.
                return;
            }
            // get the memory stream from the capsule.
            IntPtr mem = PyCapsule_GetPointer(capsule, IntPtr.Zero);
            int length = (int)Marshal.ReadIntPtr(mem);
            byte[] data = new byte[length];
            Marshal.Copy(mem + IntPtr.Size, data, 0, length);
            var ms = new MemoryStream(data);
            var formatter = CreateFormatter();
            var storage = (Dictionary<string, MaybeType>)formatter.Deserialize(ms);
            foreach(var item in storage)
            {
                if(item.Value.Valid)
                {
                    // recreate the "_NotSerialized" Types
                    NonSerializedTypeBuilder.CreateType(item.Value.Value);
                }
            }

        }

        internal static void Stash()
        {
            var runtimeStorage = new PythonNetState
            {
                Metatype = MetaType.SaveRuntimeData(),
                ImportHookState = ImportHook.SaveRuntimeData(),
                Types = TypeManager.SaveRuntimeData(),
                Classes = ClassManager.SaveRuntimeData(),
                SharedObjects = SaveRuntimeDataObjects(),
            };

            IFormatter formatter = CreateFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, runtimeStorage);

            Debug.Assert(ms.Length <= int.MaxValue);
            byte[] data = ms.GetBuffer();
            // TODO: use buffer api instead
            IntPtr mem = PyMem_Malloc(ms.Length + IntPtr.Size);
            Marshal.WriteIntPtr(mem, (IntPtr)ms.Length);
            Marshal.Copy(data, 0, mem + IntPtr.Size, (int)ms.Length);

            ClearCLRData();

            using NewReference capsule = PyCapsule_New(mem, IntPtr.Zero, IntPtr.Zero);
            int res = PySys_SetObject("clr_data", capsule.BorrowOrThrow());
            PythonException.ThrowIfIsNotZero(res);
            SerializeNonSerializableTypes();

        }

        internal static void RestoreRuntimeData()
        {
            try
            {
                RestoreRuntimeDataImpl();
            }
            finally
            {
                ClearStash();
            }
        }

        private static void RestoreRuntimeDataImpl()
        {
            // The "_NotSerialized" Types must exist before the rest of the data
            // is deserialized.
            DeserializeNonSerializableTypes();
            BorrowedReference capsule = PySys_GetObject("clr_data");
            if (capsule.IsNull)
            {
                return;
            }
            IntPtr mem = PyCapsule_GetPointer(capsule, IntPtr.Zero);
            int length = (int)Marshal.ReadIntPtr(mem);
            byte[] data = new byte[length];
            Marshal.Copy(mem + IntPtr.Size, data, 0, length);
            var ms = new MemoryStream(data);
            var formatter = CreateFormatter();
            var storage = (PythonNetState)formatter.Deserialize(ms);

            PyCLRMetaType = MetaType.RestoreRuntimeData(storage.Metatype);

            TypeManager.RestoreRuntimeData(storage.Types);
            ClassManager.RestoreRuntimeData(storage.Classes);

            RestoreRuntimeDataObjects(storage.SharedObjects);

            ImportHook.RestoreRuntimeData(storage.ImportHookState);
        }

        public static bool HasStashData()
        {
            return !PySys_GetObject("clr_data").IsNull;
        }

        public static void ClearStash()
        {
            PySys_SetObject("clr_data", default);
        }

        private static SharedObjectsState SaveRuntimeDataObjects()
        {
            var contexts = new Dictionary<PyObject, Dictionary<string, object?>>(PythonReferenceComparer.Instance);
            var extensionObjs = new Dictionary<PyObject, ExtensionType>(PythonReferenceComparer.Instance);
            // make a copy with strongly typed references to avoid concurrent modification
            var extensions = ExtensionType.loadedExtensions
                                .Select(addr => new PyObject(
                                    new BorrowedReference(addr),
                                    // if we don't skip collect, finalizer might modify loadedExtensions
                                    skipCollect: true))
                                .ToArray();
            foreach (var pyObj in extensions)
            {
                var extension = (ExtensionType)ManagedType.GetManagedObject(pyObj)!;
                var context = extension.Save(pyObj);
                if (context is not null)
                {
                    contexts[pyObj] = context;
                }
                extensionObjs.Add(pyObj, extension);
            }

            var wrappers = new Dictionary<object, List<CLRObject>>();
            var userObjects = new CLRWrapperCollection();
            // make a copy with strongly typed references to avoid concurrent modification
            var reflectedObjects = CLRObject.reflectedObjects
                                    .Select(addr => new PyObject(
                                        new BorrowedReference(addr),
                                        // if we don't skip collect, finalizer might modify reflectedObjects
                                        skipCollect: true))
                                    .ToList();
            foreach (var pyObj in reflectedObjects)
            {
                // Console.WriteLine($"saving object: {pyObj} {pyObj.rawPtr} ");
                // Wrapper must be the CLRObject
                var clrObj = (CLRObject)ManagedType.GetManagedObject(pyObj)!;
                object inst = clrObj.inst;
                List<CLRObject> mappedObjs;
                if (!userObjects.TryGetValue(inst, out var item))
                {
                    item = new CLRMappedItem(inst);
                    userObjects.Add(item);

                    Debug.Assert(!wrappers.ContainsKey(inst));
                    mappedObjs = new List<CLRObject>();
                    wrappers.Add(inst, mappedObjs);
                }
                else
                {
                    mappedObjs = wrappers[inst];
                }
                item.AddRef(pyObj);
                mappedObjs.Add(clrObj);
            }

            var wrapperStorage = new Dictionary<string, object?>();
            WrappersStorer?.Store(userObjects, wrapperStorage);

            var internalStores = new Dictionary<PyObject, CLRObject>(PythonReferenceComparer.Instance);
            foreach (var item in userObjects)
            {
                if (!item.Stored)
                {
                    var clrO = wrappers[item.Instance].First();
                    foreach (var @ref in item.PyRefs)
                    {
                        internalStores.Add(@ref, clrO);
                    }
                }
            }

            return new()
            {
                InternalStores = internalStores,
                Extensions = extensionObjs,
                Wrappers = wrapperStorage,
                Contexts = contexts,
            };
        }

        private static void RestoreRuntimeDataObjects(SharedObjectsState storage)
        {
            var extensions = storage.Extensions;
            var internalStores = storage.InternalStores;
            var contexts = storage.Contexts;
            foreach (var extension in extensions)
            {
                contexts.TryGetValue(extension.Key, out var context);
                extension.Value.Load(extension.Key, context);
            }
            foreach (var clrObj in internalStores)
            {
                clrObj.Value.Load(clrObj.Key, null);
            }
            if (WrappersStorer != null)
            {
                var wrapperStorage = storage.Wrappers;
                var handle2Obj = WrappersStorer.Restore(wrapperStorage);
                foreach (var item in handle2Obj)
                {
                    object obj = item.Instance;
                    foreach (var pyRef in item.PyRefs ?? new List<PyObject>())
                    {
                        var context = contexts[pyRef];
                        CLRObject.Restore(obj, pyRef, context);
                    }
                }
            }
        }

        internal static IFormatter CreateFormatter()
        {
            return FormatterType != null ?
                (IFormatter)Activator.CreateInstance(FormatterType)
                : new BinaryFormatter()
                {
                    SurrogateSelector = new NonSerializableSelector(),
                };
        }
    }
}
