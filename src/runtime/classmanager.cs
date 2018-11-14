using System;
using System.Collections;
using System.Collections.Generic;
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
        private static Dictionary<Type, ClassBase> cache;
        private static Type dtype;

        private ClassManager()
        {
        }

        static ClassManager()
        {
            cache = new Dictionary<Type, ClassBase>(128);
            // SEE: https://msdn.microsoft.com/en-us/library/96b1ayy4(v=vs.100).aspx
            // ""All delegates inherit from MulticastDelegate, which inherits from Delegate.""
            // Was Delegate, which caused a null MethodInfo returned from GetMethode("Invoke")
            // and crashed on Linux under Mono.
            dtype = typeof(MulticastDelegate);
        }

        public static void Reset()
        {
            cache = new Dictionary<Type, ClassBase>(128);
        }

        /// <summary>
        /// Return the ClassBase-derived instance that implements a particular
        /// reflected managed type, creating it if it doesn't yet exist.
        /// </summary>
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
            // Initialize the object later, as this might call this GetClass method
            // recursively (for example when a nested class inherits its declaring class...)
            InitClassBase(type, cb);
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

        private static void InitClassBase(Type type, ClassBase impl)
        {
            // First, we introspect the managed type and build some class
            // information, including generating the member descriptors
            // that we'll be putting in the Python class __dict__.

            ClassInfo info = GetClassInfo(type);

            impl.indexer = info.indexer;

            // Now we allocate the Python type object to reflect the given
            // managed type, filling the Python type slots with thunks that
            // point to the managed methods providing the implementation.


            IntPtr tp = TypeManager.GetTypeHandle(impl, type);
            impl.tpHandle = tp;

            // Finally, initialize the class __dict__ and return the object.
            IntPtr dict = Marshal.ReadIntPtr(tp, TypeOffset.tp_dict);


            IDictionaryEnumerator iter = info.members.GetEnumerator();
            while (iter.MoveNext())
            {
                var item = (ManagedType)iter.Value;
                var name = (string)iter.Key;
                Runtime.PyDict_SetItemString(dict, name, item.pyHandle);
            }

            // If class has constructors, generate an __doc__ attribute.
            IntPtr doc = IntPtr.Zero;
            Type marker = typeof(DocStringAttribute);
            var attrs = (Attribute[])type.GetCustomAttributes(marker, false);
            if (attrs.Length == 0)
            {
                doc = IntPtr.Zero;
            }
            else
            {
                var attr = (DocStringAttribute)attrs[0];
                string docStr = attr.DocString;
                doc = Runtime.PyString_FromString(docStr);
                Runtime.PyDict_SetItemString(dict, "__doc__", doc);
                Runtime.XDecref(doc);
            }

            var co = impl as ClassObject;
            // If this is a ClassObject AND it has constructors, generate a __doc__ attribute.
            // required that the ClassObject.ctors be changed to internal
            if (co != null)
            {
                if (co.ctors.Length > 0)
                {
                    // Implement Overloads on the class object
                    if (!CLRModule._SuppressOverloads)
                    {
                        var ctors = new ConstructorBinding(type, tp, co.binder);
                        // ExtensionType types are untracked, so don't Incref() them.
                        // TODO: deprecate __overloads__ soon...
                        Runtime.PyDict_SetItemString(dict, "__overloads__", ctors.pyHandle);
                        Runtime.PyDict_SetItemString(dict, "Overloads", ctors.pyHandle);
                    }

                    // don't generate the docstring if one was already set from a DocStringAttribute.
                    if (!CLRModule._SuppressDocs && doc == IntPtr.Zero)
                    {
                        doc = co.GetDocString();
                        Runtime.PyDict_SetItemString(dict, "__doc__", doc);
                        Runtime.XDecref(doc);
                    }
                }
            }
        }

        private static ClassInfo GetClassInfo(Type type)
        {
            var ci = new ClassInfo(type);
            var methods = new Hashtable();
            ArrayList list;
            MethodInfo meth;
            ManagedType ob;
            string name;
            object item;
            Type tp;
            int i, n;

            // This is complicated because inheritance in Python is name
            // based. We can't just find DeclaredOnly members, because we
            // could have a base class A that defines two overloads of a
            // method and a class B that defines two more. The name-based
            // descriptor Python will find needs to know about inherited
            // overloads as well as those declared on the sub class.
            BindingFlags flags = BindingFlags.Static |
                                 BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;

            MemberInfo[] info = type.GetMembers(flags);
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
                    MemberInfo[] imembers = inheritedType.GetMembers(flags);
                    for (n = 0; n < imembers.Length; n++)
                    {
                        m = imembers[n];
                        if (local[m.Name] == null)
                        {
                            items.Add(m);
                        }
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
                        if (!(meth.IsPublic || meth.IsFamily ||
                              meth.IsFamilyOrAssembly))
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
                            continue;
                        }

                        if (mm == null)
                        {
                            continue;
                        }

                        if (!(mm.IsPublic || mm.IsFamily || mm.IsFamilyOrAssembly))
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
                        if (!(fi.IsPublic || fi.IsFamily || fi.IsFamilyOrAssembly))
                        {
                            continue;
                        }
                        ob = new FieldObject(fi);
                        ci.members[mi.Name] = ob;
                        continue;

                    case MemberTypes.Event:
                        var ei = (EventInfo)mi;
                        MethodInfo me = ei.GetAddMethod(true);
                        if (!(me.IsPublic || me.IsFamily || me.IsFamilyOrAssembly))
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
            }

            return ci;
        }
    }


    internal class ClassInfo
    {
        public Indexer indexer;
        public Hashtable members;

        internal ClassInfo(Type t)
        {
            members = new Hashtable();
            indexer = null;
        }
    }
}
