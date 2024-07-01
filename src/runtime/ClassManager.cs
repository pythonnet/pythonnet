using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

using Python.Runtime.StateSerialization;

namespace Python.Runtime
{
    /// <summary>
    /// The ClassManager is responsible for creating and managing instances
    /// that implement the Python type objects that reflect managed classes.
    /// Each managed type reflected to Python is represented by an instance
    /// of a concrete subclass of ClassBase. Each instance is associated with
    /// a generated Python type object, whose slots point to static methods
    /// of the managed instance's class.
    /// </summary>
    internal class ClassManager
    {

        // Binding flags to determine which members to expose in Python.
        // This is complicated because inheritance in Python is name
        // based. We can't just find DeclaredOnly members, because we
        // could have a base class A that defines two overloads of a
        // method and a class B that defines two more. The name-based
        // descriptor Python will find needs to know about inherited
        // overloads as well as those declared on the sub class.
        internal static readonly BindingFlags BindingFlags = BindingFlags.Static |
                                                             BindingFlags.Instance |
                                                             BindingFlags.Public |
                                                             BindingFlags.NonPublic;

        internal static Dictionary<MaybeType, ReflectedClrType> cache = new(capacity: 128);
        private static readonly Type dtype;

        private ClassManager()
        {
        }

        static ClassManager()
        {
            // SEE: https://msdn.microsoft.com/en-us/library/96b1ayy4(v=vs.100).aspx
            // ""All delegates inherit from MulticastDelegate, which inherits from Delegate.""
            // Was Delegate, which caused a null MethodInfo returned from GetMethode("Invoke")
            // and crashed on Linux under Mono.
            dtype = typeof(MulticastDelegate);
        }

        public static void Reset()
        {
            cache.Clear();
        }

        internal static void RemoveClasses()
        {
            foreach (var @class in cache.Values)
            {
                @class.Dispose();
            }
            cache.Clear();
        }

        internal static ClassManagerState SaveRuntimeData()
        {
            var contexts = new Dictionary<ReflectedClrType, Dictionary<string, object?>>();
            foreach (var cls in cache)
            {
                var cb = (ClassBase)ManagedType.GetManagedObject(cls.Value)!;
                var context = cb.Save(cls.Value);
                if (context is not null)
                {
                    contexts[cls.Value] = context;
                }

                // Remove all members added in InitBaseClass.
                // this is done so that if domain reloads and a member of a
                // reflected dotnet class is removed, it is removed from the
                // Python object's dictionary tool; thus raising an AttributeError
                // instead of a TypeError.
                // Classes are re-initialized on in RestoreRuntimeData.
                using var dict = Runtime.PyObject_GenericGetDict(cls.Value);
                foreach (var member in cb.dotNetMembers)
                {
                    if ((Runtime.PyDict_DelItemString(dict.Borrow(), member) == -1) &&
                        (Exceptions.ExceptionMatches(Exceptions.KeyError)))
                    {
                        // Trying to remove a key that's not in the dictionary
                        // raises an error. We don't care about it.
                        Runtime.PyErr_Clear();
                    }
                    else if (Exceptions.ErrorOccurred())
                    {
                        throw PythonException.ThrowLastAsClrException();
                    }
                }
                // We modified the Type object, notify it we did.
                Runtime.PyType_Modified(cls.Value);
            }

            return new()
            {
                Contexts = contexts,
                Cache = cache,
            };
        }

        internal static void RestoreRuntimeData(ClassManagerState storage)
        {
            cache = storage.Cache;
            var invalidClasses = new List<KeyValuePair<MaybeType, ReflectedClrType>>();
            var contexts = storage.Contexts;
            foreach (var pair in cache)
            {
                var context = contexts[pair.Value];
                if (pair.Key.Valid)
                {
                    pair.Value.Restore(context);
                }
                else
                {
                    invalidClasses.Add(pair);
                    var cb = new UnloadedClass(pair.Key.Name);
                    cb.Load(pair.Value, context);
                    pair.Value.Restore(cb);
                }
            }
        }

        /// <summary>
        /// Return the ClassBase-derived instance that implements a particular
        /// reflected managed type, creating it if it doesn't yet exist.
        /// </summary>
        internal static BorrowedReference GetClass(Type type) => ReflectedClrType.GetOrCreate(type);

