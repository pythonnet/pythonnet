using System;
using System.Collections;
using System.Collections.Generic;
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
    [Serializable]
    internal class MethodBinder
    {
        private List<MethodInformation> list;
        public const bool DefaultAllowThreads = true;
        public bool allow_threads = DefaultAllowThreads;
        public bool init = false;

        internal MethodBinder()
        {
            list = new List<MethodInformation>();
        }

        internal MethodBinder(MethodInfo mi)
        {
            list = new List<MethodInformation> { new MethodInformation(mi, mi.GetParameters()) };
        }

        public int Count
        {
            get { return list.Count; }
        }

        internal void AddMethod(MethodBase m)
        {
            // we added a new method so we have to re sort the method list
            init = false;
            list.Add(new MethodInformation(m, m.GetParameters()));
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
                try
                {
                    // MakeGenericMethod can throw ArgumentException if the type parameters do not obey the constraints.
                    MethodInfo method = t.MakeGenericMethod(tp);
                    Exceptions.Clear();
                    return method;
                }
                catch (ArgumentException e)
                {
                    Exceptions.SetError(e);
                    // The error will remain set until cleared by a successful match.
                }
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
        internal List<MethodInformation> GetMethods()
        {
            if (!init)
            {
                // I'm sure this could be made more efficient.
                list.Sort(new MethodSorter());
                init = true;
            }
            return list;
        }

        /// <summary>
        /// Precedence algorithm largely lifted from Jython - the concerns are
        /// generally the same so we'll start with this and tweak as necessary.
        /// </summary>
        /// <remarks>
        /// Based from Jython `org.python.core.ReflectedArgs.precedence`
        /// See: https://github.com/jythontools/jython/blob/master/src/org/python/core/ReflectedArgs.java#L192
        /// </remarks>
        private static int GetPrecedence(MethodInformation methodInformation)
        {
            ParameterInfo[] pi = methodInformation.ParameterInfo;
            var mi = methodInformation.MethodBase;
            int val = mi.IsStatic ? 3000 : 0;
            int num = pi.Length;

            val += mi.IsGenericMethod ? 1 : 0;
            for (var i = 0; i < num; i++)
            {
                val += ArgPrecedence(pi[i].ParameterType);
            }

            var info = mi as MethodInfo;
            if (info != null)
            {
                val += ArgPrecedence(info.ReturnType);
                val += mi.DeclaringType == mi.ReflectedType ? 0 : 3000;
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

            if (t.IsAssignableFrom(typeof(PyObject)))
            {
                return -1;
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

            if (t.IsArray)
            {
                Type e = t.GetElementType();
                if (e == objectType)
                {
                    return 2500;
                }
                return 100 + ArgPrecedence(e);
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
            var pynargs = (int)Runtime.PyTuple_Size(args);
            object arg;
            var isGeneric = false;
            ArrayList defaultArgList;
            Type clrtype;
            Binding bindingUsingImplicitConversion = null;

            var methods = info == null ? GetMethods()
                : new List<MethodInformation>(1) { new MethodInformation(info, info.GetParameters()) };

            // TODO: Clean up
            foreach (var methodInformation in methods)
            {
                var mi = methodInformation.MethodBase;
                var pi = methodInformation.ParameterInfo;

                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }

                bool isOperator = OperatorMethod.IsOperatorMethod(mi);
                // Binary operator methods will have 2 CLR args but only one Python arg
                // (unary operators will have 1 less each), since Python operator methods are bound.
                isOperator = isOperator && pynargs == pi.Length - 1;
                bool isReverse = isOperator && OperatorMethod.IsReverse((MethodInfo)mi);  // Only cast if isOperator.
                if (isReverse && OperatorMethod.IsComparisonOp((MethodInfo)mi))
                    continue;  // Comparison operators in Python have no reverse mode.
                // Preprocessing pi to remove either the first or second argument.
                if (isOperator && !isReverse)
                {
                    // The first Python arg is the right operand, while the bound instance is the left.
                    // We need to skip the first (left operand) CLR argument.
                    pi = pi.Skip(1).ToArray();
                }
                else if (isOperator && isReverse)
                {
                    // The first Python arg is the left operand.
                    // We need to take the first CLR argument.
                    pi = pi.Take(1).ToArray();
                }

                int clrnargs = pi.Length;
                int arrayStart;

                if (CheckMethodArgumentsMatch(clrnargs,
                    pynargs,
                    pi,
                    out arrayStart,
                    out defaultArgList))
                {
                    var outs = 0;
                    var margs = new object[clrnargs];
                    var usedImplicitConversion = false;

                    for (int n = 0; n < clrnargs; n++)
                    {
                        IntPtr op;
                        if (n < pynargs)
                        {
                            if (arrayStart == n)
                            {
                                // map remaining Python arguments to a tuple since
                                // the managed function accepts it - hopefully :]
                                op = Runtime.PyTuple_GetSlice(args, arrayStart, pynargs);
                            }
                            else
                            {
                                op = Runtime.PyTuple_GetItem(args, n);
                            }

                            // this logic below handles cases when multiple overloading methods
                            // are ambiguous, hence comparison between Python and CLR types
                            // is necessary
                            clrtype = null;
                            IntPtr pyoptype;
                            if (methods.Count > 1)
                            {
                                pyoptype = IntPtr.Zero;
                                pyoptype = Runtime.PyObject_Type(op);
                                Exceptions.Clear();
                                if (pyoptype != IntPtr.Zero)
                                {
                                    clrtype = Converter.GetTypeByAlias(pyoptype);
                                }
                                Runtime.XDecref(pyoptype);
                            }


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
                    StringBuilder stringBuilder = new StringBuilder("Not enough arguments provided to disambiguate the method.  Found:");
                    foreach (var matchedMethod in argMatchedMethods)
                    {
                        stringBuilder.AppendLine();
                        stringBuilder.Append(matchedMethod.Method.ToString());
                    }
                    Exceptions.SetError(Exceptions.TypeError, stringBuilder.ToString());
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
                        Exceptions.SetError(Exceptions.TypeError, "Invoked a non-static method with an invalid instance");
                        return null;
                    }
                    target = co.inst;
                }

                return new Binding(mi, target, margs, outs);
            }
            else if (isGeneric && info == null && methodinfo != null)
            {
                // We weren't able to find a matching method but at least one
                // is a generic method and info is null. That happens when a generic
                // method was not called using the [] syntax. Let's introspect the
                // type of the arguments and use it to construct the correct method.
                Type[] types = Runtime.PythonArgsToTypeArray(args, true);
                MethodInfo mi = MatchParameters(methodinfo, types);
                if (mi != null)
                {
                    return Bind(inst, args, kw, mi, null);
                }
            }
            if (mismatchedMethods.Count > 0)
            {
                var aggregateException = GetAggregateException(mismatchedMethods);
                Exceptions.SetError(aggregateException);
            }
            return null;
        }

        static AggregateException GetAggregateException(IEnumerable<MismatchedMethod> mismatchedMethods)
        {
            return new AggregateException(mismatchedMethods.Select(m => new ArgumentException($"{m.Exception.Message} in method {m.Method}", m.Exception)));
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
        /// If unsuccessful, returns null and may set a Python error.
        /// </summary>
        /// <param name="pi">Information about expected parameters</param>
        /// <param name="paramsArray"><c>true</c>, if the last parameter is a params array.</param>
        /// <param name="args">A pointer to the Python argument tuple</param>
        /// <param name="pyArgCount">Number of arguments, passed by Python</param>
        /// <param name="kwargDict">Dictionary of keyword argument name to python object pointer</param>
        /// <param name="defaultArgList">A list of default values for omitted parameters</param>
        /// <param name="needsResolution"><c>true</c>, if overloading resolution is required</param>
        /// <param name="outs">Returns number of output parameters</param>
        /// <returns>If successful, an array of .NET arguments that can be passed to the method.  Otherwise null.</returns>
        static object[] TryConvertArguments(ParameterInfo[] pi, bool paramsArray,
            IntPtr args, int pyArgCount,
            Dictionary<string, IntPtr> kwargDict,
            ArrayList defaultArgList,
            bool needsResolution,
            out int outs,
            out bool usedImplicitConversion)
        {
            usedImplicitConversion = false;
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

                            if (pi[n].IsOut || clrtype.IsByRef)
                            {
                                outs++;
                            }

                            if (!Converter.ToManaged(op, clrtype, out arg, false))
                            {
                                Exceptions.Clear();
                                margs = null;
                                break;
                            }
                            if (arrayStart == n)
                            {
                                // GetSlice() creates a new reference but GetItem()
                                // returns only a borrow reference.
                                Runtime.XDecref(op);
                            }
                            margs[n] = arg;
                        }
                        else
                        {
                            if (defaultArgList != null)
                            {
                                margs[n] = defaultArgList[n - pynargs];
                            }
                        }
                    }

                    if (margs == null)
                    {
                        continue;
                    }

                    if (isOperator)
                    {
                        if (inst != IntPtr.Zero)
                        {
                            if (ManagedType.GetManagedObject(inst) is CLRObject co)
                            {
                                bool isUnary = pynargs == 0;
                                // Postprocessing to extend margs.
                                var margsTemp = isUnary ? new object[1] : new object[2];
                                // If reverse, the bound instance is the right operand.
                                int boundOperandIndex = isReverse ? 1 : 0;
                                // If reverse, the passed instance is the left operand.
                                int passedOperandIndex = isReverse ? 0 : 1;
                                margsTemp[boundOperandIndex] = co.inst;
                                if (!isUnary)
                                {
                                    margsTemp[passedOperandIndex] = margs[0];
                                }
                                margs = margsTemp;
                            }
                            else continue;
                        }
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

                    var binding = new Binding(mi, target, margs, outs);
                    if (usedImplicitConversion)
                    {
                        // lets just keep the first binding using implicit conversion
                        // this is to respect method order/precedence
                        if (bindingUsingImplicitConversion == null)
                        {
                            // in this case we will not return the binding yet in case there is a match
                            // which does not use implicit conversions, which will return directly
                            bindingUsingImplicitConversion = binding;
                        }
                    }
                    else
                    {
                        return binding;
                    }
                }
            }

            // if we generated a binding using implicit conversion return it
            if (bindingUsingImplicitConversion != null)
            {
                return bindingUsingImplicitConversion;
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

        /// <summary>
        /// This helper method will perform an initial check to determine if we found a matching
        /// method based on its parameters count and type <see cref="Bind(IntPtr,IntPtr,IntPtr,MethodBase,MethodInfo[])"/>
        /// </summary>
        private bool CheckMethodArgumentsMatch(int clrnargs,
            nint pynargs,
            ParameterInfo[] parameterInfo,
            out int arrayStart,
            out ArrayList defaultArgList)
        {
            arrayStart = -1;
            defaultArgList = null;

            var match = false;
            if (pynargs == clrnargs)
            {
                match = true;
            }
            else if (pynargs < clrnargs)
            {
                match = true;
                defaultArgList = new ArrayList();
                for (var v = pynargs; v < clrnargs && match; v++)
                {
                    if (parameterInfo[v].DefaultValue == DBNull.Value)
                    {
                        match = false;
                    }
                    else
                    {
                        defaultArgList.Add(parameterInfo[v].DefaultValue);
                    }
                }
            }
            else if (pynargs > clrnargs && clrnargs > 0 &&
                     Attribute.IsDefined(parameterInfo[clrnargs - 1], typeof(ParamArrayAttribute)))
            {
                // This is a `foo(params object[] bar)` style method
                match = true;
                arrayStart = clrnargs - 1;
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
                else if (list.Count > 0)
                {
                    value.Append($" for {list[0].MethodBase.Name}");
                }

                value.Append(": ");
                AppendArgumentTypes(to: value, args);
                Exceptions.RaiseTypeError(value.ToString());
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

        /// <summary>
        /// Utility class to store the information about a <see cref="MethodBase"/>
        /// </summary>
        [Serializable]
        internal class MethodInformation
        {
            public MethodBase MethodBase { get; }

            public ParameterInfo[] ParameterInfo { get; }

            public MethodInformation(MethodBase methodBase, ParameterInfo[] parameterInfo)
            {
                MethodBase = methodBase;
                ParameterInfo = parameterInfo;
            }

            public override string ToString()
            {
                return MethodBase.ToString();
            }
        }

        /// <summary>
        /// Utility class to sort method info by parameter type precedence.
        /// </summary>
        private class MethodSorter : IComparer<MethodInformation>
        {
            public int Compare(MethodInformation x, MethodInformation y)
            {
                int p1 = GetPrecedence(x);
                int p2 = GetPrecedence(y);
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
