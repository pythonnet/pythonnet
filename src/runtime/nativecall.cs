using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;

namespace Python.Runtime
{
    /// <summary>
    /// Provides support for calling native code indirectly through
    /// function pointers. Most of the important parts of the Python
    /// C API can just be wrapped with p/invoke, but there are some
    /// situations (specifically, calling functions through Python
    /// type structures) where we need to call functions indirectly.
    /// </summary>
    internal class NativeCall
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Void_1_Delegate(IntPtr a1);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Int_3_Delegate(IntPtr a1, IntPtr a2, IntPtr a3);

        public static void Void_Call_1(IntPtr fp, IntPtr a1)
        {
            var d = GetDelegate<Interop.DestructorFunc>(fp);
            d(a1);
        }

        public static IntPtr Call_3(IntPtr fp, IntPtr a1, IntPtr a2, IntPtr a3)
        {
            var d = GetDelegate<Interop.TernaryFunc>(fp);
            return d(a1, a2, a3);
        }


        public static int Int_Call_3(IntPtr fp, IntPtr a1, IntPtr a2, IntPtr a3)
        {
            var d = GetDelegate<Interop.ObjObjArgFunc>(fp);
            return d(a1, a2, a3);
        }

        internal static T GetDelegate<T>(IntPtr fp) where T: Delegate
        {
            Delegate d = null;
            if (!Interop.allocatedThunks.TryGetValue(fp, out d))
            {
                // We don't cache this delegate because this is a pure delegate ot unmanaged.
                d = Marshal.GetDelegateForFunctionPointer<T>(fp);
            }
            return (T)d;
        }
    }
}
