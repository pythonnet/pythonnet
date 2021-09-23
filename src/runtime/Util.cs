#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    internal static class Util
    {
        internal const string UnstableApiMessage =
            "This API is unstable, and might be changed or removed in the next minor release";
        internal const string MinimalPythonVersionRequired =
            "Only Python 3.5 or newer is supported";

        internal const string UseOverloadWithReferenceTypes =
            "This API is unsafe, and will be removed in the future. Use overloads working with *Reference types";

        internal static Int64 ReadCLong(IntPtr tp, int offset)
        {
            // On Windows, a C long is always 32 bits.
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                return Marshal.ReadInt32(tp, offset);
            }
            else
            {
                return Marshal.ReadInt64(tp, offset);
            }
        }

        internal static void WriteCLong(IntPtr type, int offset, Int64 flags)
        {
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                Marshal.WriteInt32(type, offset, (Int32)(flags & 0xffffffffL));
            }
            else
            {
                Marshal.WriteInt64(type, offset, flags);
            }
        }

        /// <summary>
        /// Null-coalesce: if <paramref name="primary"/> parameter is not
        /// <see cref="IntPtr.Zero"/>, return it. Otherwise return <paramref name="fallback"/>.
        /// </summary>
        internal static IntPtr Coalesce(this IntPtr primary, IntPtr fallback)
            => primary == IntPtr.Zero ? fallback : primary;

        /// <summary>
        /// Gets substring after last occurrence of <paramref name="symbol"/>
        /// </summary>
        internal static string? AfterLast(this string str, char symbol)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str));

            int last = str.LastIndexOf(symbol);
            return last >= 0 ? str.Substring(last + 1) : null;
        }

        internal static string ReadStringResource(this System.Reflection.Assembly assembly, string resourceName)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;
    }
}
