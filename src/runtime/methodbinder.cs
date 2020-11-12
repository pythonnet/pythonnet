using System;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using Microsoft.CSharp.RuntimeBinder;

namespace Python.Runtime
{
    /// <summary>
    /// A MethodBinder encapsulates information about a (possibly overloaded)
    /// managed method, and is responsible for selecting the right method given
    /// a set of Python arguments. This is also used as a base class for the
    /// ConstructorBinder, a minor variation used to invoke constructors.
    /// </summary>
    [Serializable]
    internal class MethodBinder: System.Runtime.Serialization.IDeserializationCallback
    {
        readonly List<MethodBase> methods = new List<MethodBase>();
        [NonSerialized]
        Lazy<Dictionary<CallSiteCacheKey, CallSite>> callSiteCache;
        public bool init = false;
        public bool allow_threads = true;

        internal MethodBinder() {
            this.InitializeCallSiteCache();
        }

        internal MethodBinder(MethodInfo mi):this()
        {
            this.methods.Add(mi);
        }

        public int Count
        {
            get { return this.methods.Count; }
        }

        internal void AddMethod(MethodBase m)
        {
            this.methods.Add(m);
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
        internal List<MethodBase> GetMethods()
        {
            if (!init)
            {
                // I'm sure this could be made more efficient.
                this.methods.Sort(new MethodSorter());
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

        internal Binding Bind(IntPtr inst, IntPtr args, IntPtr kw, MethodBase info, MethodInfo[] methodinfo)
        {
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

            // loop to find match, return invoker w/ or /wo error
            List<MethodBase> _methods = info != null ? new List<MethodBase> { info } : GetMethods();

            // TODO: Clean up
            foreach (MethodBase mi in _methods)
            {
                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }
                ParameterInfo[] pi = mi.GetParameters();
                bool paramsArray;

                if (!MatchesArgumentCount(pynargs, pi, kwargDict, out paramsArray, out var defaultArgList))
                {
                    continue;
                }
                var outs = 0;
                var margs = TryConvertArguments(pi, paramsArray, args, pynargs, kwargDict, defaultArgList,
                    needsResolution: _methods.Count > 1,
                    outs: out outs);

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
        /// <param name="outs">Returns number of output parameters</param>
        /// <returns>An array of .NET arguments, that can be passed to a method.</returns>
        static object[] TryConvertArguments(ParameterInfo[] pi, bool paramsArray,
            IntPtr args, int pyArgCount,
            Dictionary<string, IntPtr> kwargDict,
            List<object> defaultArgList,
            bool needsResolution,
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

                if (parameter.IsOut || isOut)
                {
                    outs++;
                }
            }

            return margs;
        }

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

        static bool MatchesArgumentCount(int positionalArgumentCount, ParameterInfo[] parameters,
            Dictionary<string, IntPtr> kwargDict,
            out bool paramsArray,
            out List<object> defaultArgList)
        {
            defaultArgList = null;
            var match = false;
            paramsArray = parameters.Length > 0 ? Attribute.IsDefined(parameters[parameters.Length - 1], typeof(ParamArrayAttribute)) : false;

            if (positionalArgumentCount == parameters.Length && kwargDict.Count == 0)
            {
                match = true;
            }
            else if (positionalArgumentCount < parameters.Length)
            {
                // every parameter past 'positionalArgumentCount' must have either
                // a corresponding keyword argument or a default parameter
                match = true;
                defaultArgList = new List<object>();
                for (var v = positionalArgumentCount; v < parameters.Length; v++)
                {
                    if (kwargDict.ContainsKey(parameters[v].Name))
                    {
                        // we have a keyword argument for this parameter,
                        // no need to check for a default parameter, but put a null
                        // placeholder in defaultArgList
                        defaultArgList.Add(null);
                    }
                    else if (parameters[v].IsOptional)
                    {
                        // IsOptional will be true if the parameter has a default value,
                        // or if the parameter has the [Optional] attribute specified.
                        // The GetDefaultValue() extension method will return the value
                        // to be passed in as the parameter value
                        defaultArgList.Add(parameters[v].GetDefaultValue());
                    }
                    else if(!paramsArray)
                    {
                        match = false;
                    }
                }
            }
            else if (positionalArgumentCount > parameters.Length && parameters.Length > 0 &&
                       Attribute.IsDefined(parameters[parameters.Length - 1], typeof(ParamArrayAttribute)))
            {
                // This is a `foo(params object[] bar)` style method
                match = true;
                paramsArray = true;
            }

            return match;
        }

        internal virtual IntPtr Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw)
        {
            return Invoke(inst, args, kw, null, null);
        }

        internal virtual IntPtr Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase info)
        {
            return Invoke(inst, args, kw, info, null);
        }

        private static string GetName(IEnumerable<MethodBase> methodInfo)
            => methodInfo.FirstOrDefault()?.Name;

        private static void SetOverloadNotFoundError(BorrowedReference args, string methodName)
        {
            var value = new StringBuilder("No method matches given arguments");
            if (methodName != null)
            {
                value.Append($" for {methodName}");
            }

            value.Append(": ");
            AppendArgumentTypes(to: value, args);
            Exceptions.SetError(Exceptions.TypeError, value.ToString());
        }
        protected static void AppendArgumentTypes(StringBuilder to, BorrowedReference args)
        {
            long argCount = Runtime.PyTuple_Size(args);
            to.Append("(");
            for (long argIndex = 0; argIndex < argCount; argIndex++)
            {
                var arg = Runtime.PyTuple_GetItem(args, argIndex);
                if (!arg.IsNull)
                {
                    var type = Runtime.PyObject_Type(arg);
                    if (!type.IsNull)
                    {
                        var description = Runtime.PyObject_Unicode(type);
                        if (!description.IsNull())
                        {
                            to.Append(Runtime.GetManagedString(description));
                        }
                        description.Dispose();
                    }
                }

                if (argIndex + 1 < argCount)
                    to.Append(", ");
            }
            to.Append(')');
        }

        static List<CSharpArgumentInfo> GetArguments(BorrowedReference args, BorrowedReference kwArgs)
        {
            int argCount = (int)Runtime.PyTuple_Size(args);

            var arguments = new List<CSharpArgumentInfo>() {
                // the instance, on which the call is made (null for static methods)
                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            };
            for (int argN = 0; argN < argCount; argN++)
            {
                arguments.Add(CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null));
            }

            if (!kwArgs.IsNull)
            {
                int kwCount = (int)Runtime.PyDict_Size(kwArgs);
                NewReference keys = Runtime.PyDict_Keys(kwArgs);
                NewReference items = Runtime.PyDict_Values(kwArgs);
                for (int kwN = 0; kwN < kwCount; kwN++)
                {
                    string argName = Runtime.GetManagedString(Runtime.PyList_GetItem(keys, kwN));
                    arguments.Add(CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.NamedArgument, argName));
                }
                keys.Dispose();
                items.Dispose();
            }

