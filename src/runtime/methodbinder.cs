using System;
using System.Collections;
using System.Reflection;

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
            MethodBase[] _methods = null;
            int pynargs = Runtime.PyTuple_Size(args);
            object arg;
            var isGeneric = false;
            ArrayList defaultArgList = null;
            if (info != null)
            {
                _methods = new MethodBase[1];
                _methods.SetValue(info, 0);
            }
            else
            {
                _methods = GetMethods();
            }
            Type clrtype;
            // TODO: Clean up
            foreach (MethodBase mi in _methods)
            {
                if (mi.IsGenericMethod)
                {
                    isGeneric = true;
                }
                ParameterInfo[] pi = mi.GetParameters();
                int clrnargs = pi.Length;
                var match = false;
                int arrayStart = -1;
                var outs = 0;

                if (pynargs == clrnargs)
                {
                    match = true;
                }
                else if (pynargs < clrnargs)
                {
                    match = true;
                    defaultArgList = new ArrayList();
                    for (int v = pynargs; v < clrnargs; v++)
                    {
                        if (pi[v].DefaultValue == DBNull.Value)
                        {
                            match = false;
                        }
                        else
                        {
                            defaultArgList.Add(pi[v].DefaultValue);
                        }
                    }
                }
                else if (pynargs > clrnargs && clrnargs > 0 &&
                         Attribute.IsDefined(pi[clrnargs - 1], typeof(ParamArrayAttribute)))
                {
                    // This is a `foo(params object[] bar)` style method
                    match = true;
                    arrayStart = clrnargs - 1;
                }

                if (match)
                {
                    var margs = new object[clrnargs];

                    for (var n = 0; n < clrnargs; n++)
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
                            if (_methods.Length > 1)
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
                                if ((pi[n].ParameterType != typeof(object)) && (pi[n].ParameterType != clrtype))
                                {
                                    IntPtr pytype = Converter.GetPythonTypeByAlias(pi[n].ParameterType);
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
                                            clrtype = pi[n].ParameterType;
                                        }
                                    }
                                    if (!typematch)
                                    {
                                        // this takes care of enum values
                                        TypeCode argtypecode = Type.GetTypeCode(pi[n].ParameterType);
                                        TypeCode paramtypecode = Type.GetTypeCode(clrtype);
                                        if (argtypecode == paramtypecode)
                                        {
                                            typematch = true;
                                            clrtype = pi[n].ParameterType;
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
                                    typematch = true;
                                    clrtype = pi[n].ParameterType;
                                }
                            }
                            else
                            {
                                clrtype = pi[n].ParameterType;
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
                Exceptions.SetError(Exceptions.TypeError, "No method matches given arguments");
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
    }


    /// <summary>
    /// Utility class to sort method info by parameter type precedence.
    /// </summary>
    internal class MethodSorter : IComparer
    {
        int IComparer.Compare(object m1, object m2)
        {
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
