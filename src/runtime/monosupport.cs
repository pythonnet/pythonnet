#if UCS4
using System;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Unix;

namespace Python.Runtime
{
    public class Utf32Marshaler : ICustomMarshaler
    {
        private static Utf32Marshaler instance = new Utf32Marshaler();

        public static ICustomMarshaler GetInstance(string s)
        {
            return instance;
        }

        public void CleanUpManagedData(object o)
        {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            UnixMarshal.FreeHeap(pNativeData);
        }

        public int GetNativeDataSize()
        {
            return IntPtr.Size;
        }

        public IntPtr MarshalManagedToNative(object obj)
        {
            var s = obj as string;
            return s == null ? IntPtr.Zero : UnixMarshal.StringToHeap(s, Encoding.UTF32);
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            return UnixMarshal.PtrToString(pNativeData, Encoding.UTF32);
        }
    }
}
#endif
