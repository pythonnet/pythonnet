using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type that provides access to CLR namespaces. The
    /// type behaves like a Python module, and can contain other sub-modules.
    /// </summary>
    internal class ModuleObject : ExtensionType
    {
        private Dictionary<string, ManagedType> cache;
        internal string moduleName;
        internal IntPtr dict;
        protected string _namespace;

        public ModuleObject(string name)
        {
            if (name == string.Empty)
            {
                throw new ArgumentException("Name must not be empty!");
            }
            moduleName = name;
            cache = new Dictionary<string, ManagedType>();
            _namespace = name;

            // Use the filename from any of the assemblies just so there's something for
            // anything that expects __file__ to be set.
            var filename = "unknown";
            var docstring = "Namespace containing types from the following assemblies:\n\n";
            foreach (Assembly a in AssemblyManager.GetAssemblies(name))
            {
                if (!a.IsDynamic && a.Location != null)
                {
                    filename = a.Location;
                }
                docstring += "- " + a.FullName + "\n";
            }

            dict = Runtime.PyDict_New();
            IntPtr pyname = Runtime.PyString_FromString(moduleName);
            IntPtr pyfilename = Runtime.PyString_FromString(filename);
            IntPtr pydocstring = Runtime.PyString_FromString(docstring);
            IntPtr pycls = TypeManager.GetTypeHandle(GetType());
            Runtime.PyDict_SetItemString(dict, "__name__", pyname);
            Runtime.PyDict_SetItemString(dict, "__file__", pyfilename);
            Runtime.PyDict_SetItemString(dict, "__doc__", pydocstring);
            Runtime.PyDict_SetItemString(dict, "__class__", pycls);
            Runtime.XDecref(pyname);
            Runtime.XDecref(pyfilename);
            Runtime.XDecref(pydocstring);

            Marshal.WriteIntPtr(pyHandle, ObjectOffset.DictOffset(pyHandle), dict);

            InitializeModuleMembers();
        }


        /// <summary>
        /// Returns a ClassBase object representing a type that appears in
        /// this module's namespace or a ModuleObject representing a child
        /// namespace (or null if the name is not found). This method does
        /// not increment the Python refcount of the returned object.
        /// </summary>
        public ManagedType GetAttribute(string name, bool guess)
        {
            ManagedType cached = null;
            cache.TryGetValue(name, out cached);
            if (cached != null)
            {
                return cached;
            }

            ModuleObject m;
            ClassBase c;
            Type type;

            //if (AssemblyManager.IsValidNamespace(name))
            //{
            //    IntPtr py_mod_name = Runtime.PyString_FromString(name);
            //    IntPtr modules = Runtime.PyImport_GetModuleDict();
            //    IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);
            //    if (module != IntPtr.Zero)
            //        return (ManagedType)this;
            //    return null;
            //}

            string qname = _namespace == string.Empty
                ? name
                : _namespace + "." + name;

            // If the fully-qualified name of the requested attribute is
            // a namespace exported by a currently loaded assembly, return
            // a new ModuleObject representing that namespace.
            if (AssemblyManager.IsValidNamespace(qname))
            {
                m = new ModuleObject(qname);
                StoreAttribute(name, m);
                return m;
            }

            // Look for a type in the current namespace. Note that this
            // includes types, delegates, enums, interfaces and structs.
            // Only public namespace members are exposed to Python.
            type = AssemblyManager.LookupType(qname);
            if (type != null)
            {
                if (!type.IsPublic)
                {
                    return null;
                }
                c = ClassManager.GetClass(type);
                StoreAttribute(name, c);
                return c;
            }

            // This is a little repetitive, but it ensures that the right
            // thing happens with implicit assembly loading at a reasonable
            // cost. Ask the AssemblyManager to do implicit loading for each
            // of the steps in the qualified name, then try it again.
            bool ignore = name.StartsWith("__");
            if (AssemblyManager.LoadImplicit(qname, !ignore))
            {
                if (AssemblyManager.IsValidNamespace(qname))
                {
                    m = new ModuleObject(qname);
                    StoreAttribute(name, m);
                    return m;
                }

                type = AssemblyManager.LookupType(qname);
                if (type != null)
                {
                    if (!type.IsPublic)
                    {
                        return null;
                    }
                    c = ClassManager.GetClass(type);
                    StoreAttribute(name, c);
                    return c;
                }
            }

            // We didn't find the name, so we may need to see if there is a
            // generic type with this base name. If so, we'll go ahead and
            // return it. Note that we store the mapping of the unmangled
            // name to generic type -  it is technically possible that some
            // future assembly load could contribute a non-generic type to
            // the current namespace with the given basename, but unlikely
            // enough to complicate the implementation for now.
            if (guess)
            {
                string gname = GenericUtil.GenericNameForBaseName(_namespace, name);
                if (gname != null)
                {
                    ManagedType o = GetAttribute(gname, false);
                    if (o != null)
                    {
                        StoreAttribute(name, o);
                        return o;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Stores an attribute in the instance dict for future lookups.
        /// </summary>
        private void StoreAttribute(string name, ManagedType ob)
        {
            Runtime.PyDict_SetItemString(dict, name, ob.pyHandle);
            cache[name] = ob;
        }


        /// <summary>
        /// Preloads all currently-known names for the module namespace. This
        /// can be called multiple times, to add names from assemblies that
        /// may have been loaded since the last call to the method.
        /// </summary>
        public void LoadNames()
        {
            ManagedType m = null;
            foreach (string name in AssemblyManager.GetNames(_namespace))
            {
                cache.TryGetValue(name, out m);
                if (m != null)
                {
                    continue;
                }
                IntPtr attr = Runtime.PyDict_GetItemString(dict, name);
                // If __dict__ has already set a custom property, skip it.
                if (attr != IntPtr.Zero)
                {
                    continue;
                }
                GetAttribute(name, true);
            }
        }

        /// <summary>
        /// Initialize module level functions and attributes
        /// </summary>
        internal void InitializeModuleMembers()
        {
            Type funcmarker = typeof(ModuleFunctionAttribute);
            Type propmarker = typeof(ModulePropertyAttribute);
            Type ftmarker = typeof(ForbidPythonThreadsAttribute);
            Type type = GetType();

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(flags);
                foreach (MethodInfo method in methods)
                {
                    object[] attrs = method.GetCustomAttributes(funcmarker, false);
                    object[] forbid = method.GetCustomAttributes(ftmarker, false);
                    bool allow_threads = forbid.Length == 0;
                    if (attrs.Length > 0)
                    {
                        string name = method.Name;
                        var mi = new MethodInfo[1];
                        mi[0] = method;
                        var m = new ModuleFunctionObject(type, name, mi, allow_threads);
                        StoreAttribute(name, m);
                    }
                }

                PropertyInfo[] properties = type.GetProperties();
                foreach (PropertyInfo property in properties)
                {
                    object[] attrs = property.GetCustomAttributes(propmarker, false);
                    if (attrs.Length > 0)
                    {
                        string name = property.Name;
                        var p = new ModulePropertyObject(property);
                        StoreAttribute(name, p);
                    }
                }
                type = type.BaseType;
            }
        }


        /// <summary>
        /// ModuleObject __getattribute__ implementation. Module attributes
        /// are always either classes or sub-modules representing subordinate
        /// namespaces. CLR modules implement a lazy pattern - the sub-modules
        /// and classes are created when accessed and cached for future use.
        /// </summary>
        public static IntPtr tp_getattro(IntPtr ob, IntPtr key)
        {
            var self = (ModuleObject)GetManagedObject(ob);

            if (!Runtime.PyString_Check(key))
            {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return IntPtr.Zero;
            }

            IntPtr op = Runtime.PyDict_GetItem(self.dict, key);
            if (op != IntPtr.Zero)
            {
                Runtime.XIncref(op);
                return op;
            }

            string name = Runtime.GetManagedString(key);
            if (name == "__dict__")
            {
                Runtime.XIncref(self.dict);
                return self.dict;
            }

            ManagedType attr = self.GetAttribute(name, true);

            if (attr == null)
            {
                Exceptions.SetError(Exceptions.AttributeError, name);
                return IntPtr.Zero;
            }

            Runtime.XIncref(attr.pyHandle);
            return attr.pyHandle;
        }

        /// <summary>
        /// ModuleObject __repr__ implementation.
        /// </summary>
        public static IntPtr tp_repr(IntPtr ob)
        {
            var self = (ModuleObject)GetManagedObject(ob);
            return Runtime.PyString_FromString($"<module '{self.moduleName}'>");
        }
    }

    /// <summary>
    /// The CLR module is the root handler used by the magic import hook
    /// to import assemblies. It has a fixed module name "clr" and doesn't
    /// provide a namespace.
    /// </summary>
    internal class CLRModule : ModuleObject
    {
        protected static bool hacked = false;
        protected static bool interactive_preload = true;
        internal static bool preload;
        // XXX Test performance of new features //
        internal static bool _SuppressDocs = false;
        internal static bool _SuppressOverloads = false;

        public CLRModule() : base("clr")
        {
            _namespace = string.Empty;

            // This hackery is required in order to allow a plain Python to
            // import the managed runtime via the CLR bootstrapper module.
            // The standard Python machinery in control at the time of the
            // import requires the module to pass PyModule_Check. :(
            if (!hacked)
            {
                IntPtr type = tpHandle;
                IntPtr mro = Marshal.ReadIntPtr(type, TypeOffset.tp_mro);
                IntPtr ext = Runtime.ExtendTuple(mro, Runtime.PyModuleType);
                Marshal.WriteIntPtr(type, TypeOffset.tp_mro, ext);
                Runtime.XDecref(mro);
                hacked = true;
            }
        }

        public static void Reset()
        {
            hacked = false;
            interactive_preload = true;
            preload = false;

            // XXX Test performance of new features //
            _SuppressDocs = false;
            _SuppressOverloads = false;
        }

        /// <summary>
        /// The initializing of the preload hook has to happen as late as
        /// possible since sys.ps1 is created after the CLR module is
        /// created.
        /// </summary>
        internal void InitializePreload()
        {
            if (interactive_preload)
            {
                interactive_preload = false;
                if (Runtime.PySys_GetObject("ps1") != IntPtr.Zero)
                {
                    preload = true;
                }
                else
                {
                    Exceptions.Clear();
                    preload = false;
                }
            }
        }

        [ModuleFunction]
        public static bool getPreload()
        {
            return preload;
        }

        [ModuleFunction]
        public static void setPreload(bool preloadFlag)
        {
            preload = preloadFlag;
        }

        //[ModuleProperty]
        public static bool SuppressDocs
        {
            get { return _SuppressDocs; }
            set { _SuppressDocs = value; }
        }

        //[ModuleProperty]
        public static bool SuppressOverloads
        {
            get { return _SuppressOverloads; }
            set { _SuppressOverloads = value; }
        }

        [ModuleFunction]
        [ForbidPythonThreads]
        public static Assembly AddReference(string name)
        {
            AssemblyManager.UpdatePath();
            Assembly assembly = null;
            assembly = AssemblyManager.FindLoadedAssembly(name);
            if (assembly == null)
            {
                assembly = AssemblyManager.LoadAssemblyPath(name);
            }
            if (assembly == null)
            {
                assembly = AssemblyManager.LoadAssembly(name);
            }
            if (assembly == null)
            {
                assembly = AssemblyManager.LoadAssemblyFullPath(name);
            }
            if (assembly == null)
            {
                throw new FileNotFoundException($"Unable to find assembly '{name}'.");
            }

            return assembly;
        }

        /// <summary>
        /// Get a Type instance for a class object.
        /// clr.GetClrType(IComparable) gives you the Type for IComparable,
        /// that you can e.g. perform reflection on. Similar to typeof(IComparable) in C#
        /// or clr.GetClrType(IComparable) in IronPython.
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns>The Type object</returns>

        [ModuleFunction]
        [ForbidPythonThreads]
        public static Type GetClrType(Type type)
        {
            return type;
        }

        [ModuleFunction]
        [ForbidPythonThreads]
        public static string FindAssembly(string name)
        {
            AssemblyManager.UpdatePath();
            return AssemblyManager.FindAssembly(name);
        }

        [ModuleFunction]
        public static string[] ListAssemblies(bool verbose)
        {
            AssemblyName[] assnames = AssemblyManager.ListAssemblies();
            var names = new string[assnames.Length];
            for (var i = 0; i < assnames.Length; i++)
            {
                if (verbose)
                {
                    names[i] = assnames[i].FullName;
                }
                else
                {
                    names[i] = assnames[i].Name;
                }
            }
            return names;
        }

        [ModuleFunction]
        public static int _AtExit()
        {
            return Runtime.AtExit();
        }
    }
}
