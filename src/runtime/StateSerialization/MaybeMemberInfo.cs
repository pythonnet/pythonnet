using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Python.Runtime
{
     [Serializable]
    internal struct MaybeMemberInfo<T> : ISerializable where T: MemberInfo
    {
        public static implicit operator MaybeMemberInfo<T> (T ob) => new MaybeMemberInfo<T>(ob);

        string name;
        MemberInfo info;
        
        // As seen in ClassManager.GetClassInfo
        const BindingFlags k_flags = BindingFlags.Static |
                        BindingFlags.Instance |
                        BindingFlags.Public ;

        public string DeletedMessage 
        {
            get
            {
                return $"The .NET {typeof(T)} {name} no longer exists";
            }
        }

        public T Value
        {
            get
            {
                if (info == null)
                {
                    throw new SerializationException(DeletedMessage);
                }
                return (T)info;
            }
        }

        public string Name {get{return name;}}
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing type: {name}");
        }

        public MaybeMemberInfo(T fi)
        {
            info = fi;
            name = info?.ToString();
        }

        internal MaybeMemberInfo(SerializationInfo serializationInfo, StreamingContext context)
        {
            // Assumption: name is always stored in "s"
            name = serializationInfo.GetString("s");
            info = null;
            try
            {
                var tp = Type.GetType(serializationInfo.GetString("t"));
                if (tp != null)
                {
                    var field_name = serializationInfo.GetString("f");
                    info = tp.GetField(field_name, k_flags);
                }
            }
            catch 
            {
            }
        }

        public void GetObjectData(SerializationInfo serializationInfo, StreamingContext context)
        {
            serializationInfo.AddValue("s", name);
            if (Valid)
            {
                serializationInfo.AddValue("f", info.Name);
                serializationInfo.AddValue("t", info.ReflectedType.AssemblyQualifiedName);
            }
        }
    }
}
