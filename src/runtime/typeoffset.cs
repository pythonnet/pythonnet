using System;
using System.Reflection;

namespace Python.Runtime
{
    internal static partial class TypeOffset
    {
        static TypeOffset()
        {
            Type type = typeof(TypeOffset);
            FieldInfo[] fields = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fields.Length; i++)
            {
                int offset = i * size;
                FieldInfo fi = fields[i];
                fi.SetValue(null, offset);
            }
        }

        public static int magic() => ManagedDataOffsets.Magic;
    }
}
