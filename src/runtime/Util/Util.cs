using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime
{
    public static class Util
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
            where T : unmanaged
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
            where T : class
        {
            foreach (var item in source)
            {
                if (item is not null) yield return item;
            }
        }

        /// <summary>
        /// Converts the specified name to snake case.
        /// </summary>
        /// <remarks>
        /// Reference: https://github.com/efcore/EFCore.NamingConventions/blob/main/EFCore.NamingConventions/Internal/SnakeCaseNameRewriter.cs
        /// </remarks>
        public static string ToSnakeCase(this string name, bool constant = false)
        {
            var builder = new StringBuilder(name.Length + Math.Min(2, name.Length / 5));
            var previousCategory = default(UnicodeCategory?);

            for (var currentIndex = 0; currentIndex < name.Length; currentIndex++)
            {
                var currentChar = name[currentIndex];
                if (currentChar == '_')
                {
                    builder.Append('_');
                    previousCategory = null;
                    continue;
                }

                var currentCategory = char.GetUnicodeCategory(currentChar);
                switch (currentCategory)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                        if (previousCategory == UnicodeCategory.SpaceSeparator ||
                            previousCategory == UnicodeCategory.LowercaseLetter ||
                            previousCategory != UnicodeCategory.DecimalDigitNumber &&
                            previousCategory != null &&
                            currentIndex > 0 &&
                            currentIndex + 1 < name.Length &&
                            char.IsLower(name[currentIndex + 1]))
                        {
                            builder.Append('_');
                        }
                        if (!constant)
                        {
                            currentChar = char.ToLower(currentChar, CultureInfo.InvariantCulture);
                        }
                        break;

                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.DecimalDigitNumber:
                        if (previousCategory == UnicodeCategory.SpaceSeparator)
                        {
                            builder.Append('_');
                        }
                        if (constant)
                        {
                            currentChar = char.ToUpper(currentChar, CultureInfo.InvariantCulture);
                        }
                        break;

                    default:
                        if (previousCategory != null)
                        {
                            previousCategory = UnicodeCategory.SpaceSeparator;
                        }
                        continue;
                }

                builder.Append(currentChar);
                previousCategory = currentCategory;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts the specified field name to snake case.
        /// const and static readonly fields are considered as constants and are converted to uppercase.
        /// </summary>
        public static string ToSnakeCase(this FieldInfo fieldInfo)
        {
            return fieldInfo.Name.ToSnakeCase(fieldInfo.IsLiteral || (fieldInfo.IsStatic && fieldInfo.IsInitOnly));
        }

        /// <summary>
        /// Converts the specified property name to snake case.
        /// Static properties without a setter are considered as constants and are converted to uppercase.
        /// </summary>
        public static string ToSnakeCase(this PropertyInfo propertyInfo)
        {
            var constant = propertyInfo.CanRead && !propertyInfo.CanWrite &&
                (propertyInfo.GetGetMethod()?.IsStatic ?? propertyInfo.GetGetMethod(nonPublic: true)?.IsStatic ?? false);
            return propertyInfo.Name.ToSnakeCase(constant);
        }
    }
}
