using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    using MaybeMethodInfo = MaybeMethodBase<MethodBase>;
    /// <summary>
    /// Implements a Python binding type for CLR methods. These work much like
    /// standard Python method bindings, but the same type is used to bind
    /// both static and instance methods.
    /// </summary>
    [Serializable]
    internal class MethodBinding : ExtensionType
    {
        internal MaybeMethodInfo info;
        internal MethodObject m;
        internal PyObject? target;
        internal PyType? targetType;

        public MethodBinding(MethodObject m, PyObject? target, PyType? targetType = null)
        {
            this.target = target;

            this.targetType = targetType ?? target?.GetPythonType();

            this.info = null;
            this.m = m;
        }

        /// <summary>
        /// Implement binding of generic methods using the subscript syntax [].
        /// </summary>
        public static NewReference mp_subscript(BorrowedReference tp, BorrowedReference idx)
        {
            var self = (MethodBinding)GetManagedObject(tp)!;

            Type[]? types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null)
            {
                return Exceptions.RaiseTypeError("type(s) expected");
            }

            MethodBase[] overloads = self.m.IsInstanceConstructor
                ? self.m.type.Value.GetConstructor(types) is { } ctor
                    ? new[] { ctor }
                    : Array.Empty<MethodBase>()
                : MethodBinder.MatchParameters(self.m.info, types);
            if (overloads.Length == 0)
            {
                return Exceptions.RaiseTypeError("No match found for given type params");
            }

            MethodObject overloaded = self.m.WithOverloads(overloads);
            var mb = new MethodBinding(overloaded, self.target, self.targetType);
            return mb.Alloc();
        }

        PyObject Signature
        {
            get
            {
                var infos = this.info.Valid ? new[] { this.info.Value } : this.m.info;
                Type type = infos.Select(i => i.DeclaringType)
                    .OrderByDescending(t => t, new TypeSpecificityComparer())
                    .First();
                infos = infos.Where(info => info.DeclaringType == type).ToArray();
                // this is a primitive version
                // the overload with the maximum number of parameters should be used
                MethodBase primary = infos.OrderByDescending(i => i.GetParameters().Length).First();
                var primaryParameters = primary.GetParameters();
                PyObject signatureClass = Runtime.InspectModule.GetAttr("Signature");

                using var parameters = new PyList();
                using var parameterClass = primaryParameters.Length > 0 ? Runtime.InspectModule.GetAttr("Parameter") : null;
                using var positionalOrKeyword = parameterClass?.GetAttr("POSITIONAL_OR_KEYWORD");
                for (int i = 0; i < primaryParameters.Length; i++)
                {
                    var parameter = primaryParameters[i];
                    var alternatives = infos.Select(info =>
                    {
                        ParameterInfo[] altParamters = info.GetParameters();
                        return i < altParamters.Length ? altParamters[i] : null;
                    }).WhereNotNull();
                    using var defaultValue = alternatives
                        .Select(alternative => alternative!.DefaultValue != DBNull.Value ? alternative.DefaultValue.ToPython() : null)
                        .FirstOrDefault(v => v != null) ?? parameterClass?.GetAttr("empty");

                    if (alternatives.Any(alternative => alternative.Name != parameter.Name)
                        || positionalOrKeyword is null)
                    {
                        return signatureClass.Invoke();
                    }

                    using var args = new PyTuple(new[] { parameter.Name.ToPython(), positionalOrKeyword });
                    using var kw = new PyDict();
                    if (defaultValue is not null)
                    {
                        kw["default"] = defaultValue;
                    }
                    using var parameterInfo = parameterClass!.Invoke(args: args, kw: kw);
                    parameters.Append(parameterInfo);
                }

                // TODO: add return annotation
                return signatureClass.Invoke(parameters);
            }
        }

        struct TypeSpecificityComparer : IComparer<Type>
        {
            public int Compare(Type a, Type b)
            {
                if (a == b) return 0;
                if (a.IsSubclassOf(b)) return 1;
                if (b.IsSubclassOf(a)) return -1;
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// MethodBinding __getattribute__ implementation.
        /// </summary>
        public static NewReference tp_getattro(BorrowedReference ob, BorrowedReference key)
        {
            var self = (MethodBinding)GetManagedObject(ob)!;

            if (!Runtime.PyString_Check(key))
            {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return default;
            }

            string? name = InternString.GetManagedString(key);
            switch (name)
            {
                case "__doc__":
                    return self.m.GetDocString();
                // FIXME: deprecate __overloads__ soon...
                case "__overloads__":
                case "Overloads":
                    var om = new OverloadMapper(self.m, self.target);
                    return om.Alloc();
                case "__signature__" when Runtime.InspectModule is not null:
                    var sig = self.Signature;
                    if (sig is null)
                    {
                        return Runtime.PyObject_GenericGetAttr(ob, key);
                    }
                    return sig.NewReferenceOrNull();
                case "__name__":
                    return self.m.GetName();
                default:
                    return Runtime.PyObject_GenericGetAttr(ob, key);
            }
        }


        /// <summary>
        /// MethodBinding  __call__ implementation.
        /// </summary>
        public static NewReference tp_call(BorrowedReference ob, BorrowedReference args, BorrowedReference kw)
        {
            var self = (MethodBinding)GetManagedObject(ob)!;

            // This works around a situation where the wrong generic method is picked,
            // for example this method in the tests: string Overloaded<T>(int arg1, int arg2, string arg3)
            if (self.info.Valid)
            {
                var info = self.info.Value;
                if (info.IsGenericMethod)
                {
                    Type[]? sigTp = Runtime.PythonArgsToTypeArray(args, true);
                    if (sigTp != null)
                    {
                        Type[] genericTp = info.GetGenericArguments();
                        MethodInfo? betterMatch = MethodBinder.MatchSignatureAndParameters(self.m.info, genericTp, sigTp);
                        if (betterMatch != null)
                        {
                            self.info = betterMatch;
                        }
                    }
                }
            }

            // This supports calling a method 'unbound', passing the instance
            // as the first argument. Note that this is not supported if any
            // of the overloads are static since we can't know if the intent
            // was to call the static method or the unbound instance method.
            var disposeList = new List<PyObject>();
            try
            {
                PyObject? target = self.target;

                if (target is null && !self.m.IsStatic())
                {
                    var len = Runtime.PyTuple_Size(args);
                    if (len < 1)
                    {
                        Exceptions.SetError(Exceptions.TypeError, "not enough arguments");
                        return default;
                    }
                    target = new PyObject(Runtime.PyTuple_GetItem(args, 0));
                    disposeList.Add(target);

                    var unboundArgs = Runtime.PyTuple_GetSlice(args, 1, len).MoveToPyObject();
                    disposeList.Add(unboundArgs);
                    args = unboundArgs;
                }

                // if the class is a IPythonDerivedClass and target is not the same as self.targetType
                // (eg if calling the base class method) then call the original base class method instead
                // of the target method.
                IntPtr superType = IntPtr.Zero;
                if (target is not null && Runtime.PyObject_TYPE(target) != self.targetType!)
                {
                    var inst = GetManagedObject(target) as CLRObject;
                    if (inst?.inst is IPythonDerivedType)
                    {
                        if (GetManagedObject(self.targetType!) is ClassBase baseType && baseType.type.Valid)
                        {
                            var baseMethodName = $"_{baseType.type.Value.Name}__{self.m.name}";
                            using var baseMethod = Runtime.PyObject_GetAttrString(target, baseMethodName);
                            if (!baseMethod.IsNull())
                            {
                                if (GetManagedObject(baseMethod.Borrow()) is MethodBinding baseSelf)
                                {
                                    self = baseSelf;
                                }
                            }
                            else
                            {
                                Runtime.PyErr_Clear();
                            }
                        }
                    }
                }

                return self.m.Invoke(target is null ? BorrowedReference.Null : target, args, kw, self.info.UnsafeValue);
            }
            finally
            {
                foreach (var ptr in disposeList)
                {
                    ptr.Dispose();
                }
            }
        }


        /// <summary>
        /// MethodBinding  __hash__ implementation.
        /// </summary>
        public static nint tp_hash(BorrowedReference ob)
        {
            var self = (MethodBinding)GetManagedObject(ob)!;
            nint x = 0;

            if (self.target is not null)
            {
                x = Runtime.PyObject_Hash(self.target);
                if (x == -1)
                {
                    return x;
                }
            }

            nint y = self.m.GetHashCode();
            return x ^ y;
        }

        /// <summary>
        /// MethodBinding  __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (MethodBinding)GetManagedObject(ob)!;
            string type = self.target is null ? "unbound" : "bound";
            string name = self.m.name;
            return Runtime.PyString_FromString($"<{type} method '{name}'>");
        }
    }
}
