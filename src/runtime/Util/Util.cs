using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    internal static class Util
    {
        internal const string UnstableApiMessage =
            "This API is unstable, and might be changed or removed in the next minor release";
        internal const string MinimalPythonVersionRequired =
            "Only Python 3.6 or newer is supported";
        internal const string InternalUseOnly =
            "This API is for internal use only";

        internal const string UseOverloadWithReferenceTypes =
            "This API is unsafe, and will be removed in the future. Use overloads working with *Reference types";
        internal const string UseNone =
            $"null is not supported in this context. Use {nameof(PyObject)}.{nameof(PyObject.None)}";

        internal const string BadStr = "bad __str__";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt32(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadInt32(ob.DangerousGetAddress(), offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadInt64(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadInt64(ob.DangerousGetAddress(), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static T* ReadPtr<T>(BorrowedReference ob, int offset)
            where T: unmanaged
        {
            Debug.Assert(offset >= 0);
            IntPtr ptr = Marshal.ReadIntPtr(ob.DangerousGetAddress(), offset);
            return (T*)ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static IntPtr ReadIntPtr(BorrowedReference ob, int offset)
        {
            Debug.Assert(offset >= 0);
            return Marshal.ReadIntPtr(ob.DangerousGetAddress(), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static BorrowedReference ReadRef(BorrowedReference @ref, int offset)
        {
            Debug.Assert(offset >= 0);
            return new BorrowedReference(ReadIntPtr(@ref, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32(BorrowedReference ob, int offset, int value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteInt32(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(BorrowedReference ob, int offset, long value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteInt64(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteIntPtr(BorrowedReference ob, int offset, IntPtr value)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteRef(BorrowedReference ob, int offset, in StolenReference @ref)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, @ref.DangerousGetAddress());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void WriteNullableRef(BorrowedReference ob, int offset, in StolenReference @ref)
        {
            Debug.Assert(offset >= 0);
            Marshal.WriteIntPtr(ob.DangerousGetAddress(), offset, @ref.DangerousGetAddressOrNull());
        }


        internal static Int64 ReadCLong(BorrowedReference tp, int offset)
        {
            // On Windows, a C long is always 32 bits.
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                return ReadInt32(tp, offset);
            }
            else
            {
                return ReadInt64(tp, offset);
            }
        }

        internal static void WriteCLong(BorrowedReference type, int offset, Int64 value)
        {
            if (Runtime.IsWindows || Runtime.Is32Bit)
            {
                WriteInt32(type, offset, (Int32)(value & 0xffffffffL));
            }
            else
            {
                WriteInt64(type, offset, value);
            }
        }

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

        public static int HexToInt(char hex) => hex switch
        {
            >= '0' and <= '9' => hex - '0',
            >= 'a' and <= 'f' => hex - 'a' + 10,
            _ => throw new ArgumentOutOfRangeException(nameof(hex)),
        };

        public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;

        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
            where T: class
        {
            foreach (var item in source)
            {
                if (item is not null) yield return item;
            }
        }
    }
}
