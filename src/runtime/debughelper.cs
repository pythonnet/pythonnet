using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Python.Runtime
{
    /// <summary>
    /// Debugging helper utilities.
    /// The methods are only executed when the DEBUG flag is set. Otherwise
    /// they are automagically hidden by the compiler and silently suppressed.
    /// </summary>
    internal class DebugUtil
    {
        [Conditional("DEBUG")]
        public static void Print(string msg, params IntPtr[] args)
        {
            string result = msg;
            result += " ";

            foreach (IntPtr t in args)
            {
                if (t == IntPtr.Zero)
                {
                    Console.WriteLine("null arg to print");
                }
                IntPtr ob = Runtime.PyObject_Repr(t);
                result += Runtime.GetManagedString(ob);
                Runtime.XDecref(ob);
                result += " ";
            }
            Console.WriteLine(result);
        }

        [Conditional("DEBUG")]
        public static void Print(string msg)
        {
            Console.WriteLine(msg);
        }

        [Conditional("DEBUG")]
        internal static void DumpType(IntPtr type)
        {
            IntPtr op = Marshal.ReadIntPtr(type, TypeOffset.tp_name);
            string name = Marshal.PtrToStringAnsi(op);

            Console.WriteLine("Dump type: {0}", name);

            op = Marshal.ReadIntPtr(type, TypeOffset.ob_type);
            Print("  type: ", op);

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_base);
            Print("  base: ", op);

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_bases);
            Print("  bases: ", op);

            //op = Marshal.ReadIntPtr(type, TypeOffset.tp_mro);
            //DebugUtil.Print("  mro: ", op);


            FieldInfo[] slots = typeof(TypeOffset).GetFields();
            int size = IntPtr.Size;

            for (var i = 0; i < slots.Length; i++)
            {
                int offset = i * size;
                name = slots[i].Name;
                op = Marshal.ReadIntPtr(type, offset);
                Console.WriteLine("  {0}: {1}", name, op);
            }

            Console.WriteLine("");
            Console.WriteLine("");

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            if (op == IntPtr.Zero)
            {
                Console.WriteLine("  dict: null");
            }
            else
            {
                Print("  dict: ", op);
            }
        }

        [Conditional("DEBUG")]
        internal static void DumpInst(IntPtr ob)
        {
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            var sz = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_basicsize);

            for (var i = 0; i < sz; i += IntPtr.Size)
            {
                var pp = new IntPtr(ob.ToInt64() + i);
                IntPtr v = Marshal.ReadIntPtr(pp);
                Console.WriteLine("offset {0}: {1}", i, v);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }

        [Conditional("DEBUG")]
        internal static void debug(string msg)
        {
            var st = new StackTrace(1, true);
            StackFrame sf = st.GetFrame(0);
            MethodBase mb = sf.GetMethod();
            Type mt = mb.DeclaringType;
            string caller = mt.Name + "." + sf.GetMethod().Name;
            Thread t = Thread.CurrentThread;
            string tid = t.GetHashCode().ToString();
            Console.WriteLine("thread {0} : {1}", tid, caller);
            Console.WriteLine("  {0}", msg);
        }

        /// <summary>
        /// Helper function to inspect/compare managed to native conversions.
        /// Especially useful when debugging CustomMarshaler.
        /// </summary>
        /// <param name="bytes"></param>
        [Conditional("DEBUG")]
        public static void PrintHexBytes(byte[] bytes)
        {
            if ((bytes == null) || (bytes.Length == 0))
            {
                Console.WriteLine("<none>");
            }
            else
            {
                foreach (byte t in bytes)
                {
                    Console.Write("{0:X2} ", t);
                }
                Console.WriteLine();
            }
        }
    }
}
