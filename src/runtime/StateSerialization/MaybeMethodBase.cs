using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;

using Python.Runtime.Reflection;

namespace Python.Runtime
{
    [Serializable]
    internal struct MaybeMethodBase<T> : ISerializable where T: MethodBase
    {
        // .ToString() of the serialized object
        const string SerializationName = "s";
        // The ReflectedType of the object
        const string SerializationType = "t";
        // Fhe parameters of the MethodBase
        const string SerializationParameters = "p";
        const string SerializationMethodName = "n";
        const string SerializationGenericParamCount = "G";
        const string SerializationFlags = "V";

        public static implicit operator MaybeMethodBase<T> (T? ob) => new (ob);

        string? name;
        MethodBase? info;

        [NonSerialized]
        Exception? deserializationException;

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

        public T UnsafeValue => (T)info!;
        public string? Name => name;
        [MemberNotNullWhen(true, nameof(info))]
        public bool Valid => info != null;

        public override string ToString()
        {
            return (info != null ? info.ToString() : $"missing method info: {name}");
        }

        public MaybeMethodBase(T? mi)
        {
            info = mi;
            name = mi?.ToString();
            Debug.Assert(name != null || info == null);
            deserializationException = null;
        }

        internal MaybeMethodBase(SerializationInfo serializationInfo, StreamingContext context)
        {
            name = serializationInfo.GetString(SerializationName);
            info = null;
            deserializationException = null;

            if (name is null) return;

            try
            {
                // Retrieve the reflected type of the method;
                var typeName = serializationInfo.GetString(SerializationType);
                var tp = Type.GetType(typeName);
                if (tp == null)
                {
                    throw new SerializationException($"The underlying type {typeName} can't be found");
                }

                var flags = (MaybeMethodFlags)serializationInfo.GetInt32(SerializationFlags);
                int genericCount = serializationInfo.GetInt32(SerializationGenericParamCount);

                // Get the method's parameters types
                var field_name = serializationInfo.GetString(SerializationMethodName);
                var param = (ParameterHelper[])serializationInfo.GetValue(SerializationParameters, typeof(ParameterHelper[]));

                info = ScanForMethod(tp, field_name, genericCount, flags, param);
            }
            catch (Exception e)
            {
                deserializationException = e;
            }
        }

        static MethodBase ScanForMethod(Type declaringType, string name, int genericCount, MaybeMethodFlags flags, ParameterHelper[] parameters)
        {
            var bindingFlags = ClassManager.BindingFlags;
            if (flags.HasFlag(MaybeMethodFlags.Constructor)) bindingFlags &= ~BindingFlags.Static;

            var alternatives = declaringType.GetMember(name,
                flags.HasFlag(MaybeMethodFlags.Constructor)
                    ? MemberTypes.Constructor
                    : MemberTypes.Method,
                bindingFlags);

            if (alternatives.Length == 0)
                throw new MissingMethodException($"{declaringType}.{name}");

            var visibility = flags & MaybeMethodFlags.Visibility;

            var result = alternatives.Cast<MethodBase>().FirstOrDefault(m
                => MatchesGenericCount(m, genericCount) && MatchesSignature(m, parameters)
                && (Visibility(m) == visibility || ClassManager.ShouldBindMethod(m)));

            if (result is null)
                throw new MissingMethodException($"Matching overload not found for {declaringType}.{name}");

            return result;
        }

        static bool MatchesGenericCount(MethodBase method, int genericCount)
            => method.ContainsGenericParameters
                ? method.GetGenericArguments().Length == genericCount
                : genericCount == 0;

        static bool MatchesSignature(MethodBase method, ParameterHelper[] parameters)
        {
            var curr = method.GetParameters();
            if (curr.Length != parameters.Length) return false;
            for (int i = 0; i < curr.Length; i++)
                if (!parameters[i].Matches(curr[i])) return false;
            return true;
        }

        public void GetObjectData(SerializationInfo serializationInfo, StreamingContext context)
        {
            serializationInfo.AddValue(SerializationName, name);
            if (Valid)
            {
                serializationInfo.AddValue(SerializationMethodName, info.Name);
                serializationInfo.AddValue(SerializationGenericParamCount,
                    info.ContainsGenericParameters ? info.GetGenericArguments().Length : 0);
                serializationInfo.AddValue(SerializationFlags, (int)Flags(info));
                string? typeName = info.ReflectedType.AssemblyQualifiedName;
                Debug.Assert(typeName != null);
                serializationInfo.AddValue(SerializationType, typeName);
                ParameterHelper[] parameters = (from p in info.GetParameters() select new ParameterHelper(p)).ToArray();
                serializationInfo.AddValue(SerializationParameters, parameters, typeof(ParameterHelper[]));
            }
        }

        static MaybeMethodFlags Flags(MethodBase method)
        {
            var flags = MaybeMethodFlags.Default;
            if (method.IsConstructor) flags |= MaybeMethodFlags.Constructor;
            if (method.IsStatic) flags |= MaybeMethodFlags.Static;
            if (method.IsPublic) flags |= MaybeMethodFlags.Public;
            return flags;
        }

        static MaybeMethodFlags Visibility(MethodBase method)
            => Flags(method) & MaybeMethodFlags.Visibility;
    }

    [Flags]
    internal enum MaybeMethodFlags
    {
        Default = 0,
        Constructor = 1,
        Static = 2,

        // TODO: other kinds of visibility
        Public = 32,
        Visibility = Public,
    }
}
