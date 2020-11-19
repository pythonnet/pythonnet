using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeType : ISerializable
    {
        public static implicit operator MaybeType (Type ob) => new MaybeType(ob);

        string m_name;
        Type m_type;
        
        public string DeletedMessage
        {
            get
            {
                return $"The .NET Type {m_name} no longer exists";
            }
        }

        public Type Value
        {
            get
            {
                if (m_type == null)
                {
                    throw new SerializationException(DeletedMessage);
                }
                return m_type;
            }
        }

        public string Name {get{return m_name;}}
        public bool Valid => m_type != null;

        public override string ToString()
        {
            return (m_type != null ? m_type.ToString() : $"missing type: {m_name}") + Valid.ToString();
        }

        public MaybeType(Type tp)
        {
            m_type = tp;
            m_name = tp.AssemblyQualifiedName;
        }

        private MaybeType(SerializationInfo info, StreamingContext context)
        {
            m_name = (string)info.GetValue("n", typeof(string));
            m_type = Type.GetType(m_name, throwOnError:false);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("n", m_name);
        }
    }

    [Serializable]
    internal struct MaybeMethod<T> : ISerializable where T: MethodBase//, MethodInfo, ConstructorInfo
    {

        public static implicit operator MaybeMethod<T> (T ob) => new MaybeMethod<T>(ob);

        string m_name;
        MethodBase m_info;
        // As seen in ClassManager.GetClassInfo
        const BindingFlags k_flags = BindingFlags.Static |
                        BindingFlags.Instance |
                        BindingFlags.Public ;

        public T Value
        {
            get
            {
                if (m_info == null)
                {
                    throw new SerializationException($"The .NET {typeof(T)} {m_name} no longer exists");
                }
                return (T)m_info;
            }
        }

        public T UnsafeValue { get { return (T)m_info; } }
        public string Name {get{return m_name;}}
        public bool Valid => m_info != null;

        public override string ToString()
        {
            return (m_info != null ? m_info.ToString() : $"missing method info: {m_name}");
        }

        public MaybeMethod(T mi)
        {
            m_info = mi;
            m_name = mi?.ToString();
        }

        internal MaybeMethod(SerializationInfo info, StreamingContext context)
        {
            m_name = info.GetString("s");
            m_info = null;
            try
            {
                // Retrive the reflected type of the method;
                var tp = Type.GetType(info.GetString("t"));
                // Get the method's parameters types
                var field_name = info.GetString("f");
                var param = (string[])info.GetValue("p", typeof(string[]));
                Type[] types = new Type[param.Length];
                for (int i = 0; i < param.Length; i++)
                {
                    types[i] = Type.GetType(param[i]);
                }
                // Try to get the method
                m_info = tp.GetMethod(field_name, k_flags, binder:null, types:types, modifiers:null);
                // Try again, may be a constructor
                if (m_info == null && m_name.Contains(".ctor"))
                {
                    m_info = tp.GetConstructor(k_flags, binder:null, types:types, modifiers:null);
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
                var p = m_info.GetParameters();
                string[] types = new string[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    types[i] = p[i].ParameterType.AssemblyQualifiedName;
                }
                info.AddValue("p", types, typeof(string[]));
            }
        }
    }

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
            return (m_info != null ? m_info.ToString() : $"missing type: {m_name}  ") + Valid.ToString();
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
