using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    internal static class OperatorMethod
    {
        public static Dictionary<string, Tuple<string, int>> OpMethodMap { get; private set; }

        private static Dictionary<string, string> _pyOpNames;
        private static PyObject _opType;

        static OperatorMethod()
        {
            // TODO: Rich compare, inplace operator support
            OpMethodMap = new Dictionary<string, Tuple<string, int>>
            {
                ["op_Addition"] = Tuple.Create("__add__", TypeOffset.nb_add),
                ["op_Subtraction"] = Tuple.Create("__sub__", TypeOffset.nb_subtract),
                ["op_Multiply"] = Tuple.Create("__mul__", TypeOffset.nb_multiply),
#if PYTHON2
                ["op_Division"] = Tuple.Create("__div__", TypeOffset.nb_divide),
#else
                ["op_Division"] = Tuple.Create("__truediv__", TypeOffset.nb_true_divide),
#endif
                ["op_BitwiseAnd"] = Tuple.Create("__and__", TypeOffset.nb_and),
                ["op_BitwiseOr"] = Tuple.Create("__or__", TypeOffset.nb_or),
                ["op_ExclusiveOr"] = Tuple.Create("__xor__", TypeOffset.nb_xor),
                ["op_LeftShift"] = Tuple.Create("__lshift__", TypeOffset.nb_lshift),
                ["op_RightShift"] = Tuple.Create("__rshift__", TypeOffset.nb_rshift),
                ["op_Modulus"] = Tuple.Create("__mod__", TypeOffset.nb_remainder),
                ["op_OneComplement"] = Tuple.Create("__invert__", TypeOffset.nb_invert)
            };

            _pyOpNames = new Dictionary<string, string>();
            foreach (string name in OpMethodMap.Keys)
            {
                _pyOpNames.Add(GetPyMethodName(name), name);
            }
        }

        public static void Initialize()
        {
            _opType = GetOperatorType();
        }

        public static void Shutdown()
        {
            if (_opType != null)
            {
                _opType.Dispose();
                _opType = null;
            }
        }

        public static bool IsOperatorMethod(string methodName)
        {
            return OpMethodMap.ContainsKey(methodName);
        }

        public static bool IsPyOperatorMethod(string pyMethodName)
        {
            return _pyOpNames.ContainsKey(pyMethodName);
        }

        public static bool IsOperatorMethod(MethodBase method)
        {
            if (!method.IsSpecialName)
            {
                return false;
            }
            return OpMethodMap.ContainsKey(method.Name);
        }

        public static void FixupSlots(IntPtr pyType, Type clrType)
        {
            IntPtr tp_as_number = Marshal.ReadIntPtr(pyType, TypeOffset.tp_as_number);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            Debug.Assert(_opType != null);
            foreach (var method in clrType.GetMethods(flags))
            {
                if (!IsOperatorMethod(method))
                {
                    continue;
                }
                var slotdef = OpMethodMap[method.Name];
                int offset = slotdef.Item2;
                IntPtr func = Marshal.ReadIntPtr(_opType.Handle, offset);
                Marshal.WriteIntPtr(pyType, offset, func);
            }
        }

        public static string GetPyMethodName(string clrName)
        {
            return OpMethodMap[clrName].Item1;
        }

        private static string GenerateDummyCode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("class OperatorMethod(object):");
            foreach (var item in OpMethodMap.Values)
            {
                string def = string.Format("    def {0}(self, other): pass", item.Item1);
                sb.AppendLine(def);
            }
            return sb.ToString();
        }

        private static PyObject GetOperatorType()
        {
            using (PyDict locals = new PyDict())
            {
                // A hack way for getting typeobject.c::slotdefs
                string code = GenerateDummyCode();
                PythonEngine.Exec(code, null, locals.Handle);
                return locals.GetItem("OperatorMethod");
            }
        }
    }
}