        internal static ClassBase GetClassImpl(Type type)
        {
            var pyType = GetClass(type);
            var impl = (ClassBase)ManagedType.GetManagedObject(pyType)!;
            Debug.Assert(impl is not null);
            return impl!;
        }


        /// <summary>
        /// Create a new ClassBase-derived instance that implements a reflected
        /// managed type. The new object will be associated with a generated
        /// Python type object.
        /// </summary>
        internal static ClassBase CreateClass(Type type)
        {
            // Next, select the appropriate managed implementation class.
            // Different kinds of types, such as array types or interface
            // types, want to vary certain implementation details to make
            // sure that the type semantics are consistent in Python.

            ClassBase impl;

            // Check to see if the given type extends System.Exception. This
            // lets us check once (vs. on every lookup) in case we need to
            // wrap Exception-derived types in old-style classes

            if (type.ContainsGenericParameters)
            {
                impl = new GenericType(type);
            }

            else if (type.IsSubclassOf(dtype))
            {
                impl = new DelegateObject(type);
            }

            else if (type.IsArray)
            {
                impl = new ArrayObject(type);
            }

            else if (type.IsInterface)
            {
                impl = new InterfaceObject(type);
            }

            else if (type == typeof(Exception) ||
                     type.IsSubclassOf(typeof(Exception)))
            {
                impl = new ExceptionClassObject(type);
            }

#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use.
            else if (null != PythonDerivedType.GetPyObjField(type))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                impl = new ClassDerivedObject(type);
            }

            else
            {
                impl = new ClassObject(type);
            }


            return impl;
        }

        internal static void InitClassBase(Type type, ClassBase impl, ReflectedClrType pyType)
        {
            // First, we introspect the managed type and build some class
            // information, including generating the member descriptors
            // that we'll be putting in the Python class __dict__.

            ClassInfo info = GetClassInfo(type, impl);

            impl.indexer = info.indexer;
            impl.richcompare.Clear();


            // Finally, initialize the class __dict__ and return the object.
            using var newDict = Runtime.PyObject_GenericGetDict(pyType.Reference);
            BorrowedReference dict = newDict.Borrow();

            foreach (var iter in info.members)
            {
                var item = iter.Value;
                var name = iter.Key;
                impl.dotNetMembers.Add(name);
                Runtime.PyDict_SetItemString(dict, name, item);
                if (ClassBase.CilToPyOpMap.TryGetValue(name, out var pyOp)
                    // workaround for unintialized types crashing in GetManagedObject
                    && item is not ReflectedClrType
                    && ManagedType.GetManagedObject(item) is MethodObject method)
                {
                    impl.richcompare.Add(pyOp, method);
                }
            }

            // If class has constructors, generate an __doc__ attribute.
            NewReference doc = default;
            Type marker = typeof(DocStringAttribute);
            var attrs = (Attribute[])type.GetCustomAttributes(marker, false);
            if (attrs.Length != 0)
            {
                var attr = (DocStringAttribute)attrs[0];
                string docStr = attr.DocString;
                doc = Runtime.PyString_FromString(docStr);
                Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, doc.Borrow());
            }

