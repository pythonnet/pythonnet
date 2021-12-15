using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // domain-level handlers are initialized in Initialize
        private static AssemblyLoadEventHandler lhandler;
        private static ResolveEventHandler rhandler;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // updated only under GIL?
        private static Dictionary<string, int> probed = new Dictionary<string, int>(32);

        // modified from event handlers below, potentially triggered from different .NET threads
        private static readonly ConcurrentQueue<Assembly> assemblies = new();
        internal static readonly List<string> pypath = new (capacity: 16);
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
            pypath.Clear();

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
        private static Assembly? ResolveHandler(object ob, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            foreach (var alreadyLoaded in assemblies)
            {
                if (AssemblyName.ReferenceMatchesDefinition(name, alreadyLoaded.GetName()))
                {
                    return alreadyLoaded;
                }
            }
            return LoadAssemblyPath(name.Name);
        }

        internal static AssemblyName? TryParseAssemblyName(string name)
        {
            try
            {
                return new AssemblyName(name);
            }
            catch (FileLoadException)
            {
                return null;
            }
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
            BorrowedReference list = Runtime.PySys_GetObject("path");
            var count = Runtime.PyList_Size(list);
            if (count != pypath.Count)
            {
                pypath.Clear();
                probed.Clear();
                for (var i = 0; i < count; i++)
                {
                    BorrowedReference item = Runtime.PyList_GetItem(list, i);
                    string? path = Runtime.GetManagedString(item);
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
        public static string FindAssembly(AssemblyName name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            return FindAssembly(name.Name);
        }

        /// <summary>
        /// Given an assembly name, try to find this assembly file using the
        /// PYTHONPATH. If not found, return null to indicate implicit load
        /// using standard load semantics (app base directory then GAC, etc.)
        /// </summary>
        public static string FindAssembly(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            return FindAssemblyCandidates(name).FirstOrDefault();
        }

        static IEnumerable<string> FindAssemblyCandidates(string name)
        {
            foreach (string head in pypath)
            {
                string path;
                if (head == null || head.Length == 0)
                {
                    path = name;
                }
                else
                {
                    path = Path.Combine(head, name);
                }

                string temp = path + ".dll";
                if (File.Exists(temp))
                {
                    yield return temp;
                }

                temp = path + ".exe";
                if (File.Exists(temp))
                {
                    yield return temp;
                }
            }
        }


        /// <summary>
        /// Loads an assembly from the application directory or the GAC
        /// given its name. Returns the assembly if loaded.
        /// </summary>
        public static Assembly LoadAssembly(AssemblyName name)
        {
            return Assembly.Load(name);
        }


        /// <summary>
        /// Loads an assembly using an augmented search path (the python path).
        /// </summary>
        public static Assembly? LoadAssemblyPath(string name)
        {
            string path = FindAssembly(name);
            if (path == null) return null;
            return Assembly.LoadFrom(path);
        }

        /// <summary>
        /// Loads an assembly using full path.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Assembly? LoadAssemblyFullPath(string name)
        {
            if (Path.IsPathRooted(name))
            {
                if (File.Exists(name))
                {
                    return Assembly.LoadFrom(name);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns an assembly that's already been loaded
        /// </summary>
        public static Assembly? FindLoadedAssembly(string name)
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
        /// Scans an assembly for exported namespaces, adding them to the
        /// mapping of valid namespaces. Note that for a given namespace
        /// a.b.c.d, each of a, a.b, a.b.c and a.b.c.d are considered to
        /// be valid namespaces (to better match Python import semantics).
        /// </summary>
        internal static void ScanAssembly(Assembly assembly)
        {
            if (assembly.GetCustomAttribute<PyExportAttribute>()?.Export == false)
            {
                return;
            }
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
                        if (namespaces.TryAdd(s, new ConcurrentDictionary<Assembly, string>()))
                        {
                            ImportHook.AddNamespace(s);
                        }
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
        /// Returns an IEnumerable<string> containing the namepsaces exported
        /// by loaded assemblies in the current app domain.
        /// </summary>
        public static IEnumerable<string> GetNamespaces ()
        {
            return namespaces.Keys;
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

            List<string>? g = GenericUtil.GetGenericBaseNames(nsname);
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
        /// Returns the <see cref="Type"/> objects for the given qualified name,
        /// looking in the currently loaded assemblies for the named
        /// type.
        /// </summary>
        public static IEnumerable<Type> LookupTypes(string qualifiedName)
            => assemblies.Select(assembly => assembly.GetType(qualifiedName)).Where(type => type != null && IsExported(type));

        internal static Type[] GetTypes(Assembly a)
        {
            if (a.IsDynamic)
            {
                try
                {
                    return a.GetTypes().Where(IsExported).ToArray();
                }
                catch (ReflectionTypeLoadException exc)
                {
                    // Return all types that were successfully loaded
                    return exc.Types.Where(x => x != null && IsExported(x)).ToArray();
                }
            }
            else
            {
                try
                {
                    return a.GetExportedTypes().Where(IsExported).ToArray();
                }
                catch (FileNotFoundException)
                {
                    return new Type[0];
                }
            }
        }

        static bool IsExported(Type type) => type.GetCustomAttribute<PyExportAttribute>()?.Export != false;
    }
}
