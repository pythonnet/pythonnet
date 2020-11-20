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
            return (m_type != null ? m_type.ToString() : $"missing type: {m_name}");
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
}