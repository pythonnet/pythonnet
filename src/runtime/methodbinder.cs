using System;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Python.Runtime
{
    /// <summary>
    /// A MethodBinder encapsulates information about a (possibly overloaded)
    /// managed method, and is responsible for selecting the right method given
    /// a set of Python arguments. This is also used as a base class for the
    /// ConstructorBinder, a minor variation used to invoke constructors.
    /// </summary>
    [Serializable]
    internal class MethodBinder
    {
        public ArrayList list;
        public MethodBase[] methods;
        public bool init = false;
        public bool allow_threads = true;

        internal MethodBinder()
        {
            list = new ArrayList();
        }

        internal MethodBinder(MethodInfo mi)
        {
            list = new ArrayList { mi };
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


        /// <summary>
        /// Given a sequence of MethodInfo and two sequences of type parameters,
        /// return the MethodInfo that matches the signature and the closed generic.
        /// </summary>
        internal static MethodInfo MatchSignatureAndParameters(MethodInfo[] mi, Type[] genericTp, Type[] sigTp)
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
                    if (sigTp[n] != pi[n].ParameterType)
                    {
                        break;
                    }
                    if (n == pi.Length - 1)
                    {
                        MethodInfo match = t;
                        if (match.IsGenericMethodDefinition)
                        {
                            // FIXME: typeArgs not used
                            Type[] typeArgs = match.GetGenericArguments();
                            return match.MakeGenericMethod(genericTp);
                        }
                        return match;
                    }
                }
            }
            return null;
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
                methods = (MethodBase[])list.ToArray(typeof(MethodBase));
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

                case TypeCode.UInt32:
                    return 11;

                case TypeCode.UInt16:
                    return 12;

                case TypeCode.Int64:
                    return 13;

                case TypeCode.Int32:
                    return 14;

                case TypeCode.Int16:
                    return 15;

                case TypeCode.Char:
                    return 16;

                case TypeCode.SByte:
                    return 17;

                case TypeCode.Byte:
                    return 18;

                case TypeCode.Single:
                    return 20;

                case TypeCode.Double:
                    return 21;

                case TypeCode.String:
                    return 30;

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

        private readonly struct MatchedMethod {
            public MatchedMethod(int kwargsMatched, int defaultsNeeded, object[] margs, int outs, MethodBase mb)
            {
                KwargsMatched = kwargsMatched;
                DefaultsNeeded = defaultsNeeded;
                ManagedArgs = margs;
                Outs = outs;
                Method = mb;
            }

            public int KwargsMatched { get; }
            public int DefaultsNeeded { get; }
            public object[] ManagedArgs { get; }
            public int Outs { get; }
            public MethodBase Method { get; }
        }

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
            // loop to find match, return invoker w/ or /wo error
            MethodBase[] _methods = null;

            var kwargDict = new Dictionary<string, IntPtr>();
            if (kw != IntPtr.Zero)
            {
                var pynkwargs = (int)Runtime.PyDict_Size(kw);
                IntPtr keylist = Runtime.PyDict_Keys(kw);
                IntPtr valueList = Runtime.PyDict_Values(kw);
                for (int i = 0; i < pynkwargs; ++i)
                {
                    var keyStr = Runtime.GetManagedString(Runtime.PyList_GetItem(new BorrowedReference(keylist), i));
                    kwargDict[keyStr] = Runtime.PyList_GetItem(new BorrowedReference(valueList), i).DangerousGetAddress();
                }
                Runtime.XDecref(keylist);
                Runtime.XDecref(valueList);
            }

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

            var argMatchedMethods = new List<MatchedMethod>(_methods.Length);

            // TODO: Clean up
            foreach (MethodBase mi in _methods)
            {
                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }
                ParameterInfo[] pi = mi.GetParameters();
                ArrayList defaultArgList;
                bool paramsArray;
                int kwargsMatched;
                int defaultsNeeded;
                bool isOperator = OperatorMethod.IsOperatorMethod(mi);  // e.g. op_Addition is defined for OperableObject
                if (!MatchesArgumentCount(pynargs, pi, kwargDict, isOperator, out paramsArray, out defaultArgList, out kwargsMatched, out defaultsNeeded))
                {
                    continue;
                }
                var outs = 0;
                int clrnargs = pi.Length;
                isOperator = isOperator && pynargs == clrnargs - 1;  // Handle mismatched arg numbers due to Python operator being bound.
                var margs = TryConvertArguments(pi, paramsArray, args, pynargs, kwargDict, defaultArgList,
                    needsResolution: _methods.Length > 1,  // If there's more than one possible match.
                    isOperator: isOperator,
                    outs: out outs);
                if (isOperator)
                {
                    if (inst != IntPtr.Zero)
                    {
                        var co = ManagedType.GetManagedObject(inst) as CLRObject;
                        if (co == null)
                        {
                            break;
                        }
                        margs[0] = co.inst;
                    }
                }

                if (margs == null)
                {
                    continue;
                }

                var matchedMethod = new MatchedMethod(kwargsMatched, defaultsNeeded, margs, outs, mi);
                argMatchedMethods.Add(matchedMethod);
            }
            if (argMatchedMethods.Count > 0)
            {
                var bestKwargMatchCount = argMatchedMethods.Max(x => x.KwargsMatched);
                var fewestDefaultsRequired = argMatchedMethods.Where(x => x.KwargsMatched == bestKwargMatchCount).Min(x => x.DefaultsNeeded);

                int bestCount = 0;
                int bestMatchIndex = -1;

                for (int index = 0; index < argMatchedMethods.Count; index++)
                {
                    var testMatch = argMatchedMethods[index];
                    if (testMatch.DefaultsNeeded == fewestDefaultsRequired && testMatch.KwargsMatched == bestKwargMatchCount)
                    {
                        bestCount++;
                        if (bestMatchIndex == -1)
                            bestMatchIndex = index;
                    }
                }

                if (bestCount > 1 && fewestDefaultsRequired > 0)
                {
                    // Best effort for determining method to match on gives multiple possible
                    // matches and we need at least one default argument - bail from this point
                    return null;
                }

                // If we're here either:
                //      (a) There is only one best match
                //      (b) There are multiple best matches but none of them require
                //          default arguments
                // in the case of (a) we're done by default. For (b) regardless of which
                // method we choose, all arguments are specified _and_ can be converted
                // from python to C# so picking any will suffice
                MatchedMethod bestMatch = argMatchedMethods[bestMatchIndex];
                var margs = bestMatch.ManagedArgs;
                var outs = bestMatch.Outs;
                var mi = bestMatch.Method;

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

        static IntPtr HandleParamsArray(IntPtr args, int arrayStart, int pyArgCount, out bool isNewReference)
        {
            isNewReference = false;
            IntPtr op;
            // for a params method, we may have a sequence or single/multiple items
            // here we look to see if the item at the paramIndex is there or not
            // and then if it is a sequence itself.
            if ((pyArgCount - arrayStart) == 1)
            {
                // we only have one argument left, so we need to check it
                // to see if it is a sequence or a single item
                IntPtr item = Runtime.PyTuple_GetItem(args, arrayStart);
                if (!Runtime.PyString_Check(item) && Runtime.PySequence_Check(item))
                {
                    // it's a sequence (and not a string), so we use it as the op
                    op = item;
                }
                else
                {
                    isNewReference = true;
                    op = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
                }
            }
            else
            {
                isNewReference = true;
                op = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
            }
            return op;
        }

        /// <summary>
        /// Attempts to convert Python positional argument tuple and keyword argument table
        /// into an array of managed objects, that can be passed to a method.
        /// </summary>
        /// <param name="pi">Information about expected parameters</param>
        /// <param name="paramsArray"><c>true</c>, if the last parameter is a params array.</param>
        /// <param name="args">A pointer to the Python argument tuple</param>
        /// <param name="pyArgCount">Number of arguments, passed by Python</param>
        /// <param name="kwargDict">Dictionary of keyword argument name to python object pointer</param>
        /// <param name="defaultArgList">A list of default values for omitted parameters</param>
        /// <param name="needsResolution"><c>true</c>, if overloading resolution is required</param>
        /// <param name="isOperator"><c>true</c>, if is operator method</param>
        /// <param name="outs">Returns number of output parameters</param>
        /// <returns>An array of .NET arguments, that can be passed to a method.</returns>
        static object[] TryConvertArguments(ParameterInfo[] pi, bool paramsArray,
            IntPtr args, int pyArgCount,
            Dictionary<string, IntPtr> kwargDict,
            ArrayList defaultArgList,
            bool needsResolution,
            bool isOperator,
            out int outs)
        {
            outs = 0;
            var margs = new object[pi.Length];
            int arrayStart = paramsArray ? pi.Length - 1 : -1;

            for (int paramIndex = 0; paramIndex < pi.Length; paramIndex++)
            {
                var parameter = pi[paramIndex];
                bool hasNamedParam = kwargDict.ContainsKey(parameter.Name);
                bool isNewReference = false;

                if (paramIndex >= pyArgCount && !(hasNamedParam || (paramsArray && paramIndex == arrayStart)))
                {
                    if (defaultArgList != null)
                    {
                        margs[paramIndex] = defaultArgList[paramIndex - pyArgCount];
                    }

                    continue;
                }

                IntPtr op;
                if (hasNamedParam)
                {
                    op = kwargDict[parameter.Name];
                }
                else
                {
                    if(arrayStart == paramIndex)
                    {
                        op = HandleParamsArray(args, arrayStart, pyArgCount, out isNewReference);
                    }
                    else
                    {
                        op = Runtime.PyTuple_GetItem(args, paramIndex);
                    }
                }
                if (isOperator && paramIndex == 0)
                {
                    // After we've obtained the first argument from Python, we need to skip the first argument of the CLR
                    // because operator method is a bound method in Python
                    paramIndex++; // Leave the first .NET param as null (margs).
                    parameter = pi[paramIndex];
                }

                bool isOut;
                if (!TryConvertArgument(op, parameter.ParameterType, needsResolution, out margs[paramIndex], out isOut))
                {
                    return null;
                }

                if (isNewReference)
                {
                    // TODO: is this a bug? Should this happen even if the conversion fails?
                    // GetSlice() creates a new reference but GetItem()
                    // returns only a borrow reference.
                    Runtime.XDecref(op);
                }

                if (isOut)
                {
                    outs++;
                }
            }

            return margs;
        }

        /// <summary>
        /// Try to convert a Python argument object to a managed CLR type.
        /// </summary>
        /// <param name="op">Pointer to the object at a particular parameter.</param>
        /// <param name="parameterType">That parameter's managed type.</param>
        /// <param name="needsResolution">There are multiple overloading methods that need resolution.</param>
        /// <param name="arg">Converted argument.</param>
        /// <param name="isOut">Whether the CLR type is passed by reference.</param>
        /// <returns></returns>
        static bool TryConvertArgument(IntPtr op, Type parameterType, bool needsResolution,
                                       out object arg, out bool isOut)
        {
            arg = null;
            isOut = false;
            var clrtype = TryComputeClrArgumentType(parameterType, op, needsResolution: needsResolution);
            if (clrtype == null)
            {
                return false;
            }

            if (!Converter.ToManaged(op, clrtype, out arg, false))
            {
                Exceptions.Clear();
                return false;
            }

            isOut = clrtype.IsByRef;
            return true;
        }

        static Type TryComputeClrArgumentType(Type parameterType, IntPtr argument, bool needsResolution)
        {
            // this logic below handles cases when multiple overloading methods
            // are ambiguous, hence comparison between Python and CLR types
            // is necessary
            Type clrtype = null;
            IntPtr pyoptype;
            if (needsResolution)
            {
                // HACK: each overload should be weighted in some way instead
                pyoptype = Runtime.PyObject_Type(argument);
                Exceptions.Clear();
                if (pyoptype != IntPtr.Zero)
                {
                    clrtype = Converter.GetTypeByAlias(pyoptype);
                }
                Runtime.XDecref(pyoptype);
            }

            if (clrtype != null)
            {
                var typematch = false;
                if ((parameterType != typeof(object)) && (parameterType != clrtype))
                {
                    IntPtr pytype = Converter.GetPythonTypeByAlias(parameterType);
                    pyoptype = Runtime.PyObject_Type(argument);
                    Exceptions.Clear();
                    if (pyoptype != IntPtr.Zero)
                    {
                        if (pytype != pyoptype)
                        {
                            typematch = false;
                        }
                        else
                        {
                            typematch = true;
                            clrtype = parameterType;
                        }
                    }
                    if (!typematch)
                    {
                        // this takes care of enum values
                        TypeCode argtypecode = Type.GetTypeCode(parameterType);
                        TypeCode paramtypecode = Type.GetTypeCode(clrtype);
                        if (argtypecode == paramtypecode)
                        {
                            typematch = true;
                            clrtype = parameterType;
                        }
                    }
                    Runtime.XDecref(pyoptype);
                    if (!typematch)
                    {
                        return null;
                    }
                }
                else
                {
                    typematch = true;
                    clrtype = parameterType;
                }
            }
            else
            {
                clrtype = parameterType;
            }

            return clrtype;
        }
        /// <summary>
        /// Check whether the number of Python and .NET arguments match, and compute additional arg information.
        /// </summary>
        /// <param name="pynargs">Number of positional args passed from Python.</param>
        /// <param name="parameters">Parameters of the specified .NET method.</param>
        /// <param name="kwargDict">Keyword args passed from Python.</param>
        /// <param name="isOperator">True if the parameters' method is an operator.</param>
        /// <param name="paramsArray">True if the final param of the .NET method is an array (`params` keyword).</param>
        /// <param name="defaultArgList">List of default values for arguments.</param>
        /// <param name="kwargsMatched">Number of kwargs from Python that are also present in the .NET method.</param>
        /// <param name="defaultsNeeded">Number of non-null defaultsArgs.</param>
        /// <returns></returns>
        static bool MatchesArgumentCount(int pynargs, ParameterInfo[] parameters,
            Dictionary<string, IntPtr> kwargDict,
            bool isOperator,
            out bool paramsArray,
            out ArrayList defaultArgList,
            out int kwargsMatched,
            out int defaultsNeeded)
        {
            defaultArgList = null;
            var match = false;
            int clrnargs = parameters.Length;
            paramsArray = clrnargs > 0 ? Attribute.IsDefined(parameters[clrnargs - 1], typeof(ParamArrayAttribute)) : false;
            var kwargCount = kwargDict.Count;
            kwargsMatched = 0;
            defaultsNeeded = 0;
            if (pynargs == clrnargs && kwargDict.Count == 0)
            {
                match = true;
            }
            else if (pynargs < clrnargs && (!paramsArray || pynargs == clrnargs - 1))
            {
                // every parameter past 'positionalArgumentCount' must have either
                // a corresponding keyword argument or a default parameter, unless
                // the method is an operator or accepts a params array.
                match = true;
                defaultArgList = new ArrayList();
                for (var v = pynargs; v < clrnargs; v++)
                {
                    if (kwargDict.ContainsKey(parameters[v].Name))
                    {
                        // we have a keyword argument for this parameter,
                        // no need to check for a default parameter, but put a null
                        // placeholder in defaultArgList
                        defaultArgList.Add(null);
                        kwargsMatched++;
                    }
                    else if (parameters[v].IsOptional)
                    {
                        // IsOptional will be true if the parameter has a default value,
                        // or if the parameter has the [Optional] attribute specified.
                        // The GetDefaultValue() extension method will return the value
                        // to be passed in as the parameter value
                        defaultArgList.Add(parameters[v].GetDefaultValue());
                        defaultsNeeded++;
                    }
                    else if (!isOperator && !paramsArray)
                    {
                        match = false;
                    }
                }
                if (isOperator && defaultArgList.Count == 0)
                {
                    // If no default arguments were provided for an operable object.
                    defaultArgList = null;
                }
            }
            else if (pynargs > clrnargs && clrnargs > 0 &&
                       Attribute.IsDefined(parameters[clrnargs - 1], typeof(ParamArrayAttribute)))
            {
                // This is a `foo(params object[] bar)` style method
                match = true;
                paramsArray = true;
            }

            return match;
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw)
        {
            return Invoke(inst, args, kw, null, null);
        }

        internal virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info)
        {
            return Invoke(inst, args, kw, info, null);
        }

        protected static void AppendArgumentTypes(StringBuilder to, IntPtr args)
        {
            long argCount = Runtime.PyTuple_Size(args);
            to.Append("(");
            for (long argIndex = 0; argIndex < argCount; argIndex++)
            {
                var arg = Runtime.PyTuple_GetItem(args, argIndex);
                if (arg != IntPtr.Zero)
                {
                    var type = Runtime.PyObject_Type(arg);
                    if (type != IntPtr.Zero)
                    {
                        try
                        {
                            var description = Runtime.PyObject_Unicode(type);
                            if (description != IntPtr.Zero)
                            {
                                to.Append(Runtime.GetManagedString(description));
                                Runtime.XDecref(description);
                            }
                        }
                        finally
                        {
                            Runtime.XDecref(type);
                        }
                    }
                }

                if (argIndex + 1 < argCount)
                    to.Append(", ");
            }
            to.Append(')');
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

                value.Append(": ");
                AppendArgumentTypes(to: value, args);
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
                        v = Converter.ToPython(binding.args[i], pt.GetElementType());
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
    }


    /// <summary>
    /// Utility class to sort method info by parameter type precedence.
    /// </summary>
    internal class MethodSorter : IComparer
    {
        int IComparer.Compare(object m1, object m2)
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


    static internal class ParameterInfoExtensions
    {
        public static object GetDefaultValue(this ParameterInfo parameterInfo)
        {
            // parameterInfo.HasDefaultValue is preferable but doesn't exist in .NET 4.0
            bool hasDefaultValue = (parameterInfo.Attributes & ParameterAttributes.HasDefault) ==
                ParameterAttributes.HasDefault;

            if (hasDefaultValue)
            {
                return parameterInfo.DefaultValue;
            }
            else
            {
                // [OptionalAttribute] was specified for the parameter.
                // See https://stackoverflow.com/questions/3416216/optionalattribute-parameters-default-value
                // for rules on determining the value to pass to the parameter
                var type = parameterInfo.ParameterType;
                if (type == typeof(object))
                    return Type.Missing;
                else if (type.IsValueType)
                    return Activator.CreateInstance(type);
                else
                    return null;
            }
        }
    }
}
