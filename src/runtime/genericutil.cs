using System;
using System.Collections.Generic;

namespace Python.Runtime
{
    /// <summary>
    /// This class is responsible for efficiently maintaining the bits
    /// of information we need to support aliases with 'nice names'.
    /// </summary>
    internal class GenericUtil
    {
        private static Dictionary<string, Dictionary<string, List<string>>> mapping;

        private GenericUtil()
        {
        }

        static GenericUtil()
        {
            mapping = new Dictionary<string, Dictionary<string, List<string>>>();
        }

        /// <summary>
        /// Register a generic type that appears in a given namespace.
        /// </summary>
        internal static void Register(Type t)
        {
            lock (mapping)
            {
                if (null == t.Namespace || null == t.Name)
                {
                    return;
                }

                Dictionary<string, List<string>> nsmap;
                if (!mapping.TryGetValue(t.Namespace, out nsmap))
                {
                    nsmap = new Dictionary<string, List<string>>();
                    mapping[t.Namespace] = nsmap;
                }
                string basename = t.Name;
                int tick = basename.IndexOf("`");
                if (tick > -1)
                {
                    basename = basename.Substring(0, tick);
                }
                List<string> gnames;
                if (!nsmap.TryGetValue(basename, out gnames))
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
        public static List<string> GetGenericBaseNames(string ns)
        {
            lock (mapping)
            {
                Dictionary<string, List<string>> nsmap;
                if (!mapping.TryGetValue(ns, out nsmap))
                {
                    return null;
                }
                var names = new List<string>();
                foreach (string key in nsmap.Keys)
                {
                    names.Add(key);
                }
                return names;
            }
        }

        /// <summary>
        /// xxx
        /// </summary>
        public static Type GenericForType(Type t, int paramCount)
        {
            return GenericByName(t.Namespace, t.Name, paramCount);
        }

        public static Type GenericByName(string ns, string name, int paramCount)
        {
            foreach (Type t in GenericsByName(ns, name))
            {
                if (t.GetGenericArguments().Length == paramCount)
                {
                    return t;
                }
            }
            return null;
        }

        public static List<Type> GenericsForType(Type t)
        {
            return GenericsByName(t.Namespace, t.Name);
        }

        public static List<Type> GenericsByName(string ns, string basename)
        {
            lock (mapping)
            {
                Dictionary<string, List<string>> nsmap;
                if (!mapping.TryGetValue(ns, out nsmap))
                {
                    return null;
                }

                int tick = basename.IndexOf("`");
                if (tick > -1)
                {
                    basename = basename.Substring(0, tick);
                }

                List<string> names;
                if (!nsmap.TryGetValue(basename, out names))
                {
                    return null;
                }

                var result = new List<Type>();
                foreach (string name in names)
                {
                    string qname = ns + "." + name;
                    Type o = AssemblyManager.LookupType(qname);
                    if (o != null)
                    {
                        result.Add(o);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// xxx
        /// </summary>
        public static string GenericNameForBaseName(string ns, string name)
        {
            lock (mapping)
            {
                Dictionary<string, List<string>> nsmap;
                if (!mapping.TryGetValue(ns, out nsmap))
                {
                    return null;
                }
                List<string> gnames = null;
                nsmap.TryGetValue(name, out gnames);
                if (gnames?.Count > 0)
                {
                    return gnames[0];
                }
            }
            return null;
        }
    }
}
