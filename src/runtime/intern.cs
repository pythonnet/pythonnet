using System;
using System.Collections.Generic;
using System.Linq;

namespace Python.Runtime
{
    static partial class InternString
    {
        private static Dictionary<string, IntPtr> _string2interns;
        private static Dictionary<IntPtr, string> _intern2strings;

        static InternString()
        {
            var identifierNames = typeof(PyIdentifier).GetFields().Select(fi => fi.Name);
            var validNames = new HashSet<string>(identifierNames);
            if (validNames.Count != _builtinNames.Length)
            {
                throw new InvalidOperationException("Identifiers args not matching");
            }
            foreach (var name in _builtinNames)
            {
                if (!validNames.Contains(name))
                {
                    throw new InvalidOperationException($"{name} is not declared");
                }
            }
        }

        public static void Initialize()
        {
            _string2interns = new Dictionary<string, IntPtr>();
            _intern2strings = new Dictionary<IntPtr, string>();

            Type type = typeof(PyIdentifier);
            foreach (string name in _builtinNames)
            {
                IntPtr op = Runtime.PyUnicode_InternFromString(name);
                SetIntern(name, op);
                type.GetField(name).SetValue(null, op);
            }
        }

        public static void Shutdown()
        {
            foreach (var ptr in _intern2strings.Keys)
            {
                Runtime.XDecref(ptr);
            }
            _string2interns = null;
            _intern2strings = null;
        }

        public static string GetManagedString(IntPtr op)
        {
            string s;
            if (TryGetInterned(op, out s))
            {
                return s;
            }
            return Runtime.GetManagedString(op);
        }

        public static bool TryGetInterned(IntPtr op, out string s)
        {
            return _intern2strings.TryGetValue(op, out s);
        }

        private static void SetIntern(string s, IntPtr op)
        {
            _string2interns.Add(s, op);
            _intern2strings.Add(op, s);
        }
    }
}
