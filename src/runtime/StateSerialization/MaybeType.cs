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
        public static implicit operator MaybeType (Type ob) => new(ob);

        // The AssemblyQualifiedName of the serialized Type
        const string SerializationName = "n";
        readonly string name;
        readonly Type type;

        public string DeletedMessage
        {
            get
            {
                return $"The .NET Type {name} no longer exists";
            }
        }

        public Type Value
        {
            get
            {
                if (type == null)
                {
                    throw new SerializationException(DeletedMessage);
                }
                return type;
            }
        }

        public string Name => name;
        public bool Valid => type != null;

        public override string ToString()
        {
            return (type != null ? type.ToString() : $"missing type: {name}");
        }

        public MaybeType(Type tp)
        {
            type = tp;
            name = tp.AssemblyQualifiedName;
        }

        private MaybeType(SerializationInfo serializationInfo, StreamingContext context)
        {
            name = (string)serializationInfo.GetValue(SerializationName, typeof(string));
            type = Type.GetType(name, throwOnError:false);
        }

        public void GetObjectData(SerializationInfo serializationInfo, StreamingContext context)
        {
            serializationInfo.AddValue(SerializationName, name);
        }
    }
}
