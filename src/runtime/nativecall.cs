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
    /// This class uses Reflection.Emit to generate IJW thunks that
    /// support indirect calls to native code using various common
    /// call signatures. This is mainly a workaround for the fact
    /// that you can't spell an indirect call in C# (but can in IL).
    /// Another approach that would work is for this to be turned
    /// into a separate utility program that could be run during the
    /// build process to generate the thunks as a separate assembly
    /// that could then be referenced by the main Python runtime.
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

        private static T GetDelegate<T>(IntPtr fp) where T: Delegate
        {
            // Use Marshal.GetDelegateForFunctionPointer<> directly after upgrade the framework
            return (T)Marshal.GetDelegateForFunctionPointer(fp, typeof(T));
        }
    }
}
