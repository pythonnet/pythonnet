using System;
using System.Collections;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Python.Runtime
{
    /// <summary>
    /// The AssemblyManager maintains information about loaded assemblies
    /// namespaces and provides an interface for name-based type lookup.
    /// </summary>
    internal class AssemblyManager
    {
        // modified from event handlers below, potentially triggered from different .NET threads
        // therefore this should be a ConcurrentDictionary
        static ConcurrentDictionary<string, ConcurrentDictionary<Assembly, string>> namespaces;
        //static Dictionary<string, Dictionary<string, string>> generics;
        static AssemblyLoadEventHandler lhandler;
        static ResolveEventHandler rhandler;
        // updated only under GIL?
        static Dictionary<string, int> probed;
        // modified from event handlers below, potentially triggered from different .NET threads
        static AssemblyList assemblies;
        internal static List<string> pypath;

        private AssemblyManager()
        {
        }

        //===================================================================
        // Initialization performed on startup of the Python runtime. Here we
        // scan all of the currently loaded assemblies to determine exported
        // names, and register to be notified of new assembly loads.
        //===================================================================

        internal static void Initialize()
        {
            namespaces = new
                ConcurrentDictionary<string, ConcurrentDictionary<Assembly, string>>();
            probed = new Dictionary<string, int>(32);
            //generics = new Dictionary<string, Dictionary<string, string>>();
            assemblies = new AssemblyList(16);
            pypath = new List<string>(16);

            AppDomain domain = AppDomain.CurrentDomain;

            lhandler = new AssemblyLoadEventHandler(AssemblyLoadHandler);
            domain.AssemblyLoad += lhandler;

            rhandler = new ResolveEventHandler(ResolveHandler);
            domain.AssemblyResolve += rhandler;

            Assembly[] items = domain.GetAssemblies();
            foreach (var a in items)
            {
                try
                {
                    ScanAssembly(a);
                    assemblies.Add(a);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error scanning assembly {0}. {1}", a, ex));
                }
            }
        }


        //===================================================================
        // Cleanup resources upon shutdown of the Python runtime.
        //===================================================================

        internal static void Shutdown()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad -= lhandler;
            domain.AssemblyResolve -= rhandler;
        }


        //===================================================================
        // Event handler for assembly load events. At the time the Python
        // runtime loads, we scan the app domain to map the assemblies that
        // are loaded at the time. We also have to register this event handler
        // so that we can know about assemblies that get loaded after the
        // Python runtime is initialized.
        //===================================================================

        static void AssemblyLoadHandler(Object ob, AssemblyLoadEventArgs args)
        {
            Assembly assembly = args.LoadedAssembly;
            assemblies.Add(assembly);
            ScanAssembly(assembly);
        }


        //===================================================================
        // Event handler for assembly resolve events. This is needed because
        // we augment the assembly search path with the PYTHONPATH when we
        // load an assembly from Python. Because of that, we need to listen
        // for failed loads, because they might be dependencies of something
        // we loaded from Python which also needs to be found on PYTHONPATH.
        //===================================================================

        static Assembly ResolveHandler(Object ob, ResolveEventArgs args)
        {
            string name = args.Name.ToLower();
            foreach (Assembly a in assemblies)
            {
                string full = a.FullName.ToLower();
                if (full.StartsWith(name))
                {
                    return a;
                }
            }
            return LoadAssemblyPath(args.Name);
        }


        //===================================================================
        // We __really__ want to avoid using Python objects or APIs when
        // probing for assemblies to load, since our ResolveHandler may be
        // called in contexts where we don't have the Python GIL and can't
        // even safely try to get it without risking a deadlock ;(
        //
        // To work around that, we update a managed copy of sys.path (which
        // is the main thing we care about) when UpdatePath is called. The
        // import hook calls this whenever it knows its about to use the
        // assembly manager, which lets us keep up with changes to sys.path
        // in a relatively lightweight and low-overhead way.
        //===================================================================

        internal static void UpdatePath()
        {
            IntPtr list = Runtime.PySys_GetObject("path");
            int count = Runtime.PyList_Size(list);
            if (count != pypath.Count)
            {
                pypath.Clear();
                probed.Clear();
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = Runtime.PyList_GetItem(list, i);
                    string path = Runtime.GetManagedString(item);
                    if (path != null)
                    {
                        pypath.Add(path);
                    }
                }
            }
        }


        //===================================================================
        // Given an assembly name, try to find this assembly file using the
        // PYTHONPATH. If not found, return null to indicate implicit load
        // using standard load semantics (app base directory then GAC, etc.)
        //===================================================================

        public static string FindAssembly(string name)
        {
            char sep = Path.DirectorySeparatorChar;
            string path;
            string temp;

            for (int i = 0; i < pypath.Count; i++)
            {
                string head = pypath[i];
                if (head == null || head.Length == 0)
                {
                    path = name;
                }
                else
                {
                    path = head + sep + name;
                }

                temp = path + ".dll";
                if (File.Exists(temp))
                {
                    return temp;
                }
                temp = path + ".exe";
                if (File.Exists(temp))
                {
                    return temp;
                }
            }
            return null;
        }


        //===================================================================
        // Loads an assembly from the application directory or the GAC
        // given a simple assembly name. Returns the assembly if loaded.
        //===================================================================

        public static Assembly LoadAssembly(string name)
        {
            Assembly assembly = null;
            try
            {
                assembly = Assembly.Load(name);
            }
            catch (System.Exception e)
            {
                //if (!(e is System.IO.FileNotFoundException)) {
                //    throw;
                //}
            }
            return assembly;
        }


        //===================================================================
        // Loads an assembly using an augmented search path (the python path).
        //===================================================================

        public static Assembly LoadAssemblyPath(string name)
        {
            string path = FindAssembly(name);
            Assembly assembly = null;
            if (path != null)
            {
                try
                {
                    assembly = Assembly.LoadFrom(path);
                }
                catch
                {
                }
            }
            return assembly;
        }

        /// <summary>
        /// Loads an assembly using full path.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Assembly LoadAssemblyFullPath(string name)
        {
            Assembly assembly = null;
            if (Path.IsPathRooted(name))
            {
                if (!Path.HasExtension(name))
                    name = name + ".dll";
                if (File.Exists(name))
                {
                    try
                    {
                        assembly = Assembly.LoadFrom(name);
                    }
                    catch
                    {
                    }
                }
            }
            return assembly;
        }

        //===================================================================
        // Returns an assembly that's already been loaded
        //===================================================================

        public static Assembly FindLoadedAssembly(string name)
        {
            foreach (Assembly a in assemblies)
            {
                if (a.GetName().Name == name)
                {
                    return a;
                }
            }
            return null;
        }

        //===================================================================
        // Given a qualified name of the form A.B.C.D, attempt to load
        // an assembly named after each of A.B.C.D, A.B.C, A.B, A. This
        // will only actually probe for the assembly once for each unique
        // namespace. Returns true if any assemblies were loaded.
        // TODO item 3 "* Deprecate implicit loading of assemblies":
        //  Set the fromFile flag if the name of the loaded assembly matches
        //  the fully qualified name that was requested if the framework
        //  actually loads an assembly.
        // Call ONLY for namespaces that HAVE NOT been cached yet.
        //===================================================================

        public static bool LoadImplicit(string name, bool warn = true)
        {
            string[] names = name.Split('.');
            bool loaded = false;
            string s = "";
            Assembly lastAssembly = null;
            HashSet<Assembly> assembliesSet = null;
            for (int i = 0; i < names.Length; i++)
            {
                s = (i == 0) ? names[0] : s + "." + names[i];
                if (!probed.ContainsKey(s))
                {
                    if (assembliesSet == null)
                    {
                        assembliesSet = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
                    }
                    Assembly a = FindLoadedAssembly(s);
                    if (a == null)
                    {
                        a = LoadAssemblyPath(s);
                    }
                    if (a == null)
                    {
                        a = LoadAssembly(s);
                    }
                    if (a != null && !assembliesSet.Contains(a))
                    {
                        loaded = true;
                        lastAssembly = a;
                    }
                    probed[s] = 1;
                }
            }

            // Deprecation warning
            if (warn && loaded)
            {
                string deprWarning = String.Format(
                    "\nThe module was found, but not in a referenced namespace.\n" +
                    "Implicit loading is deprecated. Please use clr.AddReference(\"{0}\").",
                    Path.GetFileNameWithoutExtension(lastAssembly.Location));
                Exceptions.deprecation(deprWarning);
            }

            return loaded;
        }


        //===================================================================
        // Scans an assembly for exported namespaces, adding them to the
        // mapping of valid namespaces. Note that for a given namespace
        // a.b.c.d, each of a, a.b, a.b.c and a.b.c.d are considered to
        // be valid namespaces (to better match Python import semantics).
        //===================================================================

        internal static void ScanAssembly(Assembly assembly)
        {
            // A couple of things we want to do here: first, we want to
            // gather a list of all of the namespaces contributed to by
            // the assembly.

            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                string ns = t.Namespace ?? "";
                if (!namespaces.ContainsKey(ns))
                {
                    string[] names = ns.Split('.');
                    string s = "";
                    for (int n = 0; n < names.Length; n++)
                    {
                        s = (n == 0) ? names[0] : s + "." + names[n];
                        namespaces.TryAdd(s, new ConcurrentDictionary<Assembly, string>());
                    }
                }

                if (ns != null)
                {
                    namespaces[ns].TryAdd(assembly, String.Empty);
                }

                if (ns != null && t.IsGenericTypeDefinition)
                {
                    GenericUtil.Register(t);
                }
            }
        }

        public static AssemblyName[] ListAssemblies()
        {
            List<AssemblyName> names = new List<AssemblyName>(assemblies.Count);
            foreach (Assembly assembly in assemblies)
            {
                names.Add(assembly.GetName());
            }
            return names.ToArray();
        }

        //===================================================================
        // Returns true if the given qualified name matches a namespace
        // exported by an assembly loaded in the current app domain.
        //===================================================================

        public static bool IsValidNamespace(string name)
        {
            return !String.IsNullOrEmpty(name) && namespaces.ContainsKey(name);
        }

        //===================================================================
        // Returns list of assemblies that declare types in a given namespace
        //===================================================================

        public static IEnumerable<Assembly> GetAssemblies(string nsname)
        {
            if (!namespaces.ContainsKey(nsname))
                return new List<Assembly>();

            return namespaces[nsname].Keys;
        }

        //===================================================================
        // Returns the current list of valid names for the input namespace.
        //===================================================================

        public static List<string> GetNames(string nsname)
        {
            //Dictionary<string, int> seen = new Dictionary<string, int>();
            List<string> names = new List<string>(8);

            List<string> g = GenericUtil.GetGenericBaseNames(nsname);
            if (g != null)
            {
                foreach (string n in g)
                {
                    names.Add(n);
                }
            }

            if (namespaces.ContainsKey(nsname))
            {
                foreach (Assembly a in namespaces[nsname].Keys)
                {
                    Type[] types = a.GetTypes();
                    for (int i = 0; i < types.Length; i++)
                    {
                        Type t = types[i];
                        if ((t.Namespace ?? "") == nsname)
                        {
                            names.Add(t.Name);
                        }
                    }
                }
                int nslen = nsname.Length;
                foreach (string key in namespaces.Keys)
                {
                    if (key.Length > nslen && key.StartsWith(nsname))
                    {
                        //string tail = key.Substring(nslen);
                        if (key.IndexOf('.') == -1)
                        {
                            names.Add(key);
                        }
                    }
                }
            }
            return names;
        }

        //===================================================================
        // Returns the System.Type object for a given qualified name,
        // looking in the currently loaded assemblies for the named
        // type. Returns null if the named type cannot be found.
        //===================================================================

        public static Type LookupType(string qname)
        {
            foreach (Assembly assembly in assemblies)
            {
                Type type = assembly.GetType(qname);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        /// <summary>
        /// Wrapper around List<Assembly> for thread safe access
        /// </summary>
        private class AssemblyList : IEnumerable<Assembly>{
            private readonly List<Assembly> _list;
            private readonly ReaderWriterLockSlim _lock;

            public AssemblyList(int capacity) {
                _list = new List<Assembly>(capacity);
                _lock = new ReaderWriterLockSlim();
            }

            public int Count
            {
                get
                {
                    _lock.EnterReadLock();
                    try {
                        return _list.Count;
                    }
                    finally {
                        _lock.ExitReadLock();
                    }
                }
            }

            public void Add(Assembly assembly) {
                _lock.EnterWriteLock();
                try
                {
                    _list.Add(assembly);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            public IEnumerator GetEnumerator()
            {
                return ((IEnumerable<Assembly>) this).GetEnumerator();
            }

            /// <summary>
            /// Enumerator wrapping around <see cref="AssemblyList._list"/>'s enumerator.
            /// Acquires and releases a read lock on <see cref="AssemblyList._lock"/> during enumeration
            /// </summary>
            private class Enumerator : IEnumerator<Assembly>
            {
                private readonly AssemblyList _assemblyList;

                private readonly IEnumerator<Assembly> _listEnumerator;

                public Enumerator(AssemblyList assemblyList)
                {
                    _assemblyList = assemblyList;
                    _assemblyList._lock.EnterReadLock();
                    _listEnumerator = _assemblyList._list.GetEnumerator();
                }

                public void Dispose()
                {
                    _listEnumerator.Dispose();
                    _assemblyList._lock.ExitReadLock();
                }

                public bool MoveNext()
                {
                    return _listEnumerator.MoveNext();
                }

                public void Reset()
                {
                    _listEnumerator.Reset();
                }

                public Assembly Current { get { return _listEnumerator.Current; } }

                object IEnumerator.Current { get { return Current; } }
            }

            IEnumerator<Assembly> IEnumerable<Assembly>.GetEnumerator()
            {
                return new Enumerator(this);
            }
        }
    }
}
