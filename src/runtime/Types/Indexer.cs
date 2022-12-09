using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// Bundles the information required to support an indexer property.
    /// </summary>
    [Serializable]
    internal class Indexer
    {
        /// <summary>
        /// Dictionary that maps dotnet getter setter method names to python counterparts
        /// </summary>
        static Dictionary<string, string> IndexerMethodMap = new Dictionary<string, string>
        {
            ["get_Item"] = "__getitem__",
            ["set_Item"] = "__setitem__",
        };

        /// <summary>
        /// Get property getter or setter method name in python
        /// e.g. Returns Value for get_Value
        /// </summary>
        public static bool TryGetPropertyMethodName(string methodName, out string pyMethodName)
        {
            if (Indexer.IndexerMethodMap.TryGetValue(methodName, out pyMethodName))
                return true;

            // FIXME: enabling this breaks getting Message property in Exception classes
            // if (methodName.StartsWith("get_") || methodName.StartsWith("set_"))
            // {
            //     pyMethodName = methodName.Substring(4);
            //     return true;
            // }

            return false;
        }

        public MethodBinder GetterBinder;
        public MethodBinder SetterBinder;

        public Indexer()
        {
            GetterBinder = new MethodBinder();
            SetterBinder = new MethodBinder();
        }


        public bool CanGet
        {
            get { return GetterBinder.Count > 0; }
        }

        public bool CanSet
        {
            get { return SetterBinder.Count > 0; }
        }


        public void AddProperty(Type type, PropertyInfo pi)
        {
            // NOTE:
            // Ensure to adopt the dynamically generated getter-setter methods
            // if they are available. They are always in a pair of Original-Redirected
            // e.g. _BASEVIRTUAL_get_Item() - get_Item()
            MethodInfo getter = pi.GetGetMethod(true);
            if (getter != null)
            {
                if (ClassDerivedObject.GetOriginalMethod(getter, type) is MethodInfo originalGetter
                        && ClassDerivedObject.GetRedirectedMethod(getter, type) is MethodInfo redirectedGetter)
                {
                    GetterBinder.AddMethod(originalGetter);
                    GetterBinder.AddMethod(redirectedGetter);
                }
                else
                    GetterBinder.AddMethod(getter);
            }

            MethodInfo setter = pi.GetSetMethod(true);
            if (setter != null)
            {
                if (ClassDerivedObject.GetOriginalMethod(setter, type) is MethodInfo originalSetter
                        && ClassDerivedObject.GetRedirectedMethod(setter, type) is MethodInfo redirectedSetter)
                {
                    SetterBinder.AddMethod(originalSetter);
                    SetterBinder.AddMethod(redirectedSetter);
                }
                else
                    SetterBinder.AddMethod(setter);
            }
        }

        internal NewReference GetItem(BorrowedReference inst, BorrowedReference args)
        {
            return GetterBinder.Invoke(inst, args, null);
        }

        internal void SetItem(BorrowedReference inst, BorrowedReference args)
        {
            SetterBinder.Invoke(inst, args, null);
        }

        internal bool NeedsDefaultArgs(BorrowedReference args)
        {
            var pynargs = Runtime.PyTuple_Size(args);
            MethodBase[] methods = SetterBinder.GetMethods();
            if (methods.Length == 0)
            {
                return false;
            }

            MethodBase mi = methods[0];
            ParameterInfo[] pi = mi.GetParameters();
            // need to subtract one for the value
            int clrnargs = pi.Length - 1;
            if (pynargs == clrnargs || pynargs > clrnargs)
            {
                return false;
            }

            for (var v = pynargs; v < clrnargs; v++)
            {
                if (pi[v].DefaultValue == DBNull.Value)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// This will return default arguments a new instance of a tuple. The size
        /// of the tuple will indicate the number of default arguments.
        /// </summary>
        /// <param name="args">This is pointing to the tuple args passed in</param>
        /// <returns>a new instance of the tuple containing the default args</returns>
        internal NewReference GetDefaultArgs(BorrowedReference args)
        {
            // if we don't need default args return empty tuple
            if (!NeedsDefaultArgs(args))
            {
                return Runtime.PyTuple_New(0);
            }
            var pynargs = Runtime.PyTuple_Size(args);

            // Get the default arg tuple
            MethodBase[] methods = SetterBinder.GetMethods();
            MethodBase mi = methods[0];
            ParameterInfo[] pi = mi.GetParameters();
            int clrnargs = pi.Length - 1;
            var defaultArgs = Runtime.PyTuple_New(clrnargs - pynargs);
            for (var i = 0; i < clrnargs - pynargs; i++)
            {
                if (pi[i + pynargs].DefaultValue == DBNull.Value)
                {
                    continue;
                }
                using var arg = Converter.ToPython(pi[i + pynargs].DefaultValue, pi[i + pynargs].ParameterType);
                Runtime.PyTuple_SetItem(defaultArgs.Borrow(), i, arg.Steal());
            }
            return defaultArgs;
        }
    }
}
