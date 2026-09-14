using System.Linq;
using System;
using System.Collections.Generic;
using System.Resources;

namespace Python.Runtime
{
    /// <summary>
    /// This class is responsible for efficiently maintaining the bits
    /// of information we need to support aliases with 'nice names'.
    /// </summary>
    internal static class GenericUtil
    {
        /// <summary>
        /// Maps namespace -> generic base name -> list of generic type names.
        /// </summary>
        // Lock: nested Dict/List mutations cannot be expressed with ConcurrentDictionary alone.
        private static Dictionary<string, Dictionary<string, List<string>>> mapping = new();
        private static readonly object _lock = new();

        public static void Reset()
        {
            lock (_lock)
            {
                mapping = new Dictionary<string, Dictionary<string, List<string>>>();
            }
        }

        /// <summary>
        /// Register a generic type that appears in a given namespace.
        /// </summary>
        /// <param name="t">A generic type definition (<c>t.IsGenericTypeDefinition</c> must be true)</param>
        internal static void Register(Type t)
        {
            if (null == t.Namespace || null == t.Name)
            {
                return;
            }

            lock (_lock)
            {
                if (!mapping.TryGetValue(t.Namespace, out var nsmap))
                {
                    nsmap = new Dictionary<string, List<string>>();
                    mapping[t.Namespace] = nsmap;
                }
                string basename = GetBasename(t.Name);
                if (!nsmap.TryGetValue(basename, out var gnames))
                {
                    gnames = new List<string>();
                    nsmap[basename] = gnames;
                }
                gnames.Add(t.Name);
            }
        }

        /// <summary>
        /// xxx
        /// </summary>
        public static List<string>? GetGenericBaseNames(string ns)
        {
            lock (_lock)
            {
                if (mapping.TryGetValue(ns, out var nsmap))
                {
                    return nsmap.Keys.ToList();
                }
                return null;
            }
        }

        /// <summary>
        /// Finds a generic type with the given number of generic parameters and the same name and namespace as <paramref name="t"/>.
        /// </summary>
        public static Type? GenericForType(Type t, int paramCount)
        {
            return GenericByName(t.Namespace, t.Name, paramCount);
        }

        /// <summary>
        /// Finds a generic type in the given namespace with the given name and number of generic parameters.
        /// </summary>
        public static Type? GenericByName(string ns, string basename, int paramCount)
        {
            // Snapshot under lock; AssemblyManager below can reenter Register.
            string[]? candidates = null;
            lock (_lock)
            {
                if (mapping.TryGetValue(ns, out var nsmap)
                    && nsmap.TryGetValue(GetBasename(basename), out var names))
                {
                    candidates = names.ToArray();
                }
            }
            if (candidates == null) return null;
            foreach (string name in candidates)
            {
                string qname = $"{ns}.{name}";
                Type o = AssemblyManager.LookupTypes(qname).FirstOrDefault();
                if (o != null && o.GetGenericArguments().Length == paramCount)
                {
                    return o;
                }
            }
            return null;
        }

        /// <summary>
        /// xxx
        /// </summary>
        public static string? GenericNameForBaseName(string ns, string name)
        {
            lock (_lock)
            {
                if (mapping.TryGetValue(ns, out var nsmap)
                    && nsmap.TryGetValue(name, out var gnames)
                    && gnames.Count > 0)
                {
                    return gnames[0];
                }
                return null;
            }
        }

        private static string GetBasename(string name)
        {
            int tick = name.IndexOf("`");
            if (tick > -1)
            {
                return name.Substring(0, tick);
            }
            else
            {
                return name;
            }
        }
    }
}