            // If this is a ClassObject AND it has constructors, generate a __doc__ attribute.
            // required that the ClassObject.ctors be changed to internal
            if (impl is ClassObject co)
            {
                if (co.NumCtors > 0 && !co.HasCustomNew())
                {
                    // Implement Overloads on the class object
                    if (!CLRModule._SuppressOverloads)
                    {
                        // HACK: __init__ points to instance constructors.
                        // When unbound they fully instantiate object, so we get overloads for free from MethodBinding.
                        var init = info.members["__init__"];
                        // TODO: deprecate __overloads__ soon...
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__overloads__, init);
                        Runtime.PyDict_SetItem(dict, PyIdentifier.Overloads, init);
                    }

                    // don't generate the docstring if one was already set from a DocStringAttribute.
                    if (!CLRModule._SuppressDocs && doc.IsNull())
                    {
                        doc = co.GetDocString();
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, doc.Borrow());
                    }
                }

                if (Runtime.PySequence_Contains(dict, PyIdentifier.__doc__) != 1)
                {
                    // Ensure that at least some doc string is set
                    using var fallbackDoc = Runtime.PyString_FromString(
                        $"Python wrapper for .NET type {type}"
                    );
                    Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, fallbackDoc.Borrow());
                }
            }
            doc.Dispose();

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(pyType.Reference);
        }

        internal static bool ShouldBindMethod(MethodBase mb)
        {
            if (mb is null) throw new ArgumentNullException(nameof(mb));
            return (mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly);
        }

        internal static bool ShouldBindField(FieldInfo fi)
        {
            if (fi is null) throw new ArgumentNullException(nameof(fi));
            return (fi.IsPublic || fi.IsFamily || fi.IsFamilyOrAssembly);
        }

        internal static bool ShouldBindProperty(PropertyInfo pi)
        {
                MethodInfo? mm;
                try
                {
                    mm = pi.GetGetMethod(true);
                    if (mm == null)
                    {
                        mm = pi.GetSetMethod(true);
                    }
                }
                catch (SecurityException)
                {
                    // GetGetMethod may try to get a method protected by
                    // StrongNameIdentityPermission - effectively private.
                    return false;
                }

                if (mm == null)
                {
                    return false;
                }

                return ShouldBindMethod(mm);
        }

        internal static bool ShouldBindEvent(EventInfo ei)
        {
            return ei.GetAddMethod(true) is { } add && ShouldBindMethod(add);
        }

        private static ClassInfo GetClassInfo(Type type, ClassBase impl)
        {
            var ci = new ClassInfo();
            var methods = new Dictionary<string, List<MethodBase>>();
            MethodInfo meth;
            ExtensionType ob;
            string name;
            Type tp;
            int i, n;

            MemberInfo[] info = type.GetMembers(BindingFlags);
            var local = new HashSet<string>();
            var items = new List<MemberInfo>();
            MemberInfo m;

            // Loop through once to find out which names are declared
            for (i = 0; i < info.Length; i++)
            {
                m = info[i];
                if (m.DeclaringType == type)
                {
                    local.Add(m.Name);
                }
            }

            if (type.IsEnum)
            {
                var opsImpl = typeof(EnumOps<>).MakeGenericType(type);
                foreach (var op in opsImpl.GetMethods(OpsHelper.BindingFlags))
                {
                    local.Add(op.Name);
                }
                info = info.Concat(opsImpl.GetMethods(OpsHelper.BindingFlags)).ToArray();

                // only [Flags] enums support bitwise operations
                if (type.IsFlagsEnum())
                {
                    opsImpl = typeof(FlagEnumOps<>).MakeGenericType(type);
                    foreach (var op in opsImpl.GetMethods(OpsHelper.BindingFlags))
                    {
                        local.Add(op.Name);
                    }
                    info = info.Concat(opsImpl.GetMethods(OpsHelper.BindingFlags)).ToArray();
                }
            }

            // Now again to filter w/o losing overloaded member info
            for (i = 0; i < info.Length; i++)
            {
                m = info[i];
                if (local.Contains(m.Name))
                {
                    items.Add(m);
                }
            }

            if (type.IsInterface)
            {
                // Interface inheritance seems to be a different animal:
                // more contractual, less structural.  Thus, a Type that
                // represents an interface that inherits from another
                // interface does not return the inherited interface's
                // methods in GetMembers. For example ICollection inherits
                // from IEnumerable, but ICollection's GetMemebers does not
                // return GetEnumerator.
                //
                // Not sure if this is the correct way to fix this, but it
                // seems to work. Thanks to Bruce Dodson for the fix.

                Type[] inheritedInterfaces = type.GetInterfaces();

                for (i = 0; i < inheritedInterfaces.Length; ++i)
                {
                    Type inheritedType = inheritedInterfaces[i];
                    MemberInfo[] imembers = inheritedType.GetMembers(BindingFlags);
                    for (n = 0; n < imembers.Length; n++)
                    {
                        m = imembers[n];
                        if (!local.Contains(m.Name))
                        {
                            items.Add(m);
                        }
                    }
                }

                // All interface implementations inherit from Object,
                // but GetMembers don't return them either.
                var objFlags = BindingFlags.Public | BindingFlags.Instance;
                foreach (var mi in typeof(object).GetMembers(objFlags))
                {
                    if (!local.Contains(mi.Name) && mi is not ConstructorInfo)
                    {
                        items.Add(mi);
                    }
                }
            }

            for (i = 0; i < items.Count; i++)
            {
                var mi = (MemberInfo)items[i];

                switch (mi.MemberType)
                {
                    case MemberTypes.Method:
                        meth = (MethodInfo)mi;
                        if (!ShouldBindMethod(meth))
                        {
                            continue;
                        }
                        name = meth.Name;

                        //TODO mangle?
                        if (name == "__init__" && !impl.HasCustomNew())
                            continue;

                        if (!methods.TryGetValue(name, out var methodList))
                        {
                            methodList = methods[name] = new List<MethodBase>();
                        }
                        methodList.Add(meth);
                        continue;

                    case MemberTypes.Constructor when !impl.HasCustomNew():
                        var ctor = (ConstructorInfo)mi;
                        if (ctor.IsStatic)
                        {
                            continue;
                        }

                        name = "__init__";
                        if (!methods.TryGetValue(name, out methodList))
                        {
                            methodList = methods[name] = new List<MethodBase>();
                        }
                        methodList.Add(ctor);
                        continue;

                    case MemberTypes.Property:
                        var pi = (PropertyInfo)mi;

                        if(!ShouldBindProperty(pi))
                        {
                            continue;
                        }

                        // Check for indexer
                        ParameterInfo[] args = pi.GetIndexParameters();
                        if (args.GetLength(0) > 0)
                        {
                            Indexer? idx = ci.indexer;
                            if (idx == null)
                            {
                                ci.indexer = new Indexer();
                                idx = ci.indexer;
                            }
                            idx.AddProperty(pi);
                            continue;
                        }

                        ob = new PropertyObject(pi);
                        ci.members[pi.Name] = ob.AllocObject();
                        continue;

                    case MemberTypes.Field:
                        var fi = (FieldInfo)mi;
                        if (!ShouldBindField(fi))
                        {
                            continue;
                        }
                        ob = new FieldObject(fi);
                        ci.members[mi.Name] = ob.AllocObject();
                        continue;

                    case MemberTypes.Event:
                        var ei = (EventInfo)mi;
                        if (!ShouldBindEvent(ei))
                        {
                            continue;
                        }
                        ob = ei.AddMethod.IsStatic
                            ? new EventBinding(ei)
                            : new EventObject(ei);
                        ci.members[ei.Name] = ob.AllocObject();
                        continue;

                    case MemberTypes.NestedType:
                        tp = (Type)mi;
                        if (!(tp.IsNestedPublic || tp.IsNestedFamily ||
                              tp.IsNestedFamORAssem))
                        {
                            continue;
                        }
                        // Note the given instance might be uninitialized
                        var pyType = GetClass(tp);
                        // make a copy, that could be disposed later
                        ci.members[mi.Name] = new ReflectedClrType(pyType);
                        continue;
                }
            }

            foreach (var iter in methods)
            {
                name = iter.Key;
                var mlist = iter.Value.ToArray();

                ob = new MethodObject(type, name, mlist);
                ci.members[name] = ob.AllocObject();
                if (mlist.Any(OperatorMethod.IsOperatorMethod))
                {
                    string pyName = OperatorMethod.GetPyMethodName(name);
                    string pyNameReverse = OperatorMethod.ReversePyMethodName(pyName);
                    OperatorMethod.FilterMethods(mlist, out var forwardMethods, out var reverseMethods);
                    // Only methods where the left operand is the declaring type.
                    if (forwardMethods.Length > 0)
                        ci.members[pyName] = new MethodObject(type, name, forwardMethods).AllocObject();
                    // Only methods where only the right operand is the declaring type.
                    if (reverseMethods.Length > 0)
                        ci.members[pyNameReverse] = new MethodObject(type, name, reverseMethods, argsReversed: true).AllocObject();
                }
            }

            if (ci.indexer == null && type.IsClass)
            {
                // Indexer may be inherited.
                var parent = type.BaseType;
                while (parent != null && ci.indexer == null)
                {
                    foreach (var prop in parent.GetProperties()) {
                        var args = prop.GetIndexParameters();
                        if (args.GetLength(0) > 0)
                        {
                            ci.indexer = new Indexer();
                            ci.indexer.AddProperty(prop);
                            break;
                        }
                    }
                    parent = parent.BaseType;
                }
            }

            return ci;
        }

        /// <summary>
        /// This class owns references to PyObjects in the `members` member.
        /// The caller has responsibility to DECREF them.
        /// </summary>
        private class ClassInfo
        {
            public Indexer? indexer;
            public readonly Dictionary<string, PyObject> members = new();

            internal ClassInfo()
            {
                indexer = null;
            }
        }
    }

}
