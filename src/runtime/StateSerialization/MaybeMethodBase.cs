using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMethodBase<T> : ISerializable where T: MethodBase
    {
        [Serializable]
        struct ParameterHelper : IEquatable<ParameterInfo>
        {
            public enum TypeModifier
            {
                None,
                In,
                Out,
                Ref
            }
            public readonly string Name;
            public readonly TypeModifier Modifier;

            public ParameterHelper(ParameterInfo tp)
            {
                Name = tp.ParameterType.AssemblyQualifiedName;
                Modifier = TypeModifier.None;

                if (tp.IsIn)
                {
                    Modifier = TypeModifier.In;
                }
                else if (tp.IsOut)
                {
                    Modifier = TypeModifier.Out;
                }
                else if (tp.ParameterType.IsByRef)
                {
                    Modifier = TypeModifier.Ref;
                }
            }

            public bool Equals(ParameterInfo other)
            {
                return this.Equals(new ParameterHelper(other));
            }
        }
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
                var param = (ParameterHelper[])serializationInfo.GetValue("p", typeof(ParameterHelper[]));
                Type[] types = new Type[param.Length];
                for (int i = 0; i < param.Length; i++)
                {
                    types[i] = Type.GetType(param[i].Name);
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
                    // One more step: Changing:
                    // void MyFn (ref int a)
                    // to:
                    // void MyFn (out int a)
                    // will still find the fucntion correctly as, `in`, `out` and `ref`
                    // are all represented as a reference type. Query the method we got
                    // and validate the parameters
                    bool matches = true;
                    if (param.Length != 0)
                    {
                        foreach (var item in Enumerable.Zip(param, mb.GetParameters(), (x, y) => new {x, y}))
                        {
                            if (!item.x.Equals(item.y))
                            {
                                matches = false;
                                break;
                            }
                        }
                    }
                    if (matches)
                    {
                        info = mb;
                    }
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
                ParameterHelper[] parameters = (from p in info.GetParameters() select new ParameterHelper(p)).ToArray();
                serializationInfo.AddValue("p", parameters, typeof(ParameterHelper[]));
            }
        }
    }
}