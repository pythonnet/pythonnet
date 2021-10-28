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

        internal static void DisposePythonWrappersForClrTypes()
        {
            var visited = new HashSet<IntPtr>();
            var visitedHandle = GCHandle.Alloc(visited);
            var visitedPtr = (IntPtr)visitedHandle;
            try
            {
                foreach (var cls in cache.Values)
                {
                    // XXX: Force to release instance's managed resources
                    // but not dealloc itself immediately.
                    // These managed resources should preserve vacant shells
                    // since others may still referencing it.
                    BorrowedReference meta = Runtime.PyObject_TYPE(cls);
                    ManagedType.CallTypeTraverse(cls, meta, TraverseTypeClear, visitedPtr);
                    ManagedType.CallTypeClear(cls, meta);
                }
            }
            finally
            {
                visitedHandle.Free();
            }
            cache.Clear();
        }

        private static int TraverseTypeClear(BorrowedReference ob, IntPtr arg)
        {
            var visited = (HashSet<IntPtr>)GCHandle.FromIntPtr(arg).Target;
            if (!visited.Add(ob.DangerousGetAddressOrNull()))
            {
                return 0;
            }
            var clrObj = ManagedType.GetManagedObject(ob);
            if (clrObj != null)
            {
                BorrowedReference tp = Runtime.PyObject_TYPE(ob);
                ManagedType.CallTypeTraverse(ob, tp, TraverseTypeClear, arg);
                ManagedType.CallTypeClear(ob, tp);
            }
            return 0;
        }

        internal static ClassManagerState SaveRuntimeData()
        {
            var contexts = new Dictionary<ReflectedClrType, InterDomainContext>();
            foreach (var cls in cache)
            {
                if (!cls.Key.Valid)
                {
                    // Don't serialize an invalid class
                    continue;
                }
                var context = contexts[cls.Value] = new InterDomainContext();
                var cb = (ClassBase)ManagedType.GetManagedObject(cls.Value)!;
                cb.Save(cls.Value, context);

                // Remove all members added in InitBaseClass.
                // this is done so that if domain reloads and a member of a
                // reflected dotnet class is removed, it is removed from the
                // Python object's dictionary tool; thus raising an AttributeError
                // instead of a TypeError.
                // Classes are re-initialized on in RestoreRuntimeData.
                using var dict = Runtime.PyObject_GenericGetDict(cls.Value);
                foreach (var member in cb.dotNetMembers)
                {
                    // No need to decref the member, the ClassBase instance does 
                    // not own the reference.
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
                if (!pair.Key.Valid)
                {
                    invalidClasses.Add(pair);
                    continue;
                }

                pair.Value.Restore(contexts[pair.Value]);
            }
            
            foreach (var pair in invalidClasses)
            {
                cache.Remove(pair.Key);
                pair.Value.Dispose();
            }
        }

        /// <summary>
        /// Return the ClassBase-derived instance that implements a particular
        /// reflected managed type, creating it if it doesn't yet exist.
        /// </summary>
        internal static ReflectedClrType GetClass(Type type) => ReflectedClrType.GetOrCreate(type, out _);
        internal static ClassBase GetClassImpl(Type type)
        {
            ReflectedClrType.GetOrCreate(type, out var cb);
            return cb;
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

            else if (null != type.GetField("__pyobj__"))
            {
                impl = new ClassDerivedObject(type);
            }

            else
            {
                impl = new ClassObject(type);
            }


            return impl;
        }

        internal static void InitClassBase(Type type, ClassBase impl, PyType pyType)
        {
            // First, we introspect the managed type and build some class
            // information, including generating the member descriptors
            // that we'll be putting in the Python class __dict__.

            ClassInfo info = GetClassInfo(type);

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
                switch (item)
                {
                    case ClassBase nestedClass:
                        Runtime.PyDict_SetItemString(dict, name, GetClass(nestedClass.type.Value));
                        break;
                    case ExtensionType extension:
                        using (var pyRef = extension.Alloc())
                        {
                            Runtime.PyDict_SetItemString(dict, name, pyRef.Borrow());
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }
                if (ClassBase.CilToPyOpMap.TryGetValue(name, out var pyOp)
                    && item is MethodObject method)
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

            var co = impl as ClassObject;
            // If this is a ClassObject AND it has constructors, generate a __doc__ attribute.
            // required that the ClassObject.ctors be changed to internal
            if (co != null)
            {
                if (co.NumCtors > 0)
                {
                    // Implement Overloads on the class object
                    if (!CLRModule._SuppressOverloads)
                    {
                        using var ctors = new ConstructorBinding(type, pyType, co.binder).Alloc();
                        // ExtensionType types are untracked, so don't Incref() them.
                        // TODO: deprecate __overloads__ soon...
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__overloads__, ctors.Borrow());
                        Runtime.PyDict_SetItem(dict, PyIdentifier.Overloads, ctors.Borrow());
                    }

                    // don't generate the docstring if one was already set from a DocStringAttribute.
                    if (!CLRModule._SuppressDocs && doc.IsNull())
                    {
                        doc = co.GetDocString();
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, doc.Borrow());
                    }
                }
            }
            doc.Dispose();

            // The type has been modified after PyType_Ready has been called
            // Refresh the type
            Runtime.PyType_Modified(pyType.Reference);
        }

        internal static bool ShouldBindMethod(MethodBase mb)
        {
            return (mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly);
        }

        internal static bool ShouldBindField(FieldInfo fi)
        {
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
            return ShouldBindMethod(ei.GetAddMethod(true));
        }

        private static ClassInfo GetClassInfo(Type type)
        {
            var ci = new ClassInfo();
            var methods = new Dictionary<string, List<MethodInfo>>();
            MethodInfo meth;
            ManagedType ob;
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

            // only [Flags] enums support bitwise operations
            if (type.IsEnum && type.IsFlagsEnum())
            {
                var opsImpl = typeof(EnumOps<>).MakeGenericType(type);
                foreach (var op in opsImpl.GetMethods(OpsHelper.BindingFlags))
                {
                    local.Add(op.Name);
                }
                info = info.Concat(opsImpl.GetMethods(OpsHelper.BindingFlags)).ToArray();
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
                        if (!methods.TryGetValue(name, out var methodList))
                        {
                            methodList = methods[name] = new List<MethodInfo>();
                        }
                        methodList.Add(meth);
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
                        ci.members[pi.Name] = ob;
                        continue;

                    case MemberTypes.Field:
                        var fi = (FieldInfo)mi;
                        if (!ShouldBindField(fi))
                        {
                            continue;
                        }
                        ob = new FieldObject(fi);
                        ci.members[mi.Name] = ob;
                        continue;

                    case MemberTypes.Event:
                        var ei = (EventInfo)mi;
                        if (!ShouldBindEvent(ei))
                        {
                            continue;
                        }
                        ob = new EventObject(ei);
                        ci.members[ei.Name] = ob;
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
                        ob = ManagedType.GetManagedObject(pyType)!;
                        Debug.Assert(ob is not null);
                        ci.members[mi.Name] = ob;
                        continue;
                }
            }

            foreach (var iter in methods)
            {
                name = iter.Key;
                var mlist = iter.Value.ToArray();

                ob = new MethodObject(type, name, mlist);
                ci.members[name] = ob;
                if (mlist.Any(OperatorMethod.IsOperatorMethod))
                {
                    string pyName = OperatorMethod.GetPyMethodName(name);
                    string pyNameReverse = OperatorMethod.ReversePyMethodName(pyName);
                    OperatorMethod.FilterMethods(mlist, out var forwardMethods, out var reverseMethods);
                    // Only methods where the left operand is the declaring type.
                    if (forwardMethods.Length > 0)
                        ci.members[pyName] = new MethodObject(type, name, forwardMethods);
                    // Only methods where only the right operand is the declaring type.
                    if (reverseMethods.Length > 0)
                        ci.members[pyNameReverse] = new MethodObject(type, name, reverseMethods);
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
            public readonly Dictionary<string, ManagedType> members = new();

            internal ClassInfo()
            {
                indexer = null;
            }
        }
    }

}
