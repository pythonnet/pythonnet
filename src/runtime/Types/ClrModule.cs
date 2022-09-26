using System;
using System.Linq;
using System.IO;
using System.Reflection;

namespace Python.Runtime
{
    /// <summary>
    /// The CLR module is the root handler used by the magic import hook
    /// to import assemblies. It has a fixed module name "clr" and doesn't
    /// provide a namespace.
    /// </summary>
    [Serializable]
    internal class CLRModule : ModuleObject
    {
        protected static bool interactive_preload = true;
        internal static bool preload;
        // XXX Test performance of new features //
        internal static bool _SuppressDocs = false;
        internal static bool _SuppressOverloads = false;

        static CLRModule()
        {
            Reset();
        }

        private CLRModule() : base("clr")
        {
            _namespace = string.Empty;
        }

        internal static NewReference Create(out CLRModule module)
        {
            module = new CLRModule();
            return module.Alloc();
        }

        public static void Reset()
        {
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
            Assembly? assembly = AssemblyManager.FindLoadedAssembly(name);
            if (assembly == null)
            {
                assembly = AssemblyManager.LoadAssemblyPath(name);
            }
            if (assembly == null && AssemblyManager.TryParseAssemblyName(name) is { } parsedName)
            {
                assembly = AssemblyManager.LoadAssembly(parsedName);
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
            foreach(var ns in currNs)
            {
                ImportHook.AddNamespaceWithGIL(ns);
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
        public static PyObject _load_clr_module(PyObject spec)
        {
            using var modname = spec.GetAttr("name");
            string name = modname.As<string?>() ?? throw new ArgumentException("name must not be None");
            var mod = ImportHook.Import(name);
            return mod;
        }

        [ModuleFunction]
        [ForbidPythonThreads]
        public static int _add_pending_namespaces() => ImportHook.AddPendingNamespaces();
    }
}
