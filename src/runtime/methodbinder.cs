using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// A MethodBinder encapsulates information about a (possibly overloaded)
    /// managed method, and is responsible for selecting the right method given
    /// a set of Python arguments. This is also used as a base class for the
    /// ConstructorBinder, a minor variation used to invoke constructors.
    /// </summary>
    internal class MethodBinder
    {
        public List<MethodBase> list;
        public MethodBase[] methods;
        public bool init = false;
        public bool allow_threads = true;

        private readonly Dictionary<MethodBase, object[]> _defualtArgs = new Dictionary<MethodBase, object[]>();

        private enum MethodMatchType
        {
            NotDefined = 0,
            Normal,
            Operator,
            WithDefaultArgs,
            WithParamArray,
            WithDefaultAndParamArray,
        }

        internal MethodBinder()
        {
            list = new List<MethodBase>();
        }

        internal MethodBinder(MethodInfo mi)
        {
            list = new List<MethodBase>() { mi };
        }

        public int Count
        {
            get { return list.Count; }
        }

        internal void AddMethod(MethodBase m)
        {
            list.Add(m);
        }

        /// <summary>
        /// Given a sequence of MethodInfo and a sequence of types, return the
        /// MethodInfo that matches the signature represented by those types.
        /// </summary>
        internal static MethodInfo MatchSignature(MethodInfo[] mi, Type[] tp)
        {
            if (tp == null)
            {
                return null;
            }
            int count = tp.Length;
            foreach (MethodInfo t in mi)
            {
                ParameterInfo[] pi = t.GetParameters();
                if (pi.Length != count)
                {
                    continue;
                }
                for (var n = 0; n < pi.Length; n++)
                {
                    if (tp[n] != pi[n].ParameterType)
                    {
                        break;
                    }
                    if (n == pi.Length - 1)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Given a sequence of MethodInfo and a sequence of type parameters,
        /// return the MethodInfo that represents the matching closed generic.
        /// </summary>
        internal static MethodInfo MatchParameters(MethodInfo[] mi, Type[] tp)
        {
            if (tp == null)
            {
                return null;
            }
            int count = tp.Length;
            foreach (MethodInfo t in mi)
            {
                if (!t.IsGenericMethodDefinition)
                {
                    continue;
                }
                Type[] args = t.GetGenericArguments();
                if (args.Length != count)
                {
                    continue;
                }
                return t.MakeGenericMethod(tp);
            }
            return null;
        }

        internal static IEnumerable<MethodInfo> MatchParamertersMethods(IEnumerable<MethodInfo> mi, Type[] tp)
        {
            if (tp == null)
            {
                yield break;
            }
            int count = tp.Length;
            foreach (MethodInfo t in mi)
            {
                if (!t.IsGenericMethodDefinition)
                {
                    continue;
                }
                Type[] args = t.GetGenericArguments();
                if (args.Length != count)
                {
                    continue;
                }

                MethodInfo method;
                try
                {
                    method = t.MakeGenericMethod(tp);
                }
                catch (ArgumentException)
                {
                    method = null;
                }
                if (method == null)
                {
                    continue;
                }
                yield return method;
            }
        }

        /// <summary>
        /// Given a sequence of MethodInfo and two sequences of type parameters,
        /// return the MethodInfo that matches the signature and the closed generic.
        /// </summary>
        internal static MethodInfo MatchSignatureAndParameters(IEnumerable<MethodInfo> mi, Type[] genericTp, Type[] sigTp)
        {
            if (genericTp == null || sigTp == null)
            {
                return null;
            }
            int genericCount = genericTp.Length;
            int signatureCount = sigTp.Length;
            foreach (MethodInfo t in mi)
            {
                if (!t.IsGenericMethodDefinition)
                {
                    continue;
                }
                Type[] genericArgs = t.GetGenericArguments();
                if (genericArgs.Length != genericCount)
                {
                    continue;
                }
                ParameterInfo[] pi = t.GetParameters();
                if (pi.Length != signatureCount)
                {
                    continue;
                }

                for (var n = 0; n < pi.Length; n++)
                {
                    Type sig = sigTp[n];
                    Type param = pi[n].ParameterType;

                    if (!param.IsGenericParameter && !IsNullableOf(sig, param) &&
                        !param.IsAssignableFrom(sig))
                    {
                        break;
                    }
                    if (n == pi.Length - 1)
                    {
                        MethodInfo match = t;
                        if (match.IsGenericMethodDefinition)
                        {
                            try
                            {
                                return match.MakeGenericMethod(genericTp);
                            }
                            catch (ArgumentException)
                            {
                                continue;
                            }
                        }
                        return match;
                    }
                }
            }
            return null;
        }

        private static bool IsNullableOf(Type sigType, Type target)
        {
            if (!sigType.IsValueType || !target.IsValueType)
            {
                return false;
            }
            if (target != typeof(Nullable<>))
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// Return the array of MethodInfo for this method. The result array
        /// is arranged in order of precedence (done lazily to avoid doing it
        /// at all for methods that are never called).
        /// </summary>
        internal MethodBase[] GetMethods()
        {
            if (!init)
            {
                // I'm sure this could be made more efficient.
                list.Sort(new MethodSorter());
                methods = list.ToArray();
                init = true;
            }
            return methods;
        }

        /// <summary>
        /// Precedence algorithm largely lifted from Jython - the concerns are
        /// generally the same so we'll start with this and tweak as necessary.
        /// </summary>
        /// <remarks>
        /// Based from Jython `org.python.core.ReflectedArgs.precedence`
        /// See: https://github.com/jythontools/jython/blob/master/src/org/python/core/ReflectedArgs.java#L192
        /// </remarks>
        internal static int GetPrecedence(MethodBase mi)
        {
            ParameterInfo[] pi = mi.GetParameters();
            int val = mi.IsStatic ? 3000 : 0;
            int num = pi.Length;

            val += mi.IsGenericMethod ? 1 : 0;
            for (var i = 0; i < num; i++)
            {
                val += ArgPrecedence(pi[i].ParameterType);
            }

            return val;
        }

        /// <summary>
        /// Return a precedence value for a particular Type object.
        /// </summary>
        internal static int ArgPrecedence(Type t)
        {
            Type objectType = typeof(object);
            if (t == objectType)
            {
                return 3000;
            }

            // Due to array type must be a object, "IsArray" should check first.
            if (t.IsArray)
            {
                Type e = t.GetElementType();
                if (e == objectType)
                {
                    return 2500;
                }
                return 100 + ArgPrecedence(e);
            }

            TypeCode tc = Type.GetTypeCode(t);
            // TODO: Clean up
            switch (tc)
            {
                case TypeCode.Object:
                    return 1;

                case TypeCode.UInt64:
                    return 10;

                case TypeCode.Int64:
                    return 11;

                case TypeCode.UInt32:
                    return 12;

                case TypeCode.Int32:
                    return 13;

                case TypeCode.UInt16:
                    return 14;

                case TypeCode.Int16:
                    return 15;

                case TypeCode.SByte:
                    return 17;

                case TypeCode.Byte:
                    return 18;

                case TypeCode.Double:
                    return 20;

                case TypeCode.Single:
                    return 21;

                case TypeCode.String:
                    return 30;

                // A char can be extracted from a string,
                // so 'char' should larger than string.
                case TypeCode.Char:
                    return 31;

                case TypeCode.Boolean:
                    return 40;
            }

            return 2000;
        }

        /// <summary>
        /// Bind the given Python instance and arguments to a particular method
        /// overload and return a structure that contains the converted Python
        /// instance, converted arguments and the correct method to call.
        /// </summary>
        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw)
        {
            return Bind(inst, args, kw, null, null);
        }

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info)
        {
            return Bind(inst, args, kw, info, null);
        }

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
            // loop to find match, return invoker w/ or /wo error
            MethodBase[] _methods = null;
            var pynargs = (int)Runtime.PyTuple_Size(args);
            var isGeneric = false;
            if (info != null)
            {
                _methods = new MethodBase[1];
                _methods.SetValue(info, 0);
            }
            else
            {
                _methods = GetMethods();
            }

            // TODO: Clean up
            bool hasOverloads = _methods.Length > 1;
            foreach (MethodBase mi in _methods)
            {
                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }

                int outs;
                var margs = GetInvokeArguments(inst, args, mi, pynargs, hasOverloads, out outs);
                if (margs == null)
                {
                    continue;
                }
                object target = null;
                if (!mi.IsStatic && inst != IntPtr.Zero)
                {
                    //CLRObject co = (CLRObject)ManagedType.GetManagedObject(inst);
                    // InvalidCastException: Unable to cast object of type
                    // 'Python.Runtime.ClassObject' to type 'Python.Runtime.CLRObject'
                    var co = ManagedType.GetManagedObject(inst) as CLRObject;

                    // Sanity check: this ensures a graceful exit if someone does
                    // something intentionally wrong like call a non-static method
                    // on the class rather than on an instance of the class.
                    // XXX maybe better to do this before all the other rigmarole.
                    if (co == null)
                    {
                        return null;
                    }
                    target = co.inst;
                }

                return new Binding(mi, target, margs, outs);
            }
            // We weren't able to find a matching method but at least one
            // is a generic method and info is null. That happens when a generic
            // method was not called using the [] syntax. Let's introspect the
            // type of the arguments and use it to construct the correct method.
            if (isGeneric && info == null && methodinfo != null)
            {
                Type[] types = Runtime.PythonArgsToTypeArray(args, true);
                MethodInfo mi = MatchParameters(methodinfo, types);
                return Bind(inst, args, kw, mi, null);
            }
            return null;
        }

        private static bool ExtractArgument(IntPtr op, Type clrType,
            bool hasOverload, ref object clrArg)
        {
            // this logic below handles cases when multiple overloading methods
            // are ambiguous, hence comparison between Python and CLR types
            // is necessary
            if (hasOverload && !IsMatchedClrType(op, clrType))
            {
                return false;
            }
            if (!Converter.ToManaged(op, clrType, out clrArg, false))
            {
                Exceptions.Clear();
                return false;
            }
            return true;
        }

        private static bool IsMatchedClrType(IntPtr op, Type targetType)
        {
            IntPtr pyoptype = Runtime.PyObject_TYPE(op);
            Debug.Assert(op != IntPtr.Zero && !Exceptions.ErrorOccurred());
            Type clrtype = Converter.GetTypeByAlias(pyoptype);
            if (clrtype == null)
            {
                // Not a basic builtin type, pass it
                return true;
            }

            if ((targetType != typeof(object)) && (targetType != clrtype))
            {
                IntPtr pytype = Converter.GetPythonTypeByAlias(targetType);
                if (pytype == pyoptype)
                {
                    return true;
                }
                // this takes care of enum values
                TypeCode argtypecode = Type.GetTypeCode(targetType);
                TypeCode paramtypecode = Type.GetTypeCode(clrtype);
                if (argtypecode == paramtypecode)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        private object[] GetInvokeArguments(IntPtr inst, IntPtr args, MethodBase mi,
            int pynargs, bool hasOverloads, out int outs)
        {
            ParameterInfo[] pi = mi.GetParameters();
            int clrnargs = pi.Length;
            outs = 0;
            if (clrnargs == 0)
            {
                if (pynargs != 0)
                {
                    return null;
                }
                return new object[0];
            }
            object[] margs = new object[clrnargs];
            if (!GetMultiInvokeArguments(inst, args, pynargs, mi, pi, hasOverloads, margs, ref outs))
            {
                return null;
            }
            return margs;
        }

        private bool GetMultiInvokeArguments(IntPtr inst, IntPtr args, int pynargs,
            MethodBase mi, ParameterInfo[] pi,
            bool hasOverloads, object[] margs, ref int outs)
        {
            int clrnargs = pi.Length;
            Debug.Assert(clrnargs > 0);
            bool isOperator = OperatorMethod.IsOperatorMethod(mi);
            Type lastType = pi[clrnargs - 1].ParameterType;
            bool hasArrayArgs = clrnargs > 0 &&
                                lastType.IsArray &&
                                pi[clrnargs - 1].IsDefined(typeof(ParamArrayAttribute), false);

            int fixedCnt = 0;
            int fixedStart = 0;
            MethodMatchType matchType;

            if (!hasArrayArgs)
            {
                if (pynargs == clrnargs)
                {
                    fixedCnt = clrnargs;
                    matchType = MethodMatchType.Normal;
                }
                else if (isOperator && pynargs == clrnargs - 1)
                {
                    // We need to skip the first argument
                    // cause of operator method is a bound method in Python
                    fixedStart = inst != IntPtr.Zero ? 1 : 0;
                    fixedCnt = clrnargs - 1;
                    matchType = MethodMatchType.Operator;
                }
                else if (pynargs < clrnargs)
                {
                    // Not included `foo(int x = 0, params object[] bar)`
                    object[] defaultArgList = GetDefualtArgs(mi);
                    if (defaultArgList[pynargs] == DBNull.Value)
                    {
                        return false;
                    }
                    fixedCnt = pynargs;
                    matchType = MethodMatchType.WithDefaultArgs;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                Debug.Assert(!isOperator);
                if (pynargs == clrnargs - 1)
                {
                    fixedCnt = clrnargs - 1;
                    matchType = MethodMatchType.Normal;
                }
                else if (pynargs < clrnargs - 1)
                {
                    // Included `foo(int x = 0, params object[] bar)`
                    if ((pi[pynargs].Attributes & ParameterAttributes.HasDefault) == 0)
                    {
                        return false;
                    }
                    fixedCnt = pynargs;
                    matchType = MethodMatchType.WithDefaultArgs;
                }
                else
                {
                    // This is a `foo(params object[] bar)` style method
                    // Included `foo(int x = 0, params object[] bar)`
                    fixedCnt = clrnargs - 1;
                    matchType = MethodMatchType.WithParamArray;
                }
            }

            for (int i = 0; i < fixedCnt; i++)
            {
                int fixedIdx = i + fixedStart;
                ParameterInfo param = pi[fixedIdx];
                Type clrType = param.ParameterType;
                if (i >= pynargs)
                {
                    return false;
                }

                IntPtr op = Runtime.PyTuple_GetItem(args, i);
                if (!ExtractArgument(op, clrType, hasOverloads, ref margs[fixedIdx]))
                {
                    return false;
                }

                if (param.IsOut || clrType.IsByRef)
                {
                    outs++;
                }
            }

            switch (matchType)
            {
                case MethodMatchType.Normal:
                    if (hasArrayArgs)
                    {
                        margs[clrnargs - 1] = Array.CreateInstance(lastType.GetElementType(), 0);
                    }
                    break;

                case MethodMatchType.Operator:
                    if (inst != IntPtr.Zero)
                    {
                        var co = ManagedType.GetManagedObject(inst) as CLRObject;
                        if (co == null)
                        {
                            return false;
                        }
                        margs[0] = co.inst;
                    }
                    break;

                case MethodMatchType.WithDefaultArgs:
                    object[] defaultArgList = GetDefualtArgs(mi);
                    Debug.Assert(defaultArgList != null);
                    int argCnt = hasArrayArgs ? clrnargs - 1 : clrnargs;
                    for (int i = fixedCnt; i < argCnt; i++)
                    {
                        margs[i] = defaultArgList[i];
                    }

                    if (hasArrayArgs)
                    {
                        margs[clrnargs - 1] = Array.CreateInstance(lastType.GetElementType(), 0);
                    }
                    break;

                case MethodMatchType.WithParamArray:
                    if (pynargs <= clrnargs - 1)
                    {
                        break;
                    }

                    IntPtr op;
                    bool sliced;
                    if (pynargs == 1 && pynargs == clrnargs)
                    {
                        // There is no need for slice
                        op = args;
                        sliced = false;
                    }
                    else
                    {
                        // map remaining Python arguments to a tuple since
                        // the managed function accepts it - hopefully :]
                        op = Runtime.PyTuple_GetSlice(args, clrnargs - 1, pynargs);
                        sliced = true;
                    }
                    try
                    {
                        if (!Converter.ToManaged(op, lastType, out margs[clrnargs - 1], false))
                        {
                            Exceptions.Clear();
                            return false;
                        }
                    }
                    finally
                    {
                        if (sliced) Runtime.XDecref(op);
                    }
                    break;

                default:
                    return false;
            }
            return true;
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw)
        {
            return Invoke(inst, args, kw, null, null);
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info)
        {
            return Invoke(inst, args, kw, info, null);
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
            Binding binding = Bind(inst, args, kw, info, methodinfo);
            object result;
            IntPtr ts = IntPtr.Zero;

            if (binding == null)
            {
                var value = new StringBuilder("No method matches given arguments");
                if (methodinfo != null && methodinfo.Length > 0)
                {
                    value.Append($" for {methodinfo[0].Name}");
                }

                long argCount = Runtime.PyTuple_Size(args);
                value.Append(": (");
                for(long argIndex = 0; argIndex < argCount; argIndex++) {
                    var arg = Runtime.PyTuple_GetItem(args, argIndex);
                    if (arg != IntPtr.Zero) {
                        var type = Runtime.PyObject_Type(arg);
                        if (type != IntPtr.Zero) {
                            try {
                                var description = Runtime.PyObject_Unicode(type);
                                if (description != IntPtr.Zero) {
                                    value.Append(Runtime.GetManagedString(description));
                                    Runtime.XDecref(description);
                                }
                            } finally {
                                Runtime.XDecref(type);
                            }
                        }
                    }

                    if (argIndex + 1 < argCount)
                        value.Append(", ");
                }
                value.Append(')');
                Exceptions.SetError(Exceptions.TypeError, value.ToString());
                return IntPtr.Zero;
            }

            if (allow_threads)
            {
                ts = PythonEngine.BeginAllowThreads();
            }

            try
            {
                result = binding.info.Invoke(binding.inst, BindingFlags.Default, null, binding.args, null);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    e = e.InnerException;
                }
                if (allow_threads)
                {
                    PythonEngine.EndAllowThreads(ts);
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }

            if (allow_threads)
            {
                PythonEngine.EndAllowThreads(ts);
            }

            // If there are out parameters, we return a tuple containing
            // the result followed by the out parameters. If there is only
            // one out parameter and the return type of the method is void,
            // we return the out parameter as the result to Python (for
            // code compatibility with ironpython).

            var mi = (MethodInfo)binding.info;

            if (binding.outs == 1 && mi.ReturnType == typeof(void))
            {
            }

            if (binding.outs > 0)
            {
                ParameterInfo[] pi = mi.GetParameters();
                int c = pi.Length;
                var n = 0;

                IntPtr t = Runtime.PyTuple_New(binding.outs + 1);
                IntPtr v = Converter.ToPython(result, mi.ReturnType);
                Runtime.PyTuple_SetItem(t, n, v);
                n++;

                for (var i = 0; i < c; i++)
                {
                    Type pt = pi[i].ParameterType;
                    if (pi[i].IsOut || pt.IsByRef)
                    {
                        v = Converter.ToPython(binding.args[i], pt);
                        Runtime.PyTuple_SetItem(t, n, v);
                        n++;
                    }
                }

                if (binding.outs == 1 && mi.ReturnType == typeof(void))
                {
                    v = Runtime.PyTuple_GetItem(t, 1);
                    Runtime.XIncref(v);
                    Runtime.XDecref(t);
                    return v;
                }

                return t;
            }

            return Converter.ToPython(result, mi.ReturnType);
        }

        private object[] GetDefualtArgs(MethodBase method)
        {
            object[] args;
            if (_defualtArgs.TryGetValue(method, out args))
            {
                return args;
            }
            var paramsInfo = method.GetParameters();
            args = paramsInfo.Select(T => T.DefaultValue).ToArray();
            _defualtArgs[method] = args;
            return args;
        }
    }


    /// <summary>
    /// Utility class to sort method info by parameter type precedence.
    /// </summary>
    internal class MethodSorter : IComparer<MethodBase>
    {
        public int Compare(MethodBase m1, MethodBase m2)
        {
            var me1 = (MethodBase)m1;
            var me2 = (MethodBase)m2;
            if (me1.DeclaringType != me2.DeclaringType)
            {
                // m2's type derives from m1's type, favor m2
                if (me1.DeclaringType.IsAssignableFrom(me2.DeclaringType))
                    return 1;

                // m1's type derives from m2's type, favor m1
                if (me2.DeclaringType.IsAssignableFrom(me1.DeclaringType))
                    return -1;
            }

            int p1 = MethodBinder.GetPrecedence((MethodBase)m1);
            int p2 = MethodBinder.GetPrecedence((MethodBase)m2);
            if (p1 < p2)
            {
                return -1;
            }
            if (p1 > p2)
            {
                return 1;
            }
            return 0;
        }
    }


    /// <summary>
    /// A Binding is a utility instance that bundles together a MethodInfo
    /// representing a method to call, a (possibly null) target instance for
    /// the call, and the arguments for the call (all as managed values).
    /// </summary>
    internal class Binding
    {
        public MethodBase info;
        public object[] args;
        public object inst;
        public int outs;

        internal Binding(MethodBase info, object inst, object[] args, int outs)
        {
            this.info = info;
            this.inst = inst;
            this.args = args;
            this.outs = outs;
        }
    }
}
