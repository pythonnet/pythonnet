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

        string m_name;
        MemberInfo m_info;
        
        // As seen in ClassManager.GetClassInfo
        const BindingFlags k_flags = BindingFlags.Static |
                        BindingFlags.Instance |
                        BindingFlags.Public ;

        public string DeletedMessage 
        {
            get
            {
                return $"The .NET {typeof(T)} {m_name} no longer exists";
            }
        }

        public T Value
        {
            get
            {
                if (m_info == null)
                {
                    throw new SerializationException(DeletedMessage);
                }
                return (T)m_info;
            }
        }

        public string Name {get{return m_name;}}
        public bool Valid => m_info != null;

        public override string ToString()
        {
            return (m_info != null ? m_info.ToString() : $"missing type: {m_name}");
        }

        public MaybeMemberInfo(T fi)
        {
            m_info = fi;
            m_name = m_info?.ToString();
        }

        internal MaybeMemberInfo(SerializationInfo info, StreamingContext context)
        {
            // Assumption: name is always stored in "s"
            m_name = info.GetString("s");
            m_info = null;
            try
            {
                var tp = Type.GetType(info.GetString("t"));
                if (tp != null)
                {
                    var field_name = info.GetString("f");
                    m_info = tp.GetField(field_name, k_flags);
                }
            }
            catch 
            {
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("s", m_name);
            if (Valid)
            {
                info.AddValue("f", m_info.Name);
                info.AddValue("t", m_info.ReflectedType.AssemblyQualifiedName);
            }
        }
    }
}
