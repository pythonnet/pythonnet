using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMemberInfo<T> : ISerializable where T : MemberInfo
    {
        // .ToString() of the serialized object
        const string SerializationDescription = "d";
        // The ReflectedType of the object
        const string SerializationType = "t";
        const string SerializationMemberName = "n";
        MemberInfo? info;

        [NonSerialized]
        Exception? deserializationException;

        public string DeletedMessage
        {
            get
            {
                return $"The .NET {typeof(T).Name} {Description} no longer exists. Cause: " + deserializationException?.Message ;
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

        public string Description { get; }
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing: {Description}");
        }

        public MaybeMemberInfo(T fi)
        {
            info = fi;
            Description = info.ToString();
            if (info.DeclaringType is not null)
                Description += " of " + info.DeclaringType;
            deserializationException = null;
        }

        internal MaybeMemberInfo(SerializationInfo serializationInfo, StreamingContext context)
        {
            Description = serializationInfo.GetString(SerializationDescription);
            info = null;
            deserializationException = null;
            try
            {
                var tp = Type.GetType(serializationInfo.GetString(SerializationType));
                if (tp != null)
                {
                    var memberName = serializationInfo.GetString(SerializationMemberName);
                    MemberInfo? mi = Get(tp, memberName, ClassManager.BindingFlags);
                    if (mi != null && ShouldBindMember(mi))
                    {
                        info = mi;
                    }
                }
            }
            catch (Exception e)
            {
                deserializationException = e;
            }
        }

        static MemberInfo? Get(Type type, string name, BindingFlags flags)
        {
            if (typeof(T) == typeof(FieldInfo))
                return type.GetField(name, flags);
            if (typeof(T) == typeof(PropertyInfo))
                return type.GetProperty(name, flags);
            throw new NotImplementedException(typeof(T).Name);
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
            serializationInfo.AddValue(SerializationDescription, Description);
            if (info is not null)
            {
                serializationInfo.AddValue(SerializationMemberName, info.Name);
                serializationInfo.AddValue(SerializationType, info.ReflectedType.AssemblyQualifiedName);
            }
        }
    }
}
