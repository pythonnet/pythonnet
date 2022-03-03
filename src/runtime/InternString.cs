using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    static partial class InternString
    {
        private static readonly Dictionary<string, PyString> _string2interns = new();
        private static readonly Dictionary<IntPtr, string> _intern2strings = new();
        const BindingFlags PyIdentifierFieldFlags = BindingFlags.Static | BindingFlags.NonPublic;

        static InternString()
        {
            var identifierNames = typeof(PyIdentifier).GetFields(PyIdentifierFieldFlags)
                                    .Select(fi => fi.Name.Substring(1));
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
            Debug.Assert(_string2interns.Count == 0);

            Type type = typeof(PyIdentifier);
            foreach (string name in _builtinNames)
            {
                NewReference pyStr = Runtime.PyUnicode_InternFromString(name);
                var op = new PyString(pyStr.StealOrThrow());
                Debug.Assert(name == op.As<string>());
                SetIntern(name, op);
                var field = type.GetField("f" + name, PyIdentifierFieldFlags)!;
                field.SetValue(null, op.rawPtr);
            }
        }

        public static void Shutdown()
        {
            foreach (var entry in _string2interns)
            {
                var field = typeof(PyIdentifier).GetField("f" + entry.Value, PyIdentifierFieldFlags)!;
                entry.Value.Dispose();
                field.SetValue(null, IntPtr.Zero);
            }

            _string2interns.Clear();
            _intern2strings.Clear();
        }

        public static string? GetManagedString(BorrowedReference op)
        {
            string s;
            if (TryGetInterned(op, out s))
            {
                return s;
            }
            return Runtime.GetManagedString(op);
        }

        public static bool TryGetInterned(BorrowedReference op, out string s)
        {
            return _intern2strings.TryGetValue(op.DangerousGetAddress(), out s);
        }

        private static void SetIntern(string s, PyString op)
        {
            _string2interns.Add(s, op);
            _intern2strings.Add(op.rawPtr, s);
        }
    }
}
