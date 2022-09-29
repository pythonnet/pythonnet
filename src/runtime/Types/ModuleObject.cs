using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// Implements a Python type that provides access to CLR namespaces. The
    /// type behaves like a Python module, and can contain other sub-modules.
    /// </summary>
    [Serializable]
    internal class ModuleObject : ExtensionType
    {
        private readonly Dictionary<string, PyObject> cache = new();

        internal string moduleName;
        internal PyDict dict;
        protected string _namespace;
        private readonly PyList __all__ = new ();

        // Attributes to be set on the module according to PEP302 and 451
        // by the import machinery.
        static readonly HashSet<string?> settableAttributes =
            new () {"__spec__", "__file__", "__name__", "__path__", "__loader__", "__package__"};

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        /// <remarks><seealso cref="dict"/> is initialized in <seealso cref="Create(string)"/></remarks>
        protected ModuleObject(string name)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            if (name == string.Empty)
            {
                throw new ArgumentException("Name must not be empty!");
            }
            moduleName = name;
            _namespace = name;
        }

        internal static NewReference Create(string name) => new ModuleObject(name).Alloc();

        public override NewReference Alloc()
        {
            var py = base.Alloc();

            if (dict is null)
            {
                // Use the filename from any of the assemblies just so there's something for
                // anything that expects __file__ to be set.
                var filename = "unknown";
                var docstring = "Namespace containing types from the following assemblies:\n\n";
                foreach (Assembly a in AssemblyManager.GetAssemblies(moduleName))
                {
                    if (!a.IsDynamic && a.Location != null)
                    {
                        filename = a.Location;
                    }
                    docstring += "- " + a.FullName + "\n";
                }

                using var dictRef = Runtime.PyObject_GenericGetDict(py.Borrow());
                dict = new PyDict(dictRef.StealOrThrow());
                using var pyname = Runtime.PyString_FromString(moduleName);
                using var pyfilename = Runtime.PyString_FromString(filename);
                using var pydocstring = Runtime.PyString_FromString(docstring);
                BorrowedReference pycls = TypeManager.GetTypeReference(GetType());
                Runtime.PyDict_SetItem(dict, PyIdentifier.__name__, pyname.Borrow());
                Runtime.PyDict_SetItem(dict, PyIdentifier.__file__, pyfilename.Borrow());
                Runtime.PyDict_SetItem(dict, PyIdentifier.__doc__, pydocstring.Borrow());
                Runtime.PyDict_SetItem(dict, PyIdentifier.__class__, pycls);
            }
            else
            {
                SetObjectDict(py.Borrow(), new NewReference(dict).Steal());
            }

            InitializeModuleMembers();

            return py;
        }


        /// <summary>
        /// Returns a ClassBase object representing a type that appears in
        /// this module's namespace or a ModuleObject representing a child
        /// namespace (or null if the name is not found). This method does
        /// not increment the Python refcount of the returned object.
        /// </summary>
        public NewReference GetAttribute(string name, bool guess)
        {
            cache.TryGetValue(name, out var cached);
            if (cached != null)
            {
                return new NewReference(cached);
            }

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
                var m = ModuleObject.Create(qname);
                this.StoreAttribute(name, m.Borrow());
                return m;
            }

            // Look for a type in the current namespace. Note that this
            // includes types, delegates, enums, interfaces and structs.
            // Only public namespace members are exposed to Python.
            type = AssemblyManager.LookupTypes(qname).FirstOrDefault(t => t.IsPublic);
            if (type != null)
            {
                var c = ClassManager.GetClass(type);
                StoreAttribute(name, c);
                return new NewReference(c);
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
                string? gname = GenericUtil.GenericNameForBaseName(this._namespace, name);
                if (gname != null)
                {
                    var o = this.GetAttribute(gname, false);
                    if (!o.IsNull())
                    {
                        this.StoreAttribute(name, o.Borrow());
                        return o;
                    }
                }
            }

            return default;
        }

        /// <summary>
        /// Stores an attribute in the instance dict for future lookups.
        /// </summary>
        private void StoreAttribute(string name, BorrowedReference ob)
        {
            if (Runtime.PyDict_SetItemString(dict, name, ob) != 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            cache[name] = new PyObject(ob);
        }


        /// <summary>
        /// Preloads all currently-known names for the module namespace. This
        /// can be called multiple times, to add names from assemblies that
        /// may have been loaded since the last call to the method.
        /// </summary>
        public void LoadNames()
        {
            foreach (string name in AssemblyManager.GetNames(_namespace))
            {
                cache.TryGetValue(name, out var m);
                if (m != null)
                {
                    continue;
                }
                BorrowedReference attr = Runtime.PyDict_GetItemString(dict, name);
                // If __dict__ has already set a custom property, skip it.
                if (!attr.IsNull)
                {
                    continue;
                }

                using var attrVal = GetAttribute(name, true);
                if (!attrVal.IsNull())
                {
                    // if it's a valid attribute, add it to __all__
                    using var pyname = Runtime.PyString_FromString(name);
                    if (Runtime.PyList_Append(__all__, pyname.Borrow()) != 0)
                    {
                        throw PythonException.ThrowLastAsClrException();
                    }
                }
            }
        }

        const BindingFlags ModuleMethodFlags = BindingFlags.Public | BindingFlags.Static;
        /// <summary>
        /// Initialize module level functions and attributes
        /// </summary>
        internal void InitializeModuleMembers()
        {
            Type funcmarker = typeof(ModuleFunctionAttribute);
            Type propmarker = typeof(ModulePropertyAttribute);
            Type ftmarker = typeof(ForbidPythonThreadsAttribute);
            Type type = GetType();

            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(ModuleMethodFlags);
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
                        using var m = new ModuleFunctionObject(type, name, mi, allow_threads).Alloc();
                        StoreAttribute(name, m.Borrow());
                    }
                }

                PropertyInfo[] properties = type.GetProperties();
                foreach (PropertyInfo property in properties)
                {
                    object[] attrs = property.GetCustomAttributes(propmarker, false);
                    if (attrs.Length > 0)
                    {
                        string name = property.Name;
                        using var p = new ModulePropertyObject(property).Alloc();
                        StoreAttribute(name, p.Borrow());
                    }
                }
                type = type.BaseType;
            }
        }

        internal void ResetModuleMembers()
        {
            Type type = GetType();
            var methods = type.GetMethods(ModuleMethodFlags)
                .Where(m => m.GetCustomAttribute<ModuleFunctionAttribute>() is not null)
                .OfType<MemberInfo>();
            var properties = type.GetProperties().Where(p => p.GetCustomAttribute<ModulePropertyAttribute>() is not null);

            foreach (string memberName in methods.Concat(properties).Select(m => m.Name))
            {
                if (Runtime.PyDict_DelItemString(dict, memberName) != 0)
                {
                    if (!PythonException.CurrentMatches(Exceptions.KeyError))
                    {
                        throw PythonException.ThrowLastAsClrException();
                    }
                    Runtime.PyErr_Clear();
                }
                cache.Remove(memberName);
            }
        }


        /// <summary>
        /// ModuleObject __getattribute__ implementation. Module attributes
        /// are always either classes or sub-modules representing subordinate
        /// namespaces. CLR modules implement a lazy pattern - the sub-modules
        /// and classes are created when accessed and cached for future use.
        /// </summary>
        public static NewReference tp_getattro(BorrowedReference ob, BorrowedReference key)
        {
            var self = (ModuleObject)GetManagedObject(ob)!;

            if (!Runtime.PyString_Check(key))
            {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return default;
            }

            Debug.Assert(!self.dict.IsDisposed);

            BorrowedReference op = Runtime.PyDict_GetItem(self.dict, key);
            if (op != null)
            {
                return new NewReference(op);
            }

            string? name = InternString.GetManagedString(key);
            if (name == "__dict__")
            {
                return new NewReference(self.dict);
            }

            if (name == "__all__")
            {
                self.LoadNames();
                return new NewReference(self.__all__);
            }

            NewReference attr;

            try
            {
                if (name is null) throw new ArgumentNullException();
                attr = self.GetAttribute(name, true);
            }
            catch (Exception e)
            {
                Exceptions.SetError(e);
                return default;
            }


            if (attr.IsNull())
            {
                Exceptions.SetError(Exceptions.AttributeError, name);
                return default;
            }

            return attr;
        }

        /// <summary>
        /// ModuleObject __repr__ implementation.
        /// </summary>
        public static NewReference tp_repr(BorrowedReference ob)
        {
            var self = (ModuleObject)GetManagedObject(ob)!;
            return Runtime.PyString_FromString($"<module '{self.moduleName}'>");
        }

        public static int tp_traverse(BorrowedReference ob, IntPtr visit, IntPtr arg)
        {
            var self = (ModuleObject?)GetManagedObject(ob);
            if (self is null) return 0;

            Debug.Assert(self.dict == GetObjectDict(ob));
            int res = PyVisit(self.dict, visit, arg);
            if (res != 0) return res;
            foreach (var attr in self.cache.Values)
            {
                res = PyVisit(attr, visit, arg);
                if (res != 0) return res;
            }
            return 0;
        }

        /// <summary>
        /// Override the setattr implementation.
        /// This is needed because the import mechanics need
        /// to set a few attributes
        /// </summary>
        [ForbidPythonThreads]
        public new static int tp_setattro(BorrowedReference ob, BorrowedReference key, BorrowedReference val)
        {
            var managedKey = Runtime.GetManagedString(key);
            if ((settableAttributes.Contains(managedKey)) ||
                (ManagedType.GetManagedObject(val) is ModuleObject) )
            {
                var self = (ModuleObject)ManagedType.GetManagedObject(ob)!;
                return Runtime.PyDict_SetItem(self.dict, key, val);
            }

            return ExtensionType.tp_setattro(ob, key, val);
        }

        protected override Dictionary<string, object?>? OnSave(BorrowedReference ob)
        {
            var context = base.OnSave(ob);
            System.Diagnostics.Debug.Assert(dict == GetObjectDict(ob));
            // destroy the cache(s)
            foreach (var pair in cache)
            {
                if ((Runtime.PyDict_DelItemString(dict, pair.Key) == -1) &&
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

            cache.Clear();
            return context;
        }

        protected override void OnLoad(BorrowedReference ob, Dictionary<string, object?>? context)
        {
            base.OnLoad(ob, context);
            SetObjectDict(ob, new NewReference(dict).Steal());
        }
    }

}
