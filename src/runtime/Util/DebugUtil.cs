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
        public static void Print(string msg, BorrowedReference member)
        {
            string result = msg;
            result += " ";

            if (member == null)
            {
                Console.WriteLine("null arg to print");
            }
            using var ob = Runtime.PyObject_Repr(member);
            result += Runtime.GetManagedString(ob.BorrowOrThrow());
            result += " ";
            Console.WriteLine(result);
        }

        [Conditional("DEBUG")]
        public static void Print(string msg)
        {
            Console.WriteLine(msg);
        }

        [Conditional("DEBUG")]
        internal static void DumpType(BorrowedReference type)
        {
            IntPtr op = Util.ReadIntPtr(type, TypeOffset.tp_name);
            string name = Marshal.PtrToStringAnsi(op);

            Console.WriteLine("Dump type: {0}", name);

            var objMember = Util.ReadRef(type, TypeOffset.ob_type);
            Print("  type: ", objMember);

            objMember = Util.ReadRef(type, TypeOffset.tp_base);
            Print("  base: ", objMember);

            objMember = Util.ReadRef(type, TypeOffset.tp_bases);
            Print("  bases: ", objMember);

            //op = Util.ReadIntPtr(type, TypeOffset.tp_mro);
            //DebugUtil.Print("  mro: ", op);


            var slots = TypeOffset.GetOffsets();

            foreach (var entry in slots)
            {
                int offset = entry.Value;
                name = entry.Key;
                op = Util.ReadIntPtr(type, offset);
                Console.WriteLine("  {0}: {1}", name, op);
            }

            Console.WriteLine("");
            Console.WriteLine("");

            objMember = Util.ReadRef(type, TypeOffset.tp_dict);
            if (objMember == null)
            {
                Console.WriteLine("  dict: null");
            }
            else
            {
                Print("  dict: ", objMember);
            }
        }

        [Conditional("DEBUG")]
        internal static void DumpInst(BorrowedReference ob)
        {
            BorrowedReference tp = Runtime.PyObject_TYPE(ob);
            nint sz = Util.ReadIntPtr(tp, TypeOffset.tp_basicsize);

            for (nint i = 0; i < sz; i += IntPtr.Size)
            {
                var pp = new IntPtr(ob.DangerousGetAddress().ToInt64() + i);
                IntPtr v = Marshal.ReadIntPtr(pp);
                Console.WriteLine("offset {0}: {1}", i, v);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }

        [Conditional("DEBUG")]
        internal static void Debug(string msg)
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

        [Conditional("DEBUG")]
        public static void AssertHasReferences(BorrowedReference obj)
        {
            nint refcount = Runtime.Refcount(obj);
            System.Diagnostics.Debug.Assert(refcount > 0, "Object refcount is 0 or less");
        }

        [Conditional("DEBUG")]
        public static void EnsureGIL()
        {
            System.Diagnostics.Debug.Assert(HaveInterpreterLock(), "GIL must be acquired");
        }

        public static bool HaveInterpreterLock() => Runtime.PyGILState_Check() == 1;
    }
}
