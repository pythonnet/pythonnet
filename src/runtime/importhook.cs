using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// Implements the "import hook" used to integrate Python with the CLR.
    /// </summary>
    internal static class ImportHook
    {
        private static IntPtr py_import;
        private static CLRModule root;
        private static MethodWrapper hook;
        private static IntPtr py_clr_module;

        private static IntPtr module_def = IntPtr.Zero;

        internal static void InitializeModuleDef()
        {
            if (module_def == IntPtr.Zero)
            {
                module_def = ModuleDefOffset.AllocModuleDef("clr");
            }
        }

        internal static void ReleaseModuleDef()
        {
            if (module_def == IntPtr.Zero)
            {
                return;
            }
            ModuleDefOffset.FreeModuleDef(module_def);
            module_def = IntPtr.Zero;
        }

        /// <summary>
        /// Initialize just the __import__ hook itself.
        /// </summary>
        static void InitImport()
        {
            // We replace the built-in Python __import__ with our own: first
            // look in CLR modules, then if we don't find any call the default
            // Python __import__.
            IntPtr builtins = Runtime.GetBuiltins();
            py_import = Runtime.PyObject_GetAttr(builtins, PyIdentifier.__import__);
            PythonException.ThrowIfIsNull(py_import);

            hook = new MethodWrapper(typeof(ImportHook), "__import__", "TernaryFunc");
            int res = Runtime.PyObject_SetAttr(builtins, PyIdentifier.__import__, hook.ptr);
            PythonException.ThrowIfIsNotZero(res);

            Runtime.XDecref(builtins);
        }

        /// <summary>
        /// Restore the __import__ hook.
        /// </summary>
        static void RestoreImport()
        {
            IntPtr builtins = Runtime.GetBuiltins();

            int res = Runtime.PyObject_SetAttr(builtins, PyIdentifier.__import__, py_import);
            PythonException.ThrowIfIsNotZero(res);
            Runtime.XDecref(py_import);
            py_import = IntPtr.Zero;

            hook.Release();
            hook = null;

            Runtime.XDecref(builtins);
        }

        /// <summary>
        /// Initialization performed on startup of the Python runtime.
        /// </summary>
        internal static void Initialize()
        {
            InitImport();

            // Initialize the clr module and tell Python about it.
            root = new CLRModule();

            // create a python module with the same methods as the clr module-like object
            InitializeModuleDef();
            py_clr_module = Runtime.PyModule_Create2(module_def, 3);

            // both dicts are borrowed references
            IntPtr mod_dict = Runtime.PyModule_GetDict(py_clr_module);
            IntPtr clr_dict = Runtime._PyObject_GetDictPtr(root.pyHandle); // PyObject**
            clr_dict = (IntPtr)Marshal.PtrToStructure(clr_dict, typeof(IntPtr));

            Runtime.PyDict_Update(mod_dict, clr_dict);
            IntPtr dict = Runtime.PyImport_GetModuleDict();
            Runtime.PyDict_SetItemString(dict, "CLR", py_clr_module);
            Runtime.PyDict_SetItemString(dict, "clr", py_clr_module);
        }


        /// <summary>
        /// Cleanup resources upon shutdown of the Python runtime.
        /// </summary>
        internal static void Shutdown()
        {
            if (Runtime.Py_IsInitialized() == 0)
            {
                return;
            }

            RestoreImport();

            bool shouldFreeDef = Runtime.Refcount(py_clr_module) == 1;
            Runtime.XDecref(py_clr_module);
            py_clr_module = IntPtr.Zero;
            if (shouldFreeDef)
            {
                ReleaseModuleDef();
            }

            Runtime.XDecref(root.pyHandle);
            root = null;
            CLRModule.Reset();
        }

        internal static void SaveRuntimeData(RuntimeDataStorage storage)
        {
            // Increment the reference counts here so that the objects don't 
            // get freed in Shutdown.
            Runtime.XIncref(py_clr_module);
            Runtime.XIncref(root.pyHandle);
            storage.AddValue("py_clr_module", py_clr_module);
            storage.AddValue("root", root.pyHandle);
        }

        internal static void RestoreRuntimeData(RuntimeDataStorage storage)
        {
            InitImport();
            storage.GetValue("py_clr_module", out py_clr_module);
            var rootHandle = storage.GetValue<IntPtr>("root");
            root = (CLRModule)ManagedType.GetManagedObject(rootHandle);
        }

        /// <summary>
        /// Return the clr python module (new reference)
        /// </summary>
        public static IntPtr GetCLRModule(IntPtr? fromList = null)
        {
            root.InitializePreload();

            // update the module dictionary with the contents of the root dictionary
            root.LoadNames();
            IntPtr py_mod_dict = Runtime.PyModule_GetDict(py_clr_module);
            IntPtr clr_dict = Runtime._PyObject_GetDictPtr(root.pyHandle); // PyObject**
            clr_dict = (IntPtr)Marshal.PtrToStructure(clr_dict, typeof(IntPtr));
            Runtime.PyDict_Update(py_mod_dict, clr_dict);

            // find any items from the from list and get them from the root if they're not
            // already in the module dictionary
            if (fromList != null && fromList != IntPtr.Zero)
            {
                if (Runtime.PyTuple_Check(fromList.GetValueOrDefault()))
                {
                    Runtime.XIncref(py_mod_dict);
                    using (var mod_dict = new PyDict(py_mod_dict))
                    {
                        Runtime.XIncref(fromList.GetValueOrDefault());
                        using (var from = new PyTuple(fromList.GetValueOrDefault()))
                        {
                            foreach (PyObject item in from)
                            {
                                if (mod_dict.HasKey(item))
                                {
                                    continue;
                                }

                                var s = item.AsManagedObject(typeof(string)) as string;
                                if (s == null)
                                {
                                    continue;
                                }

                                ManagedType attr = root.GetAttribute(s, true);
                                if (attr == null)
                                {
                                    continue;
                                }

                                Runtime.XIncref(attr.pyHandle);
                                using (var obj = new PyObject(attr.pyHandle))
                                {
                                    mod_dict.SetItem(s, obj);
                                }
                            }
                        }
                    }
                }
            }
            Runtime.XIncref(py_clr_module);
            return py_clr_module;
        }

        /// <summary>
        /// The actual import hook that ties Python to the managed world.
        /// </summary>
        public static IntPtr __import__(IntPtr self, IntPtr args, IntPtr kw)
        {
            // Replacement for the builtin __import__. The original import
            // hook is saved as this.py_import. This version handles CLR
            // import and defers to the normal builtin for everything else.

            var num_args = Runtime.PyTuple_Size(args);
            if (num_args < 1)
            {
                return Exceptions.RaiseTypeError("__import__() takes at least 1 argument (0 given)");
            }

            // borrowed reference
            IntPtr py_mod_name = Runtime.PyTuple_GetItem(args, 0);
            if (py_mod_name == IntPtr.Zero ||
                !Runtime.IsStringType(py_mod_name))
            {
                return Exceptions.RaiseTypeError("string expected");
            }

            // Check whether the import is of the form 'from x import y'.
            // This determines whether we return the head or tail module.

            IntPtr fromList = IntPtr.Zero;
            var fromlist = false;
            if (num_args >= 4)
            {
                fromList = Runtime.PyTuple_GetItem(args, 3);
                if (fromList != IntPtr.Zero &&
                    Runtime.PyObject_IsTrue(fromList) == 1)
                {
                    fromlist = true;
                }
            }

            string mod_name = Runtime.GetManagedString(py_mod_name);
            // Check these BEFORE the built-in import runs; may as well
            // do the Incref()ed return here, since we've already found
            // the module.
            if (mod_name == "clr" || mod_name == "CLR")
            {
                if (mod_name == "CLR")
                {
                    Exceptions.deprecation("The CLR module is deprecated. Please use 'clr'.");
                }
                IntPtr clr_module = GetCLRModule(fromList);
                if (clr_module != IntPtr.Zero)
                {
                    IntPtr sys_modules = Runtime.PyImport_GetModuleDict();
                    if (sys_modules != IntPtr.Zero)
                    {
                        Runtime.PyDict_SetItemString(sys_modules, "clr", clr_module);
                    }
                }
                return clr_module;
            }

            string realname = mod_name;
            string clr_prefix = null;

            if (mod_name.StartsWith("CLR."))
            {
                clr_prefix = "CLR."; // prepend when adding the module to sys.modules
                realname = mod_name.Substring(4);
                string msg = $"Importing from the CLR.* namespace is deprecated. Please import '{realname}' directly.";
                Exceptions.deprecation(msg);
            }
            else
            {
                // 2010-08-15: Always seemed smart to let python try first...
                // This shaves off a few tenths of a second on test_module.py
                // and works around a quirk where 'sys' is found by the
                // LoadImplicit() deprecation logic.
                // Turns out that the AssemblyManager.ResolveHandler() checks to see if any
                // Assembly's FullName.ToLower().StartsWith(name.ToLower()), which makes very
                // little sense to me.
                IntPtr res = Runtime.PyObject_Call(py_import, args, kw);
                if (res != IntPtr.Zero)
                {
                    // There was no error.
                    if (fromlist && IsLoadAll(fromList))
                    {
                        var mod = ManagedType.GetManagedObject(res) as ModuleObject;
                        mod?.LoadNames();
                    }
                    return res;
                }
                // There was an error
                if (!Exceptions.ExceptionMatches(Exceptions.ImportError))
                {
                    // and it was NOT an ImportError; bail out here.
                    return IntPtr.Zero;
                }

                if (mod_name == string.Empty)
                {
                    // Most likely a missing relative import.
                    // For example site-packages\bs4\builder\__init__.py uses it to check if a package exists:
                    //     from . import _html5lib
                    // We don't support them anyway
                    return IntPtr.Zero;
                }
                // Otherwise,  just clear the it.
                Exceptions.Clear();
            }

            string[] names = realname.Split('.');

            // See if sys.modules for this interpreter already has the
            // requested module. If so, just return the existing module.
            IntPtr modules = Runtime.PyImport_GetModuleDict();
            IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);

            if (module != IntPtr.Zero)
            {
                if (fromlist)
                {
                    if (IsLoadAll(fromList))
                    {
                        var mod = ManagedType.GetManagedObject(module) as ModuleObject;
                        mod?.LoadNames();
                    }
                    Runtime.XIncref(module);
                    return module;
                }
                if (clr_prefix != null)
                {
                    return GetCLRModule(fromList);
                }
                module = Runtime.PyDict_GetItemString(modules, names[0]);
                Runtime.XIncref(module);
                return module;
            }
            Exceptions.Clear();

            // Traverse the qualified module name to get the named module
            // and place references in sys.modules as we go. Note that if
            // we are running in interactive mode we pre-load the names in
            // each module, which is often useful for introspection. If we
            // are not interactive, we stick to just-in-time creation of
            // objects at lookup time, which is much more efficient.
            // NEW: The clr got a new module variable preload. You can
            // enable preloading in a non-interactive python processing by
            // setting clr.preload = True

            ModuleObject head = mod_name == realname ? null : root;
            ModuleObject tail = root;
            root.InitializePreload();

            foreach (string name in names)
            {
                ManagedType mt = tail.GetAttribute(name, true);
                if (!(mt is ModuleObject))
                {
                    Exceptions.SetError(Exceptions.ImportError, $"No module named {name}");
                    return IntPtr.Zero;
                }
                if (head == null)
                {
                    head = (ModuleObject)mt;
                }
                tail = (ModuleObject)mt;
                if (CLRModule.preload)
                {
                    tail.LoadNames();
                }

                // Add the module to sys.modules
                Runtime.PyDict_SetItemString(modules, tail.moduleName, tail.pyHandle);

                // If imported from CLR add CLR.<modulename> to sys.modules as well
                if (clr_prefix != null)
                {
                    Runtime.PyDict_SetItemString(modules, clr_prefix + tail.moduleName, tail.pyHandle);
                }
            }

            {
                var mod = fromlist ? tail : head;

                if (fromlist && IsLoadAll(fromList))
                {
                    mod.LoadNames();
                }

                Runtime.XIncref(mod.pyHandle);
                return mod.pyHandle;
            }
        }

        private static bool IsLoadAll(IntPtr fromList)
        {
            if (CLRModule.preload)
            {
                return false;
            }
            if (Runtime.PySequence_Size(fromList) != 1)
            {
                return false;
            }
            IntPtr fp = Runtime.PySequence_GetItem(fromList, 0);
            bool res = Runtime.GetManagedString(fp) == "*";
            Runtime.XDecref(fp);
            return res;
        }
    }
}
