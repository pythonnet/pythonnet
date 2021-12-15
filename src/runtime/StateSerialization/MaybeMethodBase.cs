using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;

using Python.Runtime.Reflection;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMethodBase<T> : ISerializable where T: MethodBase
    {
        // .ToString() of the serialized object
        const string SerializationName = "s";
        // The ReflectedType of the object
        const string SerializationType = "t";
        // Fhe parameters of the MethodBase
        const string SerializationParameters = "p";
        const string SerializationIsCtor = "c";
        const string SerializationMethodName = "n";

        public static implicit operator MaybeMethodBase<T> (T? ob) => new (ob);

        string? name;
        MethodBase? info;

        [NonSerialized]
        Exception? deserializationException;

        public string DeletedMessage 
        {
            get
            {
                return $"The .NET {typeof(T)} {name} no longer exists. Cause: " + deserializationException?.Message ;
            }
        }

        public T Value
        {
            get
            {
                if (info == null)
                {
                    throw new SerializationException(DeletedMessage, innerException: deserializationException);
                }
                return (T)info;
            }
        }

        public T UnsafeValue => (T)info!;
        public string? Name => name;
        [MemberNotNullWhen(true, nameof(info))]
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing method info: {name}");
        }

        public MaybeMethodBase(T? mi)
        {
            info = mi;
            name = mi?.ToString();
            deserializationException = null;
        }

        internal MaybeMethodBase(SerializationInfo serializationInfo, StreamingContext context)
        {
            name = serializationInfo.GetString(SerializationName);
            info = null;
            deserializationException = null;

            if (name is null) return;

            try
            {
                // Retrieve the reflected type of the method;
                var typeName = serializationInfo.GetString(SerializationType);
                var tp = Type.GetType(typeName);
                if (tp == null)
                {
                    throw new SerializationException($"The underlying type {typeName} can't be found");
                }
                // Get the method's parameters types
                var field_name = serializationInfo.GetString(SerializationMethodName);
                var param = (ParameterHelper[])serializationInfo.GetValue(SerializationParameters, typeof(ParameterHelper[]));
                Type[] types = new Type[param.Length];
                bool hasRefType = false;
                for (int i = 0; i < param.Length; i++)
                {
                    var paramTypeName = param[i].TypeName;
                    types[i] = Type.GetType(paramTypeName);
                    if (types[i] == null)
                    {
                        throw new SerializationException($"The parameter of type {paramTypeName} can't be found");
                    }
                    else if (types[i].IsByRef)
                    {
                        hasRefType = true;
                    }
                }

                MethodBase? mb = null;
                if (serializationInfo.GetBoolean(SerializationIsCtor))
                {
                    // We never want the static constructor.
                    mb = tp.GetConstructor(ClassManager.BindingFlags&(~BindingFlags.Static), binder:null, types:types, modifiers:null);
                }
                else
                {
                    mb = tp.GetMethod(field_name, ClassManager.BindingFlags, binder:null, types:types, modifiers:null);
                }
                
                if (mb != null && hasRefType)
                {
                    mb = CheckRefTypes(mb, param);
                }

                // Do like in ClassManager.GetClassInfo
                if(mb != null && ClassManager.ShouldBindMethod(mb))
                {
                    info = mb;
                }
            }
            catch (Exception e)
            {
                deserializationException = e;
            }
        }

        MethodBase? CheckRefTypes(MethodBase mb, ParameterHelper[] ph)
        {
            // One more step: Changing:
            // void MyFn (ref int a)
            // to:
            // void MyFn (out int a)
            // will still find the function correctly as, `in`, `out` and `ref`
            // are all represented as a reference type. Query the method we got
            // and validate the parameters
            if (ph.Length != 0)
            {
                foreach (var item in Enumerable.Zip(ph, mb.GetParameters(), (orig, current) => new {orig, current}))
                {
                    if (!item.current.Equals(item.orig))
                    {
                        // False positive
                        return null;
                    }
                }
            }

            return mb;
        }

        public void GetObjectData(SerializationInfo serializationInfo, StreamingContext context)
        {
            serializationInfo.AddValue(SerializationName, name);
            if (Valid)
            {
                serializationInfo.AddValue(SerializationMethodName, info.Name);
                serializationInfo.AddValue(SerializationType, info.ReflectedType.AssemblyQualifiedName);
                ParameterHelper[] parameters = (from p in info.GetParameters() select new ParameterHelper(p)).ToArray();
                serializationInfo.AddValue(SerializationParameters, parameters, typeof(ParameterHelper[]));
                serializationInfo.AddValue(SerializationIsCtor, info.IsConstructor);
            }
        }
    }
}
