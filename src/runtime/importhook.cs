using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    //========================================================================
    // Implements the "import hook" used to integrate Python with the CLR.
    //========================================================================

    internal class ImportHook
    {
        static IntPtr py_import;
        static CLRModule root;
        static MethodWrapper hook;

#if PYTHON3
        static IntPtr py_clr_module;
        static IntPtr module_def;
#endif

        //===================================================================
        // Initialization performed on startup of the Python runtime.
        //===================================================================

        internal static void Initialize()
        {
            // Initialize the Python <--> CLR module hook. We replace the
            // built-in Python __import__ with our own. This isn't ideal,
            // but it provides the most "Pythonic" way of dealing with CLR
            // modules (Python doesn't provide a way to emulate packages).
            IntPtr dict = Runtime.PyImport_GetModuleDict();
#if PYTHON3
            IntPtr mod = Runtime.PyImport_ImportModule("builtins");
            py_import = Runtime.PyObject_GetAttrString(mod, "__import__");
#elif PYTHON2
            IntPtr mod = Runtime.PyDict_GetItemString(dict, "__builtin__");
            py_import = Runtime.PyObject_GetAttrString(mod, "__import__");
#endif
            hook = new MethodWrapper(typeof(ImportHook), "__import__", "TernaryFunc");
            Runtime.PyObject_SetAttrString(mod, "__import__", hook.ptr);
            Runtime.XDecref(hook.ptr);

            root = new CLRModule();

#if PYTHON3
    // create a python module with the same methods as the clr module-like object
            module_def = ModuleDefOffset.AllocModuleDef("clr");
            py_clr_module = Runtime.PyModule_Create2(module_def, 3);

            // both dicts are borrowed references
            IntPtr mod_dict = Runtime.PyModule_GetDict(py_clr_module);
            IntPtr clr_dict = Runtime._PyObject_GetDictPtr(root.pyHandle); // PyObject**
            clr_dict = (IntPtr)Marshal.PtrToStructure(clr_dict, typeof(IntPtr));

            Runtime.PyDict_Update(mod_dict, clr_dict);
            Runtime.PyDict_SetItemString(dict, "CLR", py_clr_module);
            Runtime.PyDict_SetItemString(dict, "clr", py_clr_module);
#elif PYTHON2
            Runtime.XIncref(root.pyHandle); // we are using the module two times
            Runtime.PyDict_SetItemString(dict, "CLR", root.pyHandle);
            Runtime.PyDict_SetItemString(dict, "clr", root.pyHandle);
#endif
        }


        //===================================================================
        // Cleanup resources upon shutdown of the Python runtime.
        //===================================================================

        internal static void Shutdown()
        {
#if PYTHON3
            if (0 != Runtime.Py_IsInitialized()) {
                Runtime.XDecref(py_clr_module);
                Runtime.XDecref(root.pyHandle);
            }
            ModuleDefOffset.FreeModuleDef(module_def);
#elif PYTHON2
            if (0 != Runtime.Py_IsInitialized())
            {
                Runtime.XDecref(root.pyHandle);
                Runtime.XDecref(root.pyHandle);
            }
#endif
            if (0 != Runtime.Py_IsInitialized())
            {
                Runtime.XDecref(py_import);
            }
        }

        //===================================================================
        // Return the clr python module (new reference)
        //===================================================================
        public static IntPtr GetCLRModule(IntPtr? fromList = null)
        {
            root.InitializePreload();
#if PYTHON3
    // update the module dictionary with the contents of the root dictionary
            root.LoadNames();
            IntPtr py_mod_dict = Runtime.PyModule_GetDict(py_clr_module);
            IntPtr clr_dict = Runtime._PyObject_GetDictPtr(root.pyHandle); // PyObject**
            clr_dict = (IntPtr)Marshal.PtrToStructure(clr_dict, typeof(IntPtr));
            Runtime.PyDict_Update(py_mod_dict, clr_dict);

            // find any items from the fromlist and get them from the root if they're not
            // aleady in the module dictionary
            if (fromList != null && fromList != IntPtr.Zero) {
                if (Runtime.PyTuple_Check(fromList.GetValueOrDefault()))
                {
                    Runtime.XIncref(py_mod_dict);
                    using(PyDict mod_dict = new PyDict(py_mod_dict)) {
                        Runtime.XIncref(fromList.GetValueOrDefault());
                        using (PyTuple from = new PyTuple(fromList.GetValueOrDefault())) {
                            foreach (PyObject item in from) {
                                if (mod_dict.HasKey(item))
                                    continue;

                                string s = item.AsManagedObject(typeof(string)) as string;
                                if (null == s)
                                    continue;

                                ManagedType attr = root.GetAttribute(s, true);
                                if (null == attr)
                                    continue;

                                Runtime.XIncref(attr.pyHandle);
                                using (PyObject obj = new PyObject(attr.pyHandle)) {
                                    mod_dict.SetItem(s, obj);
                                }
                            }
                        }
                    }
                }
            }

            Runtime.XIncref(py_clr_module);
            return py_clr_module;
#elif PYTHON2
            Runtime.XIncref(root.pyHandle);
            return root.pyHandle;
#endif
        }

        //===================================================================
        // The actual import hook that ties Python to the managed world.
        //===================================================================

        public static IntPtr __import__(IntPtr self, IntPtr args, IntPtr kw)
        {
            // Replacement for the builtin __import__. The original import
            // hook is saved as this.py_import. This version handles CLR
            // import and defers to the normal builtin for everything else.

            int num_args = Runtime.PyTuple_Size(args);
            if (num_args < 1)
            {
                return Exceptions.RaiseTypeError(
                    "__import__() takes at least 1 argument (0 given)"
                    );
            }

            // borrowed reference
            IntPtr py_mod_name = Runtime.PyTuple_GetItem(args, 0);
            if ((py_mod_name == IntPtr.Zero) ||
                (!Runtime.IsStringType(py_mod_name)))
            {
                return Exceptions.RaiseTypeError("string expected");
            }

            // Check whether the import is of the form 'from x import y'.
            // This determines whether we return the head or tail module.

            IntPtr fromList = IntPtr.Zero;
            bool fromlist = false;
            if (num_args >= 4)
            {
                fromList = Runtime.PyTuple_GetItem(args, 3);
                if ((fromList != IntPtr.Zero) &&
                    (Runtime.PyObject_IsTrue(fromList) == 1))
                {
                    fromlist = true;
                }
            }

            string mod_name = Runtime.GetManagedString(py_mod_name);
            // Check these BEFORE the built-in import runs; may as well
            // do the Incref()ed return here, since we've already found
            // the module.
            if (mod_name == "clr")
            {
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
            if (mod_name == "CLR")
            {
                Exceptions.deprecation("The CLR module is deprecated. " +
                                       "Please use 'clr'.");
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
                string msg = String.Format("Importing from the CLR.* namespace " +
                                           "is deprecated. Please import '{0}' directly.", realname);
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

            // Now we need to decide if the name refers to a CLR module,
            // and may have to do an implicit load (for b/w compatibility)
            // using the AssemblyManager. The assembly manager tries
            // really hard not to use Python objects or APIs, because
            // parts of it can run recursively and on strange threads.
            //
            // It does need an opportunity from time to time to check to
            // see if sys.path has changed, in a context that is safe. Here
            // we know we have the GIL, so we'll let it update if needed.

            AssemblyManager.UpdatePath();
            if (!AssemblyManager.IsValidNamespace(realname))
            {
                if (!AssemblyManager.LoadImplicit(realname))
                {
                    // May be called when a module being imported imports a module.
                    // In particular, I've seen decimal import copy import org.python.core
                    return Runtime.PyObject_Call(py_import, args, kw);
                }
            }

            // See if sys.modules for this interpreter already has the
            // requested module. If so, just return the exising module.
            IntPtr modules = Runtime.PyImport_GetModuleDict();
            IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);

            if (module != IntPtr.Zero)
            {
                if (fromlist)
                {
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

            ModuleObject head = (mod_name == realname) ? null : root;
            ModuleObject tail = root;
            root.InitializePreload();

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                ManagedType mt = tail.GetAttribute(name, true);
                if (!(mt is ModuleObject))
                {
                    string error = String.Format("No module named {0}", name);
                    Exceptions.SetError(Exceptions.ImportError, error);
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
                Runtime.PyDict_SetItemString(modules,
                    tail.moduleName,
                    tail.pyHandle);

                // If imported from CLR add CLR.<modulename> to sys.modules as well
                if (clr_prefix != null)
                {
                    Runtime.PyDict_SetItemString(modules,
                        clr_prefix + tail.moduleName,
                        tail.pyHandle);
                }
            }

            ModuleObject mod = fromlist ? tail : head;

            if (fromlist && Runtime.PySequence_Size(fromList) == 1)
            {
                IntPtr fp = Runtime.PySequence_GetItem(fromList, 0);
                if ((!CLRModule.preload) && Runtime.GetManagedString(fp) == "*")
                {
                    mod.LoadNames();
                }
                Runtime.XDecref(fp);
            }

            Runtime.XIncref(mod.pyHandle);
            return mod.pyHandle;
        }
    }
}