            return arguments;
        }

        static object[] GetParameters(
            BorrowedReference args, BorrowedReference kwArgs,
            out int argCount, out int kwCount)
        {
            argCount = (int)Runtime.PyTuple_Size(args);
            kwCount = kwArgs.IsNull ? 0 : (int)Runtime.PyDict_Size(kwArgs);
            var result = new object[argCount + kwCount];
            for (int argN = 0; argN < argCount; argN++)
            {
                var arg = Runtime.PyTuple_GetItem(args, argN);
                if (!Converter.ToManaged(arg, typeof(object), out result[argN], setError: true))
                    return null;
            }

            if (!kwArgs.IsNull)
            {
                NewReference items = Runtime.PyDict_Values(kwArgs);
                for (int kwN = 0; kwN < kwCount; kwN++)
                {
                    var kwArg = Runtime.PyList_GetItem(items, kwN);
                    if (!Converter.ToManaged(kwArg, typeof(object), out result[kwN + argCount], setError: true))
                        return null;
                }
                items.Dispose();
            }

            return result;
        }

        internal virtual IntPtr Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase info, MethodInfo[] methodinfo)
        {
            var clrInstance = ManagedType.GetManagedObject(inst) as CLRObject;
            if (clrInstance is null)
                // static method binding is not yet implemented with DLR
                return this.StaticInvoke(IntPtr.Zero, args, kw, info, methodinfo);

            object[] parameters = GetParameters(args, kw, out int argCount, out int kwCount);
            if (parameters is null) return IntPtr.Zero;

            var method = info ?? methodinfo?.FirstOrDefault() ?? this.GetMethods()[0];
            var callSite = this.GetOrCreateCallSite(args, kw, parameters.Length, argCount, kwCount, method);

            object result;
            IntPtr ts = IntPtr.Zero;

            if (allow_threads)
            {
                ts = PythonEngine.BeginAllowThreads();
            }

            try
            {
                var invokeParameters = new object[parameters.Length + 2];
                invokeParameters[0] = callSite;
                invokeParameters[1] = clrInstance?.inst;
                Array.Copy(parameters, sourceIndex: 0, invokeParameters, destinationIndex: 2, parameters.Length);
                result = ((dynamic)callSite).Target.DynamicInvoke(invokeParameters);
            } catch (TargetInvocationException e)
                when (e.InnerException is RuntimeBinderException bindError)
            {
                if (allow_threads)
                {
                    PythonEngine.EndAllowThreads(ts);
                }
                // TODO: propagate bindError
                SetOverloadNotFoundError(args, GetName(methodinfo ?? GetMethods().ToArray()));
                return IntPtr.Zero;
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

#warning INCOMPLETE

            return Converter.ToPython(result, typeof(object) /*must be actual return type here*/);
        }

        internal virtual IntPtr StaticInvoke(IntPtr inst, BorrowedReference args, BorrowedReference kw, MethodBase info, MethodInfo[] methodinfo)
        {
            Binding binding = Bind(inst,
                args.DangerousGetAddress(),
                kw.DangerousGetAddressOrNull(),
                info, methodinfo);

            object result;
            IntPtr ts = IntPtr.Zero;

            if (binding == null)
            {
                SetOverloadNotFoundError(args, GetName(methodinfo));
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

        private CallSite GetOrCreateCallSite(BorrowedReference args, BorrowedReference kw, int parameterCount, int argCount, int kwCount, MethodBase method)
        {
            var frame = Runtime.PyEval_GetFrame();
            var cacheKey = new CallSiteCacheKey
            {
                Line = frame.IsNull ? -1 : Runtime.PyFrame_GetLineNumber(frame),
                ArgCount = argCount,
                KwArgCount = kwCount,
            };
            lock (this.callSiteCache.Value)
            {
                if (!this.callSiteCache.Value.TryGetValue(cacheKey, out var callSite))
                {
                    var arguments = GetArguments(args, kw);
                    var binder = PythonNetCallSiteBinder.InvokeMember(
                        method.Name,
                        // TODO: forward type arguments
                        typeArguments: null,
                        context: method.DeclaringType,
                        arguments.ToArray()
                    );

                    var callSiteTypeArgs = new List<Type>
                    {
                        typeof(CallSite),
                        typeof(object),
                    };
                    for (int i = 0; i < parameterCount; i++)
                        callSiteTypeArgs.Add(typeof(object));
                    // return type
                    callSiteTypeArgs.Add(typeof(object));
                    var delegateType = Expression.GetFuncType(callSiteTypeArgs.ToArray());

                    callSite = CallSite.Create(delegateType, binder);
                    this.callSiteCache.Value[cacheKey] = callSite;
                }

                return callSite;
            }
        }

        void System.Runtime.Serialization.IDeserializationCallback.OnDeserialization(object sender)
        {
            this.InitializeCallSiteCache();
        }

        void InitializeCallSiteCache()
        {
            this.callSiteCache = new Lazy<Dictionary<CallSiteCacheKey, CallSite>>(() => new Dictionary<CallSiteCacheKey, CallSite>());
        }

        struct CallSiteCacheKey : IEquatable<CallSiteCacheKey>
        {
            public int Line { get; set; }
            public int ArgCount { get; set; }
#warning has to be replaced with the full ordered list of kwarg names
            public int KwArgCount { get; set; }

            public override bool Equals(object obj) => obj is CallSiteCacheKey key && this.Equals(key);
            public bool Equals(CallSiteCacheKey other) => this.Line == other.Line && this.ArgCount == other.ArgCount && this.KwArgCount == other.KwArgCount;

            public override int GetHashCode()
            {
                int hashCode = -1650922183;
                hashCode = hashCode * -1521134295 + this.Line.GetHashCode();
                hashCode = hashCode * -1521134295 + this.ArgCount.GetHashCode();
                hashCode = hashCode * -1521134295 + this.KwArgCount.GetHashCode();
                return hashCode;
            }
        }
    }


    /// <summary>
    /// Utility class to sort method info by parameter type precedence.
    /// </summary>
    internal class MethodSorter : IComparer<MethodBase>
    {
        int IComparer<MethodBase>.Compare(MethodBase me1, MethodBase me2)
        {
            if (me1.DeclaringType != me2.DeclaringType)
            {
                // m2's type derives from m1's type, favor m2
                if (me1.DeclaringType.IsAssignableFrom(me2.DeclaringType))
                    return 1;

                // m1's type derives from m2's type, favor m1
                if (me2.DeclaringType.IsAssignableFrom(me1.DeclaringType))
                    return -1;
            }

            int p1 = MethodBinder.GetPrecedence(me1);
            int p2 = MethodBinder.GetPrecedence(me2);
            return p1.CompareTo(p2);
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
