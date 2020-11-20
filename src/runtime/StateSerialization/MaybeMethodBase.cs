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
}