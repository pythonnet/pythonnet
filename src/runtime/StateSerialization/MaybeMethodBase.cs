using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMethodBase<T> : ISerializable where T: MethodBase
    {
        public static implicit operator MaybeMethodBase<T> (T ob) => new MaybeMethodBase<T>(ob);

        string name;
        MethodBase info;

        [NonSerialized]
        Exception deserializationException;

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

        public T UnsafeValue { get { return (T)info; } }
        public string Name {get{return name;}}
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing method info: {name}");
        }

        public MaybeMethodBase(T mi)
        {
            info = mi;
            name = mi?.ToString();
            deserializationException = null;
        }

        internal MaybeMethodBase(SerializationInfo serializationInfo, StreamingContext context)
        {
            name = serializationInfo.GetString("s");
            info = null;
            deserializationException = null;
            try
            {
                // Retrive the reflected type of the method;
                var tp = Type.GetType(serializationInfo.GetString("t"));
                // Get the method's parameters types
                var field_name = serializationInfo.GetString("f");
                var param = (string[])serializationInfo.GetValue("p", typeof(string[]));
                Type[] types = new Type[param.Length];
                for (int i = 0; i < param.Length; i++)
                {
                    types[i] = Type.GetType(param[i]);
                }
                // Try to get the method
                MethodBase mb = tp.GetMethod(field_name, ClassManager.BindingFlags, binder:null, types:types, modifiers:null);
                // Try again, may be a constructor
                if (mb == null && name.Contains(".ctor"))
                {
                    mb = tp.GetConstructor(ClassManager.BindingFlags, binder:null, types:types, modifiers:null);
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

        public void GetObjectData(SerializationInfo serializationInfo, StreamingContext context)
        {
            serializationInfo.AddValue("s", name);
            if (Valid)
            {
                serializationInfo.AddValue("f", info.Name);
                serializationInfo.AddValue("t", info.ReflectedType.AssemblyQualifiedName);
                var p = info.GetParameters();
                string[] types = new string[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    types[i] = p[i].ParameterType.AssemblyQualifiedName;
                }
                serializationInfo.AddValue("p", types, typeof(string[]));
            }
        }
    }
}