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
        static BorrowedReference ClrModuleReference => new BorrowedReference(py_clr_module);

        private const string LoaderCode = @"
import importlib.abc
import sys

class DotNetLoader(importlib.abc.Loader):

    def __init__(self):
        super(DotNetLoader, self).__init__()

    @classmethod
    def exec_module(klass, mod):
        # this method is needed to mark this
        # loader as a non-legacy loader.
        pass

    @classmethod
    def create_module(klass, spec):
        import clr
        a = clr._LoadClrModule(spec)
        #mod = getattr(clr, '__imported')
        print(a)
        #print(mod)
        #print(mod is a)
        #delattr(clr, '__imported')
        return a

class DotNetFinder(importlib.abc.MetaPathFinder):
    
    def __init__(self):
        print('DotNetFinder init')
        super(DotNetFinder, self).__init__()
    
    @classmethod
    def find_spec(klass, fullname, paths=None, target=None): 
        import clr
        # print(clr._availableNamespaces)
        if (hasattr(clr, '_availableNamespaces') and fullname in clr._availableNamespaces):
        #if (clr._NamespaceLoaded(fullname)):
            return importlib.machinery.ModuleSpec(fullname, DotNetLoader(), is_package=True)
        return None

sys.meta_path.append(DotNetFinder())
            ";

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

            // Add/create the MetaPathLoader
            PythonEngine.Exec(LoaderCode);
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

            bool shouldFreeDef = Runtime.Refcount(py_clr_module) == 1;
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
        }

        /// <summary>
        /// Return the clr python module (new reference)
        /// </summary>
        public static unsafe NewReference GetCLRModule(BorrowedReference fromList = default)
        {
            root.InitializePreload();

            // update the module dictionary with the contents of the root dictionary
            root.LoadNames();
            BorrowedReference py_mod_dict = Runtime.PyModule_GetDict(ClrModuleReference);
            using (var clr_dict = Runtime.PyObject_GenericGetDict(root.ObjectReference))
            {
                Runtime.PyDict_Update(py_mod_dict, clr_dict);
            }

            Runtime.XIncref(py_clr_module);
            return NewReference.DangerousFromPointer(py_clr_module);
        }

        /// <summary>
        /// The actual import hook that ties Python to the managed world.
        /// </summary>
        public static ModuleObject __import__(string modname)
        {
            // Console.WriteLine("Import hook");

            string realname = modname;
            string[] names = realname.Split('.');

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
            // root.LoadNames();

            foreach (string name in names)
            {
                ManagedType mt = tail.GetAttribute(name, true);
                if (!(mt is ModuleObject))
                {
                    // originalException.Restore();
                    // Exceptions.SetError(Exceptions.ImportError, "");
                    // throw new PythonException();
                    // TODO: set exception
                    return null;
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

            {
                Runtime.XIncref(tail.pyHandle);
                return tail;
            }
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
