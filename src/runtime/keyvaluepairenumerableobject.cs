using System;
using System.Collections.Generic;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type for managed KeyValuePairEnumerable (dictionaries).
    /// This type is essentially the same as a ClassObject, except that it provides
    /// sequence semantics to support natural dictionary usage (__contains__ and __len__)
    /// from Python.
    /// </summary>
    internal class KeyValuePairEnumerableObject : ClassObject
    {
        private static Dictionary<Tuple<Type, string>, MethodInfo> methodsByType = new Dictionary<Tuple<Type, string>, MethodInfo>();
        private static Dictionary<string, string> methodMap = new Dictionary<string, string>
        {
            { "mp_length", "Count" },
            { "sq_contains", "ContainsKey" }
        };

        public List<string> MappedMethods { get; } = new List<string>();

        internal KeyValuePairEnumerableObject(Type tp) : base(tp)
        {
            if (!tp.IsKeyValuePairEnumerable())
            {
                throw new ArgumentException("object is not a KeyValuePair Enumerable");
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

            var key = Tuple.Create(self.GetType(), "Count");
            var methodInfo = methodsByType[key];

            return (int)methodInfo.Invoke(self, null);
        }

        /// <summary>
        /// Implements __contains__ for dictionary types.
        /// </summary>
        public static int sq_contains(IntPtr ob, IntPtr v)
        {
            var obj = (CLRObject)GetManagedObject(ob);
            var self = obj.inst;

            var key = Tuple.Create(self.GetType(), "ContainsKey");
            var methodInfo = methodsByType[key];

            var parameters = methodInfo.GetParameters();
            object arg;
            if (!Converter.ToManaged(v, parameters[0].ParameterType, out arg, false))
            {
                Exceptions.SetError(Exceptions.TypeError,
                    $"invalid parameter type for sq_contains: should be {Converter.GetTypeByAlias(v)}, found {parameters[0].ParameterType}");
            }

            return (bool)methodInfo.Invoke(self, new[] { arg }) ? 1 : 0;
        }
    }

    public static class KeyValuePairEnumerableObjectExtension
    {
        public static bool IsKeyValuePairEnumerable(this Type type)
        {
            var iEnumerableType = typeof(IEnumerable<>);
            var keyValuePairType = typeof(KeyValuePair<,>);
            var requiredMethods = new[] { "ContainsKey", "Count" };

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
                        foreach (var requiredMethod in requiredMethods)
                        {
                            var method = type.GetMethod(requiredMethod);
                            if (method == null)
                            {
                                method = type.GetMethod($"get_{requiredMethod}");
                                if (method == null)
                                {
                                    return false;
                                }
                            }
                        }

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
