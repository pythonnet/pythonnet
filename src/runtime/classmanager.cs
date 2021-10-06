using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;

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

        private static Dictionary<MaybeType, ClassBase> cache;
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
            cache = new Dictionary<MaybeType, ClassBase>(128);
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
                    cls.CallTypeTraverse(TraverseTypeClear, visitedPtr);
                    cls.CallTypeClear();
                }
            }
            finally
            {
                visitedHandle.Free();
            }
            cache.Clear();
        }

        private static int TraverseTypeClear(IntPtr ob, IntPtr arg)
        {
            var visited = (HashSet<IntPtr>)GCHandle.FromIntPtr(arg).Target;
            if (!visited.Add(ob))
            {
                return 0;
            }
            var clrObj = ManagedType.GetManagedObject(ob);
            if (clrObj != null)
            {
                clrObj.CallTypeTraverse(TraverseTypeClear, arg);
                clrObj.CallTypeClear();
            }
            return 0;
        }

        internal static void SaveRuntimeData(RuntimeDataStorage storage)
        {
            var contexts = storage.AddValue("contexts",
                new Dictionary<IntPtr, InterDomainContext>());
            storage.AddValue("cache", cache);
            foreach (var cls in cache)
            {
                if (!cls.Key.Valid)
                {
                    // Don't serialize an invalid class
                    continue;
                }
                // This incref is for cache to hold the cls,
                // thus no need for decreasing it at RestoreRuntimeData.
                Runtime.XIncref(cls.Value.pyHandle);
                var context = contexts[cls.Value.pyHandle] = new InterDomainContext();
                cls.Value.Save(context);

                // Remove all members added in InitBaseClass.
                // this is done so that if domain reloads and a member of a
                // reflected dotnet class is removed, it is removed from the
                // Python object's dictionary tool; thus raising an AttributeError
                // instead of a TypeError.
                // Classes are re-initialized on in RestoreRuntimeData.
                using var dict = Runtime.PyObject_GenericGetDict(cls.Value.TypeReference);
                foreach (var member in cls.Value.dotNetMembers)
                {
                    // No need to decref the member, the ClassBase instance does 
                    // not own the reference.
                    if ((Runtime.PyDict_DelItemString(dict, member) == -1) &&
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
                Runtime.PyType_Modified(cls.Value.TypeReference);
            }
        }

        internal static Dictionary<ManagedType, InterDomainContext> RestoreRuntimeData(RuntimeDataStorage storage)
        {
            cache = storage.GetValue<Dictionary<MaybeType, ClassBase>>("cache");
            var invalidClasses = new List<KeyValuePair<MaybeType, ClassBase>>();
            var contexts = storage.GetValue <Dictionary<IntPtr, InterDomainContext>>("contexts");
            var loadedObjs = new Dictionary<ManagedType, InterDomainContext>();
            foreach (var pair in cache)
            {
                if (!pair.Key.Valid)
                {
                    invalidClasses.Add(pair);
                    continue;
                }
                // Ensure, that matching Python type exists first.
                // It is required for self-referential classes
                // (e.g. with members, that refer to the same class)
                var pyType = InitPyType(pair.Key.Value, pair.Value);
                // re-init the class
                InitClassBase(pair.Key.Value, pair.Value, pyType);
                // We modified the Type object, notify it we did.
                Runtime.PyType_Modified(pair.Value.TypeReference);
                var context = contexts[pair.Value.pyHandle];
                pair.Value.Load(context);
                var slotsHolder = TypeManager.GetSlotsHolder(pyType);
                pair.Value.InitializeSlots(slotsHolder);
                Runtime.PyType_Modified(pair.Value.TypeReference);
                loadedObjs.Add(pair.Value, context);
            }
            
            foreach (var pair in invalidClasses)
            {
                cache.Remove(pair.Key);
                Runtime.XDecref(pair.Value.pyHandle);
            }

            return loadedObjs;
        }

        /// <summary>
        /// Return the ClassBase-derived instance that implements a particular
        /// reflected managed type, creating it if it doesn't yet exist.
        /// </summary>
        /// <returns>A Borrowed reference to the ClassBase object</returns>
        internal static ClassBase GetClass(Type type)
        {
            ClassBase cb = null;
            cache.TryGetValue(type, out cb);
            if (cb != null)
            {
                return cb;
            }
            cb = CreateClass(type);
            cache.Add(type, cb);
            // Ensure, that matching Python type exists first.
            // It is required for self-referential classes
            // (e.g. with members, that refer to the same class)
            var pyType = InitPyType(type, cb);
            // Initialize the object later, as this might call this GetClass method
            // recursively (for example when a nested class inherits its declaring class...)
            InitClassBase(type, cb, pyType);
            return cb;
        }


        /// <summary>
        /// Create a new ClassBase-derived instance that implements a reflected
        /// managed type. The new object will be associated with a generated
        /// Python type object.
        /// </summary>
        private static ClassBase CreateClass(Type type)
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

        private static PyType InitPyType(Type type, ClassBase impl)
        {
            var pyType = TypeManager.GetOrCreateClass(type);

            // Set the handle attributes on the implementing instance.
            impl.tpHandle = impl.pyHandle = pyType.Handle;

            return pyType;
        }

        private static void InitClassBase(Type type, ClassBase impl, PyType pyType)
        {
            // First, we introspect the managed type and build some class
            // information, including generating the member descriptors
            // that we'll be putting in the Python class __dict__.

            ClassInfo info = GetClassInfo(type);

            impl.indexer = info.indexer;
            impl.richcompare = new Dictionary<int, MethodObject>();

            // Now we force initialize the Python type object to reflect the given
            // managed type, filling the Python type slots with thunks that
            // point to the managed methods providing the implementation.


            TypeManager.GetOrInitializeClass(impl, type);

            // Finally, initialize the class __dict__ and return the object.
            using var dict = Runtime.PyObject_GenericGetDict(pyType.Reference);


            if (impl.dotNetMembers == null)
            {
                impl.dotNetMembers = new List<string>();
            }
            IDictionaryEnumerator iter = info.members.GetEnumerator();
            while (iter.MoveNext())
            {
                var item = (ManagedType)iter.Value;
                var name = (string)iter.Key;
                impl.dotNetMembers.Add(name);
                Runtime.PyDict_SetItemString(dict, name, item.ObjectReference);
                // Decref the item now that it's been used.
                item.DecrRefCount();
                if (ClassBase.CilToPyOpMap.TryGetValue(name, out var pyOp)) {
                    impl.richcompare.Add(pyOp, (MethodObject)item);
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
                doc = NewReference.DangerousFromPointer(Runtime.PyString_FromString(docStr));
                Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, doc);
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
                        var ctors = new ConstructorBinding(type, pyType, co.binder);
                        // ExtensionType types are untracked, so don't Incref() them.
                        // TODO: deprecate __overloads__ soon...
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__overloads__, ctors.ObjectReference);
                        Runtime.PyDict_SetItem(dict, PyIdentifier.Overloads, ctors.ObjectReference);
                        ctors.DecrRefCount();
                    }

                    // don't generate the docstring if one was already set from a DocStringAttribute.
                    if (!CLRModule._SuppressDocs && doc.IsNull())
                    {
                        doc = co.GetDocString();
                        Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, doc);
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
                MethodInfo mm = null;
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
            var methods = new Hashtable();
            ArrayList list;
            MethodInfo meth;
            ManagedType ob;
            string name;
            object item;
            Type tp;
            int i, n;

            MemberInfo[] info = type.GetMembers(BindingFlags);
            var local = new Hashtable();
            var items = new ArrayList();
            MemberInfo m;

            // Loop through once to find out which names are declared
            for (i = 0; i < info.Length; i++)
            {
                m = info[i];
                if (m.DeclaringType == type)
                {
                    local[m.Name] = 1;
                }
            }

            // only [Flags] enums support bitwise operations
            if (type.IsEnum && type.IsFlagsEnum())
            {
                var opsImpl = typeof(EnumOps<>).MakeGenericType(type);
                foreach (var op in opsImpl.GetMethods(OpsHelper.BindingFlags))
                {
                    local[op.Name] = 1;
                }
                info = info.Concat(opsImpl.GetMethods(OpsHelper.BindingFlags)).ToArray();
            }

            // Now again to filter w/o losing overloaded member info
            for (i = 0; i < info.Length; i++)
            {
                m = info[i];
                if (local[m.Name] != null)
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
                        if (local[m.Name] == null)
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
                    if (local[mi.Name] == null)
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
                        item = methods[name];
                        if (item == null)
                        {
                            item = methods[name] = new ArrayList();
                        }
                        list = (ArrayList)item;
                        list.Add(meth);
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
                            Indexer idx = ci.indexer;
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
                        ob = GetClass(tp);
                        if (ob.pyHandle == IntPtr.Zero && ob is ClassObject)
                        {
                            ob.pyHandle = ob.tpHandle = TypeManager.GetOrCreateClass(tp).Handle;
                        }
                        Debug.Assert(ob.pyHandle != IntPtr.Zero);
                        // GetClass returns a Borrowed ref. ci.members owns the reference.
                        ob.IncrRefCount();
                        ci.members[mi.Name] = ob;
                        continue;
                }
            }

            IDictionaryEnumerator iter = methods.GetEnumerator();

            while (iter.MoveNext())
            {
                name = (string)iter.Key;
                list = (ArrayList)iter.Value;

                var mlist = (MethodInfo[])list.ToArray(typeof(MethodInfo));

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
            public Indexer indexer;
            public Hashtable members;

            internal ClassInfo()
            {
                members = new Hashtable();
                indexer = null;
            }
        }
    }

}
