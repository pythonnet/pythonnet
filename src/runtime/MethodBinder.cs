using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// <summary>
        /// The overloads of this method
        /// </summary>
        public List<MaybeMethodBase> list;

        [NonSerialized]
        public MethodBase[]? methods;

        [NonSerialized]
        public bool init = false;

        private static Dictionary<string, MethodInfo> _resolvedGenericsCache = new();
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
        internal static MethodBase? MatchSignature(MethodBase[] mi, Type[] tp)
        {
            if (tp == null)
            {
                return null;
            }
            int count = tp.Length;
            foreach (MethodBase t in mi)
            {
                var t = mi[i];
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
        /// return the MethodInfo(s) that represents the matching closed generic.
        /// If unsuccessful, returns null and may set a Python error.
        /// </summary>
        internal static MethodInfo[] MatchParameters(MethodBase[] mi, Type[]? tp)
        {
            if (tp == null)
            {
                return Array.Empty<MethodInfo>();
            }
            int count = tp.Length;
            var result = new List<MethodInfo>();
            foreach (MethodInfo t in mi)
            {
                var t = mi[i];
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
                    result.Add(method);
                }
                catch (ArgumentException)
                {
                    // The error will remain set until cleared by a successful match.
                }
            }
            return result.ToArray();
        }

        // Given a generic method and the argsTypes previously matched with it, 
        // generate the matching method
        internal static MethodInfo ResolveGenericMethod(MethodInfo method, Object[] args)
        {
            // No need to resolve a method where generics are already assigned
            if(!method.ContainsGenericParameters){
                return method;
            }

            bool shouldCache = method.DeclaringType != null;
            string key = null;

            // Check our resolved generics cache first
            if (shouldCache)
            {
                key = method.DeclaringType.AssemblyQualifiedName + method.ToString() + string.Join(",", args.Select(x => x?.GetType()));
                if (_resolvedGenericsCache.TryGetValue(key, out var cachedMethod))
                {
                    return cachedMethod;
                }
            }

            // Get our matching generic types to create our method
            var methodGenerics = method.GetGenericArguments().Where(x => x.IsGenericParameter).ToArray();
            var resolvedGenericsTypes = new Type[methodGenerics.Length];
            int resolvedGenerics = 0;

            var parameters = method.GetParameters();

            // Iterate to length of ArgTypes since default args are plausible
            for (int k = 0; k < args.Length; k++)
            {
                if(args[k] == null){
                    continue;
                }

                var argType = args[k].GetType();
                var parameterType = parameters[k].ParameterType;

                // Ignore those without generic params
                if (!parameterType.ContainsGenericParameters)
                {
                    continue;
                }

                // The parameters generic definition
                var paramGenericDefinition = parameterType.GetGenericTypeDefinition();

                // For the arg that matches this param index, determine the matching type for the generic
                var currentType = argType;
                while (currentType != null)
                {

                    // Check the current type for generic type definition
                    var genericType = currentType.IsGenericType ? currentType.GetGenericTypeDefinition() : null;

                    // If the generic type matches our params generic definition, this is our match
                    // go ahead and match these types to this arg
                    if (paramGenericDefinition == genericType)
                    {

                        // The matching generic for this method parameter
                        var paramGenerics = parameterType.GenericTypeArguments;
                        var argGenericsResolved = currentType.GenericTypeArguments;

                        for (int j = 0; j < paramGenerics.Length; j++)
                        {

                            // Get the final matching index for our resolved types array for this params generic
                            var index = Array.IndexOf(methodGenerics, paramGenerics[j]);

                            if (resolvedGenericsTypes[index] == null)
                            {
                                // Add it, and increment our count
                                resolvedGenericsTypes[index] = argGenericsResolved[j];
                                resolvedGenerics++;
                            }
                            else if (resolvedGenericsTypes[index] != argGenericsResolved[j])
                            {
                                // If we have two resolved types for the same generic we have a problem
                                throw new ArgumentException("ResolveGenericMethod(): Generic method mismatch on argument types");
                            }
                        }

                        break;
                    }

                    // Step up the inheritance tree
                    currentType = currentType.BaseType;
                }
            }

            try
            {
                if (resolvedGenerics != methodGenerics.Length)
                {
                    throw new Exception($"ResolveGenericMethod(): Count of resolved generics {resolvedGenerics} does not match method generic count {methodGenerics.Length}.");
                }

                method = method.MakeGenericMethod(resolvedGenericsTypes);

                if (shouldCache)
                {
                    // Add to cache
                    _resolvedGenericsCache.Add(key, method);
                }
            }
            catch (ArgumentException e)
            {
                // Will throw argument exception if improperly matched
                Exceptions.SetError(e);
            }

            return method;
        }


        /// <summary>
        /// Given a sequence of MethodInfo and two sequences of type parameters,
        /// return the MethodInfo that matches the signature and the closed generic.
        /// </summary>
        internal static MethodInfo? MatchSignatureAndParameters(MethodBase[] mi, Type[] genericTp, Type[] sigTp)
        {
            if (genericTp == null || sigTp == null)
            {
                return null;
            }
            int genericCount = genericTp.Length;
            int signatureCount = sigTp.Length;
            for (var i = 0; i < mi.Length; i++)
            {
                var t = mi[i];
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
            return methods!;
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
                val += ArgPrecedence(pi[i].ParameterType, methodInformation);
            }

            var info = mi as MethodInfo;
            if (info != null)
            {
                val += ArgPrecedence(info.ReturnType, methodInformation);
                val += mi.DeclaringType == mi.ReflectedType ? 0 : 3000;
            }

            return val;
        }

        /// <summary>
        /// Return a precedence value for a particular Type object.
        /// </summary>
        internal static int ArgPrecedence(Type t, MethodInformation mi)
        {
            Type objectType = typeof(object);
            if (t == objectType)
            {
                return 3000;
            }

            if (t.IsAssignableFrom(typeof(PyObject)) && !OperatorMethod.IsOperatorMethod(mi.MethodBase))
            {
                return -1;
            }

            TypeCode tc = Type.GetTypeCode(t);
            // TODO: Clean up
            switch (tc)
            {
                case TypeCode.Object:
                    return 1;

                // we place higher precision methods at the top
                case TypeCode.Decimal:
                    return 2;
                case TypeCode.Double:
                    return 3;
                case TypeCode.Single:
                    return 4;

                case TypeCode.Int64:
                    return 21;
                case TypeCode.Int32:
                    return 22;
                case TypeCode.Int16:
                    return 23;
                case TypeCode.UInt64:
                    return 24;
                case TypeCode.UInt32:
                    return 25;
                case TypeCode.UInt16:
                    return 26;
                case TypeCode.Char:
                    return 27;
                case TypeCode.Byte:
                    return 28;
                case TypeCode.SByte:
                    return 29;

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
                return 100 + ArgPrecedence(e, mi);
            }

            return 2000;
        }

        /// <summary>
        /// Bind the given Python instance and arguments to a particular method
        /// overload and return a structure that contains the converted Python
        /// instance, converted arguments and the correct method to call.
        /// </summary>
        /// <param name="inst">The Python target of the method invocation.</param>
        /// <param name="args">The Python arguments.</param>
        /// <param name="kw">The Python keyword arguments.</param>
        /// <returns>A Binding if successful.  Otherwise null.</returns>
        internal Binding? Bind(BorrowedReference inst, BorrowedReference args, BorrowedReference kw)
        {
            return Bind(inst, args, kw, null, null);
        }

        /// <summary>
        /// Bind the given Python instance and arguments to a particular method
        /// overload in <see cref="list"/> and return a structure that contains the converted Python
        /// instance, converted arguments and the correct method to call.
        /// If unsuccessful, may set a Python error.
        /// </summary>
        /// <param name="inst">The Python target of the method invocation.</param>
        /// <param name="args">The Python arguments.</param>
        /// <param name="kw">The Python keyword arguments.</param>
        /// <param name="info">If not null, only bind to that method.</param>
        /// <returns>A Binding if successful.  Otherwise null.</returns>
        internal Binding? Bind(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase? info)
        {
            return Bind(inst, args, kw, info, null);
        }

        private readonly struct MatchedMethod
        {
            public MatchedMethod(int kwargsMatched, int defaultsNeeded, object?[] margs, int outs, MethodBase mb)
            {
                KwargsMatched = kwargsMatched;
                DefaultsNeeded = defaultsNeeded;
                ManagedArgs = margs;
                Outs = outs;
                Method = mb;
            }

            public int KwargsMatched { get; }
            public int DefaultsNeeded { get; }
            public object?[] ManagedArgs { get; }
            public int Outs { get; }
            public MethodBase Method { get; }
        }

        private readonly struct MismatchedMethod
        {
            public MismatchedMethod(Exception exception, MethodBase mb)
            {
                Exception = exception;
                Method = mb;
            }

            public Exception Exception { get; }
            public MethodBase Method { get; }
        }

        /// <summary>
        /// Bind the given Python instance and arguments to a particular method
        /// overload in <see cref="list"/> and return a structure that contains the converted Python
        /// instance, converted arguments and the correct method to call.
        /// If unsuccessful, may set a Python error.
        /// </summary>
        /// <param name="inst">The Python target of the method invocation.</param>
        /// <param name="args">The Python arguments.</param>
        /// <param name="kw">The Python keyword arguments.</param>
        /// <param name="info">If not null, only bind to that method.</param>
        /// <param name="methodinfo">If not null, additionally attempt to bind to the generic methods in this array by inferring generic type parameters.</param>
        /// <returns>A Binding if successful.  Otherwise null.</returns>
        internal Binding? Bind(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase? info, MethodBase[]? methodinfo)
        {
            // Relevant function variables used post conversion
            Binding bindingUsingImplicitConversion = null;
            Binding genericBinding = null;

            // loop to find match, return invoker w/ or w/o error
            var kwargDict = new Dictionary<string, PyObject>();
            if (kw != null)
            {
                nint pynkwargs = Runtime.PyDict_Size(kw);
                using var keylist = Runtime.PyDict_Keys(kw);
                using var valueList = Runtime.PyDict_Values(kw);
                for (int i = 0; i < pynkwargs; ++i)
                {
                    var keyStr = Runtime.GetManagedString(Runtime.PyList_GetItem(keylist.Borrow(), i));
                    BorrowedReference value = Runtime.PyList_GetItem(valueList.Borrow(), i);
                    kwargDict[keyStr!] = new PyObject(value);
                }
            }

            MethodBase[] _methods;
            if (info != null)
            {
                _methods = new MethodBase[1];
                _methods.SetValue(info, 0);
            }
            else
            {
                _methods = GetMethods();
            }

            return Bind(inst, args, kwargDict, _methods, matchGenerics: true);
        }

        static Binding? Bind(BorrowedReference inst, BorrowedReference args, Dictionary<string, PyObject> kwargDict, MethodBase[] methods, bool matchGenerics)
        {
            var pynargs = (int)Runtime.PyTuple_Size(args);
            var isGeneric = false;

            var argMatchedMethods = new List<MatchedMethod>(methods.Length);
            var mismatchedMethods = new List<MismatchedMethod>();

            // TODO: Clean up
            foreach (MethodBase mi in methods)
            {
                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }
                ParameterInfo[] pi = mi.GetParameters();
                ArrayList? defaultArgList;
                bool paramsArray;
                int kwargsMatched;
                int defaultsNeeded;

                bool isOperator = OperatorMethod.IsOperatorMethod(mi);
                // Binary operator methods will have 2 CLR args but only one Python arg
                // (unary operators will have 1 less each), since Python operator methods are bound.
                isOperator = isOperator && pyArgCount == pi.Length - 1;
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

                // Must be done after IsOperator section
                int clrArgCount = pi.Length;

                if (CheckMethodArgumentsMatch(clrArgCount,
                    pyArgCount,
                    kwArgDict,
                    pi,
                    out bool paramsArray,
                    out ArrayList defaultArgList))
                {
                    var outs = 0;
                    var margs = new object[clrArgCount];

                    int paramsArrayIndex = paramsArray ? pi.Length - 1 : -1; // -1 indicates no paramsArray
                    var usedImplicitConversion = false;

                    // Conversion loop for each parameter
                    for (int paramIndex = 0; paramIndex < clrArgCount; paramIndex++)
                    {
                        IntPtr op = IntPtr.Zero;            // Python object to be converted; not yet set
                        var parameter = pi[paramIndex];     // Clr parameter we are targeting
                        object arg;                         // Python -> Clr argument

                        // Check our KWargs for this parameter
                        bool hasNamedParam = kwArgDict == null ? false : kwArgDict.TryGetValue(parameter.Name, out op);
                        bool isNewReference = false;

                        // Check if we are going to use default
                        if (paramIndex >= pyArgCount && !(hasNamedParam || (paramsArray && paramIndex == paramsArrayIndex)))
                        {
                            if (defaultArgList != null)
                            {
                                margs[paramIndex] = defaultArgList[paramIndex - pyArgCount];
                            }

                            continue;
                        }

                        // At this point, if op is IntPtr.Zero we don't have a KWArg and are not using default
                        if (op == IntPtr.Zero)
                        {
                            // If we have reached the paramIndex
                            if (paramsArrayIndex == paramIndex)
                            {
                                op = HandleParamsArray(args, paramsArrayIndex, pyArgCount, out isNewReference);
                            }
                            else
                            {
                                op = Runtime.PyTuple_GetItem(args, paramIndex);
                            }
                        }

                        // this logic below handles cases when multiple overloading methods
                        // are ambiguous, hence comparison between Python and CLR types
                        // is necessary
                        Type clrtype = null;
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


                        if (clrtype != null)
                        {
                            var typematch = false;

                            if ((parameter.ParameterType != typeof(object)) && (parameter.ParameterType != clrtype))
                            {
                                IntPtr pytype = Converter.GetPythonTypeByAlias(parameter.ParameterType);
                                pyoptype = Runtime.PyObject_Type(op);
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
                                        clrtype = parameter.ParameterType;
                                    }
                                }
                                if (!typematch)
                                {
                                    // this takes care of nullables
                                    var underlyingType = Nullable.GetUnderlyingType(parameter.ParameterType);
                                    if (underlyingType == null)
                                    {
                                        underlyingType = parameter.ParameterType;
                                    }
                                    // this takes care of enum values
                                    TypeCode argtypecode = Type.GetTypeCode(underlyingType);
                                    TypeCode paramtypecode = Type.GetTypeCode(clrtype);
                                    if (argtypecode == paramtypecode)
                                    {
                                        typematch = true;
                                        clrtype = parameter.ParameterType;
                                    }
                                    // lets just keep the first binding using implicit conversion
                                    // this is to respect method order/precedence
                                    else if (bindingUsingImplicitConversion == null)
                                    {
                                        // accepts non-decimal numbers in decimal parameters
                                        if (underlyingType == typeof(decimal))
                                        {
                                            clrtype = parameter.ParameterType;
                                            usedImplicitConversion |= typematch = Converter.ToManaged(op, clrtype, out arg, false);
                                        }
                                        if (!typematch)
                                        {
                                            // this takes care of implicit conversions
                                            var opImplicit = parameter.ParameterType.GetMethod("op_Implicit", new[] { clrtype });
                                            if (opImplicit != null)
                                            {
                                                usedImplicitConversion |= typematch = opImplicit.ReturnType == parameter.ParameterType;
                                                clrtype = parameter.ParameterType;
                                            }
                                        }
                                    }
                                }
                                Runtime.XDecref(pyoptype);
                                if (!typematch)
                                {
                                    margs = null;
                                    break;
                                }
                            }
                            else
                            {
                                clrtype = parameter.ParameterType;
                            }
                        }
                        else
                        {
                            clrtype = parameter.ParameterType;
                        }

                        if (parameter.IsOut || clrtype.IsByRef)
                        {
                            outs++;
                        }

                        if (!Converter.ToManaged(op, clrtype, out arg, false))
                        {
                            margs = null;
                            break;
                        }

                        if (isNewReference)
                        {
                            // TODO: is this a bug? Should this happen even if the conversion fails?
                            // GetSlice() creates a new reference but GetItem()
                            // returns only a borrow reference.
                            Runtime.XDecref(op);
                        }

                        margs[paramIndex] = arg;

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
                                bool isUnary = pyArgCount == 0;
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

                    // If this match is generic we need to resolve it with our types.
                    // Store this generic match to be used if no others match
                    if (mi.IsGenericMethod)
                    {
                        mi = ResolveGenericMethod((MethodInfo)mi, margs);
                        genericBinding = new Binding(mi, target, margs, outs);
                        continue;
                    }

                    var binding = new Binding(mi, target, margs, outs);
                    if (usedImplicitConversion)
                    {
                        // in this case we will not return the binding yet in case there is a match
                        // which does not use implicit conversions, which will return directly
                        bindingUsingImplicitConversion = binding;
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

            // if we generated a generic binding, return it
            if (genericBinding != null)
            {
                return genericBinding;
            }

            return null;
        }


        static AggregateException GetAggregateException(IEnumerable<MismatchedMethod> mismatchedMethods)
        {
            return new AggregateException(mismatchedMethods.Select(m => new ArgumentException($"{m.Exception.Message} in method {m.Method}", m.Exception)));
        }

        static BorrowedReference HandleParamsArray(BorrowedReference args, int arrayStart, int pyArgCount, out NewReference tempObject)
        {
            BorrowedReference op;
            tempObject = default;
            // for a params method, we may have a sequence or single/multiple items
            // here we look to see if the item at the paramIndex is there or not
            // and then if it is a sequence itself.
            if ((pyArgCount - arrayStart) == 1)
            {
                // we only have one argument left, so we need to check it
                // to see if it is a sequence or a single item
                BorrowedReference item = Runtime.PyTuple_GetItem(args, arrayStart);
                if (!Runtime.PyString_Check(item) && Runtime.PySequence_Check(item) || (ManagedType.GetManagedObject(item) as CLRObject)?.inst is IEnumerable))
                {
                    // it's a sequence (and not a string), so we use it as the op
                    op = item;
                }
                else
                {
                    tempObject = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
                    op = tempObject.Borrow();
                }
            }
            else
            {
                tempObject = Runtime.PyTuple_GetSlice(args, arrayStart, pyArgCount);
                op = tempObject.Borrow();
            }
            return op;
        }

        /// <summary>
        /// This helper method will perform an initial check to determine if we found a matching
        /// method based on its parameters count and type <see cref="Bind(IntPtr,IntPtr,IntPtr,MethodBase,MethodInfo[])"/>
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
        static object?[]? TryConvertArguments(ParameterInfo[] pi, bool paramsArray,
            BorrowedReference args, int pyArgCount,
            Dictionary<string, PyObject> kwargDict,
            ArrayList? defaultArgList,
            out int outs)
        {
            outs = 0;
            var margs = new object?[pi.Length];
            int arrayStart = paramsArray ? pi.Length - 1 : -1;

            for (int paramIndex = 0; paramIndex < pi.Length; paramIndex++)
            {
                var parameter = pi[paramIndex];
                bool hasNamedParam = parameter.Name != null ? kwargDict.ContainsKey(parameter.Name) : false;

                if (paramIndex >= pyArgCount && !(hasNamedParam || (paramsArray && paramIndex == arrayStart)))
                {
                    if (defaultArgList != null)
                    {
                        margs[paramIndex] = defaultArgList[paramIndex - pyArgCount];
                    }

                    if (parameter.ParameterType.IsByRef)
                    {
                        outs++;
                    }

                    continue;
                }

                BorrowedReference op;
                NewReference tempObject = default;
                if (hasNamedParam)
                {
                    op = kwargDict[parameter.Name!];
                }
                else
                {
                    if(arrayStart == paramIndex)
                    {
                        op = HandleParamsArray(args, arrayStart, pyArgCount, out tempObject);
                    }
                    else
                    {
                        op = Runtime.PyTuple_GetItem(args, paramIndex);
                    }
                }

                bool isOut;
                if (!TryConvertArgument(op, parameter.ParameterType, out margs[paramIndex], out isOut))
                {
                    tempObject.Dispose();
                    return null;
                }

                tempObject.Dispose();

                if (isOut)
                {
                    outs++;
                }
            }

            return margs;
        }

        /// <summary>
        /// Try to convert a Python argument object to a managed CLR type.
        /// If unsuccessful, may set a Python error.
        /// </summary>
        /// <param name="op">Pointer to the Python argument object.</param>
        /// <param name="parameterType">That parameter's managed type.</param>
        /// <param name="arg">Converted argument.</param>
        /// <param name="isOut">Whether the CLR type is passed by reference.</param>
        /// <returns>true on success</returns>
        static bool TryConvertArgument(BorrowedReference op, Type parameterType,
                                       out object? arg, out bool isOut)
        {
            arg = null;
            isOut = false;
            var clrtype = TryComputeClrArgumentType(parameterType, op);
            if (clrtype == null)
            {
                return false;
            }

            if (!Converter.ToManaged(op, clrtype, out arg, true))
            {
                return false;
            }

            isOut = clrtype.IsByRef;
            return true;
        }

        /// <summary>
        /// Determine the managed type that a Python argument object needs to be converted into.
        /// </summary>
        /// <param name="parameterType">The parameter's managed type.</param>
        /// <param name="argument">Pointer to the Python argument object.</param>
        /// <returns>null if conversion is not possible</returns>
        static Type? TryComputeClrArgumentType(Type parameterType, BorrowedReference argument)
        {
            // this logic below handles cases when multiple overloading methods
            // are ambiguous, hence comparison between Python and CLR types
            // is necessary
            Type? clrtype = null;

            if (clrtype != null)
            {
                if ((parameterType != typeof(object)) && (parameterType != clrtype))
                {
                    BorrowedReference pytype = Converter.GetPythonTypeByAlias(parameterType);
                    BorrowedReference pyoptype = Runtime.PyObject_TYPE(argument);
                    var typematch = false;
                    if (pyoptype != null)
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
                        TypeCode parameterTypeCode = Type.GetTypeCode(parameterType);
                        TypeCode clrTypeCode = Type.GetTypeCode(clrtype);
                        if (parameterTypeCode == clrTypeCode)
                        {
                            typematch = true;
                            clrtype = parameterType;
                        }
                        else
                        {
                            Exceptions.RaiseTypeError($"Expected {parameterTypeCode}, got {clrTypeCode}");
                        }
                    }
                    if (!typematch)
                    {
                        return null;
                    }
                }
                else
                {
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
        /// <param name="positionalArgumentCount">Number of positional args passed from Python.</param>
        /// <param name="parameters">Parameters of the specified .NET method.</param>
        /// <param name="kwargDict">Keyword args passed from Python.</param>
        /// <param name="paramsArray">True if the final param of the .NET method is an array (`params` keyword).</param>
        /// <param name="defaultArgList">List of default values for arguments.</param>
        /// <param name="kwargsMatched">Number of kwargs from Python that are also present in the .NET method.</param>
        /// <param name="defaultsNeeded">Number of non-null defaultsArgs.</param>
        /// <returns></returns>
        static bool MatchesArgumentCount(int positionalArgumentCount, ParameterInfo[] parameters,
            Dictionary<string, PyObject> kwargDict,
            out bool paramsArray,
            out ArrayList? defaultArgList,
            out int kwargsMatched,
            out int defaultsNeeded)


        private bool CheckMethodArgumentsMatch(int clrArgCount,
            int pyArgCount,
            Dictionary<string, IntPtr> kwargDict,
            ParameterInfo[] parameterInfo,
            out bool paramsArray,
            out ArrayList defaultArgList)
        {
            var match = false;

            // Prepare our outputs
            defaultArgList = null;
            paramsArray = false;
            if (parameterInfo.Length > 0)
            {
                var lastParameterInfo = parameterInfo[parameterInfo.Length - 1];
                if (lastParameterInfo.ParameterType.IsArray)
                {
                    paramsArray = Attribute.IsDefined(lastParameterInfo, typeof(ParamArrayAttribute));
                }
            }

            // First if we have anys kwargs, look at the function for matching args
            if (kwargDict != null && kwargDict.Count > 0)
            {
                // If the method doesn't have all of these kw args, it is not a match
                // Otherwise just continue on to see if it is a match
                if (!kwargDict.All(x => parameterInfo.Any(pi => x.Key == pi.Name)))
                {
                    return false;
                }
            }

            // If they have the exact same amount of args they do match
            // Must check kwargs because it contains additional args
            if (pyArgCount == clrArgCount && (kwargDict == null || kwargDict.Count == 0))
            {
                match = true;
            }
            else if (pyArgCount < clrArgCount)
            {
                // every parameter past 'pyArgCount' must have either
                // a corresponding keyword argument or a default parameter
                match = true;
                defaultArgList = new ArrayList();
                for (var v = pyArgCount; v < clrArgCount && match; v++)
                {
                    if (kwargDict != null && kwargDict.ContainsKey(parameterInfo[v].Name))
                    {
                        // we have a keyword argument for this parameter,
                        // no need to check for a default parameter, but put a null
                        // placeholder in defaultArgList
                        defaultArgList.Add(null);
                    }
                    else if (parameterInfo[v].IsOptional)
                    {
                        // IsOptional will be true if the parameter has a default value,
                        // or if the parameter has the [Optional] attribute specified.
                        if (parameterInfo[v].HasDefaultValue)
                        {
                            defaultArgList.Add(parameterInfo[v].DefaultValue);
                        }
                        else
                        {
                            // [OptionalAttribute] was specified for the parameter.
                            // See https://stackoverflow.com/questions/3416216/optionalattribute-parameters-default-value
                            // for rules on determining the value to pass to the parameter
                            var type = parameterInfo[v].ParameterType;
                            if (type == typeof(object))
                                defaultArgList.Add(Type.Missing);
                            else if (type.IsValueType)
                                defaultArgList.Add(Activator.CreateInstance(type));
                            else
                                defaultArgList.Add(null);
                        }
                    }
                    else if (parameters[v].IsOut) {
                        defaultArgList.Add(null);
                    }
                    else if (!paramsArray)
                    {
                        // If there is no KWArg or Default value, then this isn't a match
                        match = false;
                    }
                }
            }
            else if (pyArgCount > clrArgCount && clrArgCount > 0 && paramsArray)
            {
                // This is a `foo(params object[] bar)` style method
                // We will handle the params later
                match = true;
            }
            return match;
        }

        internal virtual NewReference Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw)
        {
            return Invoke(inst, args, kw, null, null);
        }

        internal virtual NewReference Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase? info)
        {
            return Invoke(inst, args, kw, info, null);
        }

        protected static void AppendArgumentTypes(StringBuilder to, BorrowedReference args)
        {
            Runtime.AssertNoErorSet();

            nint argCount = Runtime.PyTuple_Size(args);
            to.Append("(");
            for (nint argIndex = 0; argIndex < argCount; argIndex++)
            {
                BorrowedReference arg = Runtime.PyTuple_GetItem(args, argIndex);
                if (arg != null)
                {
                    BorrowedReference type = Runtime.PyObject_TYPE(arg);
                    if (type != null)
                    {
                        using var description = Runtime.PyObject_Str(type);
                        if (description.IsNull())
                        {
                            Exceptions.Clear();
                            to.Append(Util.BadStr);
                        }
                        else
                        {
                            to.Append(Runtime.GetManagedString(description.Borrow()));
                        }
                    }
                }

                if (argIndex + 1 < argCount)
                    to.Append(", ");
            }
            to.Append(')');
        }

        internal virtual NewReference Invoke(BorrowedReference inst, BorrowedReference args, BorrowedReference kw, MethodBase? info, MethodBase[]? methodinfo)
        {
            // No valid methods, nothing to bind.
            if (GetMethods().Length == 0)
            {
                var msg = new StringBuilder("The underlying C# method(s) have been deleted");
                if (list.Count > 0 && list[0].Name != null)
                {
                    msg.Append($": {list[0]}");
                }
                return Exceptions.RaiseTypeError(msg.ToString());
            }

            Binding? binding = Bind(inst, args, kw, info, methodinfo);.cs
            object result;
            IntPtr ts = IntPtr.Zero;

            if (binding == null)
            {
                var value = new StringBuilder("No method matches given arguments");
                if (methodinfo != null && methodinfo.Length > 0)
                {
                    value.Append($" for {methodinfo[0].DeclaringType?.Name}.{methodinfo[0].Name}");
                }
                else if (list.Count > 0 && list[0].Valid)
                {
                    value.Append($" for {list[0].Value.DeclaringType?.Name}.{list[0].Value.Name}");
                }

                value.Append(": ");
                Runtime.PyErr_Fetch(out var errType, out var errVal, out var errTrace);
                AppendArgumentTypes(to: value, args);
                Runtime.PyErr_Restore(errType.StealNullable(), errVal.StealNullable(), errTrace.StealNullable());
                return Exceptions.RaiseTypeError(value.ToString());
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
                return default;
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

            var returnType = binding.info.IsConstructor ? typeof(void) : ((MethodInfo)binding.info).ReturnType;

            if (binding.outs > 0)
            {
                ParameterInfo[] pi = binding.info.GetParameters();
                int c = pi.Length;
                var n = 0;

                bool isVoid = returnType == typeof(void);
                int tupleSize = binding.outs + (isVoid ? 0 : 1);
                using var t = Runtime.PyTuple_New(tupleSize);
                if (!isVoid)
                {
                    using var v = Converter.ToPython(result, returnType);
                    Runtime.PyTuple_SetItem(t.Borrow(), n, v.Steal());
                    n++;
                }

                for (var i = 0; i < c; i++)
                {
                    Type pt = pi[i].ParameterType;
                    if (pi[i].IsOut || pt.IsByRef)
                    {
                        using var v = Converter.ToPython(binding.args[i], pt.GetElementType());
                        Runtime.PyTuple_SetItem(t.Borrow(), n, v.Steal());
                        n++;
                    }
                }

                if (binding.outs == 1 && returnType == typeof(void))
                {
                    BorrowedReference item = Runtime.PyTuple_GetItem(t.Borrow(), 0);
                    return new NewReference(item);
                }

                return new NewReference(t.Borrow());
            }

            return Converter.ToPython(result, returnType);
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
                                to.Append(Runtime.GetManagedSpan(description, out var newReference));
                                newReference.Dispose();
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
        public object?[] args;
        public object? inst;
        public int outs;

        internal Binding(MethodBase info, object? inst, object?[] args, int outs)
        {
            this.info = info;
            this.inst = inst;
            this.args = args;
            this.outs = outs;
        }
    }


    static internal class ParameterInfoExtensions
    {
        public static object? GetDefaultValue(this ParameterInfo parameterInfo)
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
