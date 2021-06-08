using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type that provides access to CLR namespaces. The
    /// type behaves like a Python module, and can contain other sub-modules.
    /// </summary>
    [Serializable]
    internal class ModuleObject : ExtensionType
    {
        private Dictionary<string, ManagedType> cache;

        internal string moduleName;
        internal IntPtr dict;
        internal BorrowedReference DictRef => new BorrowedReference(dict);
        protected string _namespace;
        private IntPtr __all__ = IntPtr.Zero;

        // Attributes to be set on the module according to PEP302 and 451
        // by the import machinery.
        static readonly HashSet<string> settableAttributes = 
            new HashSet<string> {"__spec__", "__file__", "__name__", "__path__", "__loader__", "__package__"};

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

            var dictRef = Runtime.PyObject_GenericGetDict(ObjectReference);
            PythonException.ThrowIfIsNull(dictRef);
            dict = dictRef.DangerousMoveToPointer();
            __all__ = Runtime.PyList_New(0);
            using var pyname = NewReference.DangerousFromPointer(Runtime.PyString_FromString(moduleName));
            using var pyfilename = NewReference.DangerousFromPointer(Runtime.PyString_FromString(filename));
            using var pydocstring = NewReference.DangerousFromPointer(Runtime.PyString_FromString(docstring));
            BorrowedReference pycls = TypeManager.GetTypeReference(GetType());
            Runtime.PyDict_SetItem(DictRef, PyIdentifier.__name__, pyname);
            Runtime.PyDict_SetItem(DictRef, PyIdentifier.__file__, pyfilename);
            Runtime.PyDict_SetItem(DictRef, PyIdentifier.__doc__, pydocstring);
            Runtime.PyDict_SetItem(DictRef, PyIdentifier.__class__, pycls);

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
                m.DecrRefCount();
                return m;
            }

            // Look for a type in the current namespace. Note that this
            // includes types, delegates, enums, interfaces and structs.
            // Only public namespace members are exposed to Python.
            type = AssemblyManager.LookupTypes(qname).FirstOrDefault(t => t.IsPublic);
            if (type != null)
            {
                c = ClassManager.GetClass(type);
                StoreAttribute(name, c);
                return c;
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

        static void ImportWarning(Exception exception)
        {
            Exceptions.warn(exception.ToString(), Exceptions.ImportWarning);
        }


        /// <summary>
        /// Stores an attribute in the instance dict for future lookups.
        /// </summary>
        private void StoreAttribute(string name, ManagedType ob)
        {
            if (Runtime.PyDict_SetItemString(dict, name, ob.pyHandle) != 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            ob.IncrRefCount();
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
                BorrowedReference attr = Runtime.PyDict_GetItemString(DictRef, name);
                // If __dict__ has already set a custom property, skip it.
                if (!attr.IsNull)
                {
                    continue;
                }

                if(GetAttribute(name, true) != null)
                {
                    // if it's a valid attribute, add it to __all__
                    var pyname = Runtime.PyString_FromString(name);
                    try
                    {
                        if (Runtime.PyList_Append(new BorrowedReference(__all__), new BorrowedReference(pyname)) != 0)
                        {
                            throw PythonException.ThrowLastAsClrException();
                        }
                    }
                    finally
                    {
                        Runtime.XDecref(pyname);
                    }
                }
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
                        m.DecrRefCount();
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
                        p.DecrRefCount();
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

            string name = InternString.GetManagedString(key);
            if (name == "__dict__")
            {
                Runtime.XIncref(self.dict);
                return self.dict;
            }

            if (name == "__all__")
            {
                self.LoadNames();
                Runtime.XIncref(self.__all__);
                return self.__all__;
            }

            ManagedType attr = null;

            try
            {
                attr = self.GetAttribute(name, true);
            }
            catch (Exception e)
            {
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }


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

        public static int tp_traverse(IntPtr ob, IntPtr visit, IntPtr arg)
        {
            var self = (ModuleObject)GetManagedObject(ob);
            int res = PyVisit(self.dict, visit, arg);
            if (res != 0) return res;
            foreach (var attr in self.cache.Values)
            {
                res = PyVisit(attr.pyHandle, visit, arg);
                if (res != 0) return res;
            }
            return 0;
        }

        protected override void Clear()
        {
            Runtime.Py_CLEAR(ref this.dict);
            ClearObjectDict(this.pyHandle);
            foreach (var attr in this.cache.Values)
            {
                Runtime.XDecref(attr.pyHandle);
            }
            this.cache.Clear();
            base.Clear();
        }

        /// <summary>
        /// Override the setattr implementation.
        /// This is needed because the import mechanics need
        /// to set a few attributes
        /// </summary>
        [ForbidPythonThreads]
        public new static int tp_setattro(IntPtr ob, IntPtr key, IntPtr val)
        {
            var managedKey = Runtime.GetManagedString(key);
            if ((settableAttributes.Contains(managedKey)) || 
                (ManagedType.GetManagedObject(val)?.GetType() == typeof(ModuleObject)) )
            {
                var self = (ModuleObject)ManagedType.GetManagedObject(ob);
                return Runtime.PyDict_SetItem(self.dict, key, val);
            }

            return ExtensionType.tp_setattro(ob, key, val);
        }

        protected override void OnSave(InterDomainContext context)
        {
            base.OnSave(context);
            System.Diagnostics.Debug.Assert(dict == GetObjectDict(pyHandle));
            foreach (var attr in cache.Values)
            {
                Runtime.XIncref(attr.pyHandle);
            }
            // Decref twice in tp_clear, equilibrate them.
            Runtime.XIncref(dict);
            Runtime.XIncref(dict);
            // destroy the cache(s)
            foreach (var pair in cache)
            {
                if ((Runtime.PyDict_DelItemString(DictRef, pair.Key) == -1) &&
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
                pair.Value.DecrRefCount();
            }

            cache.Clear();
        }

        protected override void OnLoad(InterDomainContext context)
        {
            base.OnLoad(context);
            SetObjectDict(pyHandle, dict);
        }
    }

    /// <summary>
    /// The CLR module is the root handler used by the magic import hook
    /// to import assemblies. It has a fixed module name "clr" and doesn't
    /// provide a namespace.
    /// </summary>
    [Serializable]
    internal class CLRModule : ModuleObject
    {
        protected static bool hacked = false;
        protected static bool interactive_preload = true;
        internal static bool preload;
        // XXX Test performance of new features //
        internal static bool _SuppressDocs = false;
        internal static bool _SuppressOverloads = false;

        static CLRModule()
        {
            Reset();
        }

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
                if (!Runtime.PySys_GetObject("ps1").IsNull)
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
            var origNs = AssemblyManager.GetNamespaces();
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
            // Classes that are not in a namespace needs an extra nudge to be found.
            ImportHook.UpdateCLRModuleDict();

            // A bit heavyhanded, but we can't use the AssemblyManager's AssemblyLoadHandler
            // method because it may be called from other threads, leading to deadlocks
            // if it is called while Python code is executing.
            var currNs = AssemblyManager.GetNamespaces().Except(origNs);
            foreach(var ns in currNs){
                ImportHook.AddNamespace(ns);
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

        /// <summary>
        /// Note: This should *not* be called directly.
        /// The function that get/import a CLR assembly as a python module.
        /// This function should only be called by the import machinery as seen
        /// in importhook.cs
        /// </summary>
        /// <param name="spec">A ModuleSpec Python object</param>
        /// <returns>A new reference to the imported module, as a PyObject.</returns>
        [ModuleFunction]
        [ForbidPythonThreads]
        public static ModuleObject _load_clr_module(PyObject spec)
        {
            ModuleObject mod = null;
            using var modname = spec.GetAttr("name");
            mod = ImportHook.Import(modname.ToString());
            return mod;
        }
    }
}
