using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        //
        // WARNING: Dangerous if cross-app domain usage is ever supported
        //    Reusing the dictionary with assemblies accross multiple initializations is problematic. 
        //    Loading happens from CurrentDomain (see line 53). And if the first call is from AppDomain that is later unloaded, 
        //    than it can end up referring to assemblies that are already unloaded (default behavior after unload appDomain - 
        //     unless LoaderOptimization.MultiDomain is used);
        //    So for multidomain support it is better to have the dict. recreated for each app-domain initialization
        private static ConcurrentDictionary<string, ConcurrentDictionary<Assembly, string>> namespaces =
            new ConcurrentDictionary<string, ConcurrentDictionary<Assembly, string>>();
        //private static Dictionary<string, Dictionary<string, string>> generics;
        private static AssemblyLoadEventHandler lhandler;
        private static ResolveEventHandler rhandler;

        // updated only under GIL?
        private static Dictionary<string, int> probed = new Dictionary<string, int>(32);

        // modified from event handlers below, potentially triggered from different .NET threads
        private static ConcurrentQueue<Assembly> assemblies;
        internal static List<string> pypath;

        private AssemblyManager()
        {
        }

        /// <summary>
        /// Initialization performed on startup of the Python runtime. Here we
        /// scan all of the currently loaded assemblies to determine exported
        /// names, and register to be notified of new assembly loads.
        /// </summary>
        internal static void Initialize()
        {
            assemblies = new ConcurrentQueue<Assembly>();
            pypath = new List<string>(16);

            AppDomain domain = AppDomain.CurrentDomain;

            lhandler = new AssemblyLoadEventHandler(AssemblyLoadHandler);
            domain.AssemblyLoad += lhandler;

            rhandler = new ResolveEventHandler(ResolveHandler);
            domain.AssemblyResolve += rhandler;

            Assembly[] items = domain.GetAssemblies();
            foreach (Assembly a in items)
            {
                try
                {
                    ScanAssembly(a);
                    assemblies.Enqueue(a);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error scanning assembly {0}. {1}", a, ex);
                }
            }
        }


        /// <summary>
        /// Cleanup resources upon shutdown of the Python runtime.
        /// </summary>
        internal static void Shutdown()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad -= lhandler;
            domain.AssemblyResolve -= rhandler;
        }


        /// <summary>
        /// Event handler for assembly load events. At the time the Python
        /// runtime loads, we scan the app domain to map the assemblies that
        /// are loaded at the time. We also have to register this event handler
        /// so that we can know about assemblies that get loaded after the
        /// Python runtime is initialized.
        /// </summary>
        private static void AssemblyLoadHandler(object ob, AssemblyLoadEventArgs args)
        {
            Assembly assembly = args.LoadedAssembly;
            assemblies.Enqueue(assembly);
            ScanAssembly(assembly);
        }


        /// <summary>
        /// Event handler for assembly resolve events. This is needed because
        /// we augment the assembly search path with the PYTHONPATH when we
        /// load an assembly from Python. Because of that, we need to listen
        /// for failed loads, because they might be dependencies of something
        /// we loaded from Python which also needs to be found on PYTHONPATH.
        /// </summary>
        private static Assembly ResolveHandler(object ob, ResolveEventArgs args)
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


        /// <summary>
        /// We __really__ want to avoid using Python objects or APIs when
        /// probing for assemblies to load, since our ResolveHandler may be
        /// called in contexts where we don't have the Python GIL and can't
        /// even safely try to get it without risking a deadlock ;(
        /// To work around that, we update a managed copy of sys.path (which
        /// is the main thing we care about) when UpdatePath is called. The
        /// import hook calls this whenever it knows its about to use the
        /// assembly manager, which lets us keep up with changes to sys.path
        /// in a relatively lightweight and low-overhead way.
        /// </summary>
        internal static void UpdatePath()
        {
            IntPtr list = Runtime.PySys_GetObject("path");
            var count = Runtime.PyList_Size(list);
            if (count != pypath.Count)
            {
                pypath.Clear();
                probed.Clear();
                for (var i = 0; i < count; i++)
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


        /// <summary>
        /// Given an assembly name, try to find this assembly file using the
        /// PYTHONPATH. If not found, return null to indicate implicit load
        /// using standard load semantics (app base directory then GAC, etc.)
        /// </summary>
        public static string FindAssembly(string name)
        {
            char sep = Path.DirectorySeparatorChar;

            foreach (string head in pypath)
            {
                string path;
                if (head == null || head.Length == 0)
                {
                    path = name;
                }
                else
                {
                    path = head + sep + name;
                }

                string temp = path + ".dll";
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


        /// <summary>
        /// Loads an assembly from the application directory or the GAC
        /// given a simple assembly name. Returns the assembly if loaded.
        /// </summary>
        public static Assembly LoadAssembly(string name)
        {
            Assembly assembly = null;
            try
            {
                assembly = Assembly.Load(name);
            }
            catch (Exception)
            {
                //if (!(e is System.IO.FileNotFoundException))
                //{
                //    throw;
                //}
            }
            return assembly;
        }


        /// <summary>
        /// Loads an assembly using an augmented search path (the python path).
        /// </summary>
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
                catch (Exception)
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
                {
                    name = name + ".dll";
                }
                if (File.Exists(name))
                {
                    try
                    {
                        assembly = Assembly.LoadFrom(name);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return assembly;
        }

        /// <summary>
        /// Returns an assembly that's already been loaded
        /// </summary>
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

        /// <summary>
        /// Given a qualified name of the form A.B.C.D, attempt to load
        /// an assembly named after each of A.B.C.D, A.B.C, A.B, A. This
        /// will only actually probe for the assembly once for each unique
        /// namespace. Returns true if any assemblies were loaded.
        /// </summary>
        /// <remarks>
        /// TODO item 3 "* Deprecate implicit loading of assemblies":
        /// Set the fromFile flag if the name of the loaded assembly matches
        /// the fully qualified name that was requested if the framework
        /// actually loads an assembly.
        /// Call ONLY for namespaces that HAVE NOT been cached yet.
        /// </remarks>
        public static bool LoadImplicit(string name, bool warn = true)
        {
            string[] names = name.Split('.');
            var loaded = false;
            var s = "";
            Assembly lastAssembly = null;
            HashSet<Assembly> assembliesSet = null;
            for (var i = 0; i < names.Length; i++)
            {
                s = i == 0 ? names[0] : s + "." + names[i];
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
                string location = Path.GetFileNameWithoutExtension(lastAssembly.Location);
                string deprWarning = "The module was found, but not in a referenced namespace.\n" +
                                     $"Implicit loading is deprecated. Please use clr.AddReference('{location}').";
                Exceptions.deprecation(deprWarning);
            }

            return loaded;
        }


        /// <summary>
        /// Scans an assembly for exported namespaces, adding them to the
        /// mapping of valid namespaces. Note that for a given namespace
        /// a.b.c.d, each of a, a.b, a.b.c and a.b.c.d are considered to
        /// be valid namespaces (to better match Python import semantics).
        /// </summary>
        internal static void ScanAssembly(Assembly assembly)
        {
            // A couple of things we want to do here: first, we want to
            // gather a list of all of the namespaces contributed to by
            // the assembly.
            foreach (Type t in GetTypes(assembly))
            {
                string ns = t.Namespace ?? "";
                if (!namespaces.ContainsKey(ns))
                {
                    string[] names = ns.Split('.');
                    var s = "";
                    for (var n = 0; n < names.Length; n++)
                    {
                        s = n == 0 ? names[0] : s + "." + names[n];
                        namespaces.TryAdd(s, new ConcurrentDictionary<Assembly, string>());
                    }
                }

                if (ns != null)
                {
                    namespaces[ns].TryAdd(assembly, string.Empty);
                }

                if (ns != null && t.IsGenericTypeDefinition)
                {
                    GenericUtil.Register(t);
                }
            }
        }

        public static AssemblyName[] ListAssemblies()
        {
            var names = new List<AssemblyName>(assemblies.Count);
            foreach (Assembly assembly in assemblies)
            {
                names.Add(assembly.GetName());
            }
            return names.ToArray();
        }

        /// <summary>
        /// Returns true if the given qualified name matches a namespace
        /// exported by an assembly loaded in the current app domain.
        /// </summary>
        public static bool IsValidNamespace(string name)
        {
            return !string.IsNullOrEmpty(name) && namespaces.ContainsKey(name);
        }

        /// <summary>
        /// Returns list of assemblies that declare types in a given namespace
        /// </summary>
        public static IEnumerable<Assembly> GetAssemblies(string nsname)
        {
            return !namespaces.ContainsKey(nsname) ? new List<Assembly>() : namespaces[nsname].Keys;
        }

        /// <summary>
        /// Returns the current list of valid names for the input namespace.
        /// </summary>
        public static List<string> GetNames(string nsname)
        {
            //Dictionary<string, int> seen = new Dictionary<string, int>();
            var names = new List<string>(8);

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
                    foreach (Type t in GetTypes(a))
                    {
                        if ((t.Namespace ?? "") == nsname && !t.IsNested)
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

        /// <summary>
        /// Returns the System.Type object for a given qualified name,
        /// looking in the currently loaded assemblies for the named
        /// type. Returns null if the named type cannot be found.
        /// </summary>
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

        internal static Type[] GetTypes(Assembly a)
        {
            if (a.IsDynamic)
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException exc)
                {
                    // Return all types that were successfully loaded
                    return exc.Types.Where(x => x != null).ToArray();
                }
            }
            else
            {
                try
                {
                    return a.GetExportedTypes();
                }
                catch (FileNotFoundException)
                {
                    return new Type[0];
                }
            }
        }
    }
}