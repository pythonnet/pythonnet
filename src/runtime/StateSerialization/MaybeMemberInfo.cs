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
                    MemberInfo mi = tp.GetField(field_name, ClassManager.BindingFlags);
                    if (mi != null && ShouldBindMember(mi))
                    {
                        info = mi;
                    }
                }
            }
            catch 
            {
            }
        }

        // This is complicated because we bind fields 
        // based on the visibility of the field, properties 
        // based on it's setter/getter (which is a method 
        //  info) visibility and events based on their
        // AddMethod visibility.
        static bool ShouldBindMember(MemberInfo mi)
        {
            if (mi is PropertyInfo pi)
            {
                return ClassManager.ShouldBindProperty(pi);
            }
            else if (mi is FieldInfo fi)
            {
                return ClassManager.ShouldBindField(fi);
            }
            else if (mi is EventInfo ei)
            {
                return ClassManager.ShouldBindEvent(ei);
            }

            return false;
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
