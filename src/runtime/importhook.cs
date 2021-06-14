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
        private static CLRModule root;
        private static IntPtr py_clr_module;
        static BorrowedReference ClrModuleReference => new BorrowedReference(py_clr_module);

        private const string LoaderCode = @"
import importlib.abc
import sys

class DotNetLoader(importlib.abc.Loader):

    @classmethod
    def exec_module(klass, mod):
        # This method needs to exist.
        pass

    @classmethod
    def create_module(klass, spec):
        import clr
        return clr._load_clr_module(spec)

class DotNetFinder(importlib.abc.MetaPathFinder):

    @classmethod
    def find_spec(klass, fullname, paths=None, target=None): 
        # Don't import, we might call ourselves recursively!
        if 'clr' not in sys.modules:
            return None
        clr = sys.modules['clr']
        if clr._available_namespaces and fullname in clr._available_namespaces:
            return importlib.machinery.ModuleSpec(fullname, DotNetLoader(), is_package=True)
        return None
            ";
        const string availableNsKey = "_available_namespaces";

        /// <summary>
        /// Initialization performed on startup of the Python runtime.
        /// </summary>
        internal static unsafe void Initialize()
        {
            // Initialize the clr module and tell Python about it.
            root = new CLRModule();

            // create a python module with the same methods as the clr module-like object
            py_clr_module = Runtime.PyModule_New("clr").DangerousMoveToPointer();

            // both dicts are borrowed references
            BorrowedReference mod_dict = Runtime.PyModule_GetDict(ClrModuleReference);
            using var clr_dict = Runtime.PyObject_GenericGetDict(root.ObjectReference);

            Runtime.PyDict_Update(mod_dict, clr_dict);
            BorrowedReference dict = Runtime.PyImport_GetModuleDict();
            Runtime.PyDict_SetItemString(dict, "CLR", ClrModuleReference);
            Runtime.PyDict_SetItemString(dict, "clr", ClrModuleReference);
            SetupNamespaceTracking();
            SetupImportHook();
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

            TeardownNameSpaceTracking();
            Runtime.XDecref(py_clr_module);
            py_clr_module = IntPtr.Zero;

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
            storage.GetValue("py_clr_module", out py_clr_module);
            var rootHandle = storage.GetValue<IntPtr>("root");
            root = (CLRModule)ManagedType.GetManagedObject(rootHandle);
            BorrowedReference dict = Runtime.PyImport_GetModuleDict();
            Runtime.PyDict_SetItemString(dict, "clr", ClrModuleReference);
            SetupNamespaceTracking();
        }

        static void SetupImportHook()
        {
            // Create the import hook module
            var import_hook_module = Runtime.PyModule_New("clr.loader");

            // Run the python code to create the module's classes.
            var builtins = Runtime.PyEval_GetBuiltins();
            var exec = Runtime.PyDict_GetItemString(builtins, "exec");
            using var args = NewReference.DangerousFromPointer(Runtime.PyTuple_New(2));

            var codeStr = NewReference.DangerousFromPointer(Runtime.PyString_FromString(LoaderCode));
            Runtime.PyTuple_SetItem(args, 0, codeStr);
            var mod_dict = Runtime.PyModule_GetDict(import_hook_module);
            // reference not stolen due to overload incref'ing for us.
            Runtime.PyTuple_SetItem(args, 1, mod_dict);
            Runtime.PyObject_Call(exec, args, default);
            // Set as a sub-module of clr.
            if(Runtime.PyModule_AddObject(ClrModuleReference, "loader", import_hook_module.DangerousGetAddress()) != 0)
            {
                Runtime.XDecref(import_hook_module.DangerousGetAddress());
                throw PythonException.ThrowLastAsClrException();
            }

            // Finally, add the hook to the meta path
            var findercls = Runtime.PyDict_GetItemString(mod_dict, "DotNetFinder");
            var finderCtorArgs = NewReference.DangerousFromPointer(Runtime.PyTuple_New(0));
            var finder_inst = Runtime.PyObject_CallObject(findercls, finderCtorArgs);
            var metapath = Runtime.PySys_GetObject("meta_path");
            Runtime.PyList_Append(metapath, finder_inst);
        }

        /// <summary>
        /// Sets up the tracking of loaded namespaces. This makes available to 
        /// Python, as a Python object, the loaded namespaces. The set of loaded
        /// namespaces is used during the import to verify if we can import a 
        /// CLR assembly as a module or not. The set is stored on the clr module.
        /// </summary>
        static void SetupNamespaceTracking()
        {
            using var newset = Runtime.PySet_New(default);
            foreach (var ns in AssemblyManager.GetNamespaces())
            {
                using var pyNs = NewReference.DangerousFromPointer(Runtime.PyString_FromString(ns));
                if (Runtime.PySet_Add(newset, pyNs) != 0)
                {
                    throw PythonException.ThrowLastAsClrException();
                }
                if (Runtime.PyDict_SetItemString(root.DictRef, availableNsKey, newset) != 0)
                {
                    throw PythonException.ThrowLastAsClrException();
                }
            }

        }

        /// <summary>
        /// Removes the set of available namespaces from the clr module.
        /// </summary>
        static void TeardownNameSpaceTracking()
        {
            // If the C# runtime isn't loaded, then there are no namespaces available
            Runtime.PyDict_SetItemString(root.dict, availableNsKey, Runtime.PyNone);
        }

        public static void AddNamespace(string name)
        {
            var pyNs = Runtime.PyString_FromString(name);
            try
            {
                var nsSet = Runtime.PyDict_GetItemString(new BorrowedReference(root.dict), availableNsKey);
                if (!(nsSet.IsNull  || nsSet.DangerousGetAddress() == Runtime.PyNone))
                {
                    if (Runtime.PySet_Add(nsSet, new BorrowedReference(pyNs)) != 0)
                    {
                        throw PythonException.ThrowLastAsClrException();
                    }
                }
            }
            finally
            {
                Runtime.XDecref(pyNs);
            }
        }


        /// <summary>
        /// Because we use a proxy module for the clr module, we somtimes need
        /// to force the py_clr_module to sync with the actual clr module's dict.
        /// </summary>
        internal static void UpdateCLRModuleDict()
        {
            root.InitializePreload();

            // update the module dictionary with the contents of the root dictionary
            root.LoadNames();
            BorrowedReference py_mod_dict = Runtime.PyModule_GetDict(ClrModuleReference);
            using var clr_dict = Runtime.PyObject_GenericGetDict(root.ObjectReference);

            Runtime.PyDict_Update(py_mod_dict, clr_dict);
        }

        /// <summary>
        /// Return the clr python module (new reference)
        /// </summary>
        public static unsafe NewReference GetCLRModule()
        {
            UpdateCLRModuleDict();
            Runtime.XIncref(py_clr_module);
            return NewReference.DangerousFromPointer(py_clr_module);
        }

        /// <summary>
        /// The hook to import a CLR module into Python. Returns a new reference
        /// to the module.
        /// </summary>
        public static ModuleObject Import(string modname)
        {
            // Traverse the qualified module name to get the named module. 
            // Note that if
            // we are running in interactive mode we pre-load the names in
            // each module, which is often useful for introspection. If we
            // are not interactive, we stick to just-in-time creation of
            // objects at lookup time, which is much more efficient.
            // NEW: The clr got a new module variable preload. You can
            // enable preloading in a non-interactive python processing by
            // setting clr.preload = True

            ModuleObject head = null;
            ModuleObject tail = root;
            root.InitializePreload();

            string[] names = modname.Split('.');
            foreach (string name in names)
            {
                ManagedType mt = tail.GetAttribute(name, true);
                if (!(mt is ModuleObject))
                {
                    Exceptions.SetError(Exceptions.ImportError, $"'{name}' Is not a ModuleObject.");
                    throw PythonException.ThrowLastAsClrException();
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
            }
            tail.IncrRefCount();
            return tail;
        }

        private static bool IsLoadAll(BorrowedReference fromList)
        {
            if (fromList == null) throw new ArgumentNullException(nameof(fromList));

            if (CLRModule.preload)
            {
                return false;
            }
            if (Runtime.PySequence_Size(fromList) != 1)
            {
                return false;
            }
            using var fp = Runtime.PySequence_GetItem(fromList, 0);
            return Runtime.GetManagedString(fp) == "*";
        }
    }
}
