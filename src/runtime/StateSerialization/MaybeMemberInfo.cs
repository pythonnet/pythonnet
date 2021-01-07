using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMemberInfo<T> : ISerializable where T : MemberInfo
    {
        public static implicit operator MaybeMemberInfo<T>(T ob) => new MaybeMemberInfo<T>(ob);

        // .ToString() of the serialized object
        const string SerializationName = "s";
        // The ReflectedType of the object
        const string SerializationType = "t";
        const string SerializationFieldName = "f";
        string name;
        MemberInfo info;

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

        public string Name => name;
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing type: {name}");
        }

        public MaybeMemberInfo(T fi)
        {
            info = fi;
            name = info?.ToString();
            deserializationException = null;
        }

        internal MaybeMemberInfo(SerializationInfo serializationInfo, StreamingContext context)
        {
            // Assumption: name is always stored in "s"
            name = serializationInfo.GetString(SerializationName);
            info = null;
            deserializationException = null;
            try
            {
                var tp = Type.GetType(serializationInfo.GetString(SerializationType));
                if (tp != null)
                {
                    var field_name = serializationInfo.GetString(SerializationFieldName);
                    MemberInfo mi = tp.GetField(field_name, ClassManager.BindingFlags);
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
            serializationInfo.AddValue(SerializationName, name);
            if (Valid)
            {
                serializationInfo.AddValue(SerializationFieldName, info.Name);
                serializationInfo.AddValue(SerializationType, info.ReflectedType.AssemblyQualifiedName);
            }
        }
    }
}
