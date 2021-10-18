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
    internal unsafe class NativeCall
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Void_1_Delegate(IntPtr a1);

        public static void CallDealloc(IntPtr fp, StolenReference a1)
        {
            var d = (delegate* unmanaged[Cdecl]<StolenReference, void>)fp;
            d(a1);
        }

        public static NewReference Call_3(IntPtr fp, BorrowedReference a1, BorrowedReference a2, BorrowedReference a3)
        {
            var d = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)fp;
            return d(a1, a2, a3);
        }


        public static int Int_Call_3(IntPtr fp, BorrowedReference a1, BorrowedReference a2, BorrowedReference a3)
        {
            var d = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)fp;
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
