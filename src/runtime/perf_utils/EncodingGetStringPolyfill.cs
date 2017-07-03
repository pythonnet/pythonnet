using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// This polyfill is thread unsafe.
    /// </summary>
#if !NETSTANDARD
    public static class EncodingGetStringPolyfill
    {
        private static readonly MethodInfo PlatformGetStringMethodInfo =
            typeof(Encoding).GetMethod(
                "GetString",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[]
                {
                    typeof(byte*), typeof(int)
                }, null);

        private static readonly byte[] StdDecodeBuffer = PlatformGetStringMethodInfo == null ? new byte[1024 * 1024] : null;

        private static Dictionary<Encoding, EncodingGetStringUnsafeDelegate> PlatformGetStringMethodsDelegatesCache = new Dictionary<Encoding, EncodingGetStringUnsafeDelegate>();

        private unsafe delegate string EncodingGetStringUnsafeDelegate(byte* pstr, int size);

        public unsafe static string GetString(this Encoding encoding, byte* pstr, int size)
        {
            if (PlatformGetStringMethodInfo != null)
            {
                EncodingGetStringUnsafeDelegate getStringDelegate;
                if (!PlatformGetStringMethodsDelegatesCache.TryGetValue(encoding, out getStringDelegate))
                {
                    getStringDelegate =
                        (EncodingGetStringUnsafeDelegate)PlatformGetStringMethodInfo.CreateDelegate(
                            typeof(EncodingGetStringUnsafeDelegate), encoding);
                    PlatformGetStringMethodsDelegatesCache.Add(encoding, getStringDelegate);
                }
                return getStringDelegate(pstr, size);
            }

            byte[] buffer = size <= StdDecodeBuffer.Length ? StdDecodeBuffer : new byte[size];
            Marshal.Copy((IntPtr)pstr, buffer, 0, size);
            return encoding.GetString(buffer, 0, size);
        }
    }
#endif

}
