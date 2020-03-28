using System;
using System.Collections.Generic;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed dictionaries. This type is essentially
    /// the same as a ClassObject, except that it provides sequence semantics
    /// to support natural dictionary usage (__contains__ and __len__) from Python.
    /// </summary>
    internal class DictionaryObject : ClassObject
    {
        private static Dictionary<Tuple<Type, string>, MethodInfo> methodsByType = new Dictionary<Tuple<Type, string>, MethodInfo>();
        private static Dictionary<string, string> methodMap = new Dictionary<string, string>
        {
            { "mp_length", "Count" },
            { "sq_contains", "ContainsKey" }
        };

        public List<string> MappedMethods { get; } = new List<string>();

        internal DictionaryObject(Type tp) : base(tp)
        {
            if (!tp.IsDictionary())
            {
                throw new ArgumentException("object is not a dict");
            }

            foreach (var name in methodMap)
            {
                var key = Tuple.Create(type, name.Value);
                MethodInfo method;
                if (!methodsByType.TryGetValue(key, out method))
                {
                    method = tp.GetMethod(name.Value);
                    if (method == null)
                    {
                        method = tp.GetMethod($"get_{name.Value}");
                    }
                    if (method == null)
                    {
                        continue;
                    }
                    methodsByType.Add(key, method);
                }

                MappedMethods.Add(name.Key);
            }
        }

        internal override bool CanSubclass() => false;

        /// <summary>
        /// Implements __len__ for dictionary types.
        /// </summary>
        public static int mp_length(IntPtr ob)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            var self = obj.inst;

            MethodInfo methodInfo;
            if (!TryGetMethodInfo(self.GetType(), "Count", out methodInfo))
            {
                return 0;
            }

            return (int)methodInfo.Invoke(self, null);
        }

        /// <summary>
        /// Implements __contains__ for dictionary types.
        /// </summary>
        public static int sq_contains(IntPtr ob, IntPtr v)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            var self = obj.inst;

            MethodInfo methodInfo;
            if (!TryGetMethodInfo(self.GetType(), "ContainsKey", out methodInfo))
            {
                return 0;
            }

            var parameters = methodInfo.GetParameters();
            object arg;
            if (!Converter.ToManaged(v, parameters[0].ParameterType, out arg, false))
            {
                Exceptions.SetError(Exceptions.TypeError,
                    $"invalid parameter type for sq_contains: should be {Converter.GetTypeByAlias(v)}, found {parameters[0].ParameterType}");
            }

            return (bool)methodInfo.Invoke(self, new[] { arg }) ? 1 : 0;
        }

        private static bool TryGetMethodInfo(Type type, string alias, out MethodInfo methodInfo)
        {
            var key = Tuple.Create(type, alias);

            if (!methodsByType.TryGetValue(key, out methodInfo))
            {
                Exceptions.SetError(Exceptions.TypeError,
                    $"{nameof(type)} does not define {alias} method");

                return false;
            }

            return true;
        }
    }

    public static class DictionaryObjectExtension
    {
        public static bool IsDictionary(this Type type)
        {
            var iEnumerableType = typeof(IEnumerable<>);
            var keyValuePairType = typeof(KeyValuePair<,>);

            var interfaces = type.GetInterfaces();
            foreach (var i in interfaces)
            {
                if (i.IsGenericType &&
                    i.GetGenericTypeDefinition() == iEnumerableType)
                {
                    var arguments = i.GetGenericArguments();
                    if (arguments.Length != 1) continue;

                    var a = arguments[0];
                    if (a.IsGenericType &&
                        a.GetGenericTypeDefinition() == keyValuePairType &&
                        a.GetGenericArguments().Length == 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
