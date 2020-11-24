namespace Python.Runtime.Native
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;

    static class ABI
    {
        internal static void Initialize(Version version, BorrowedReference pyType)
        {
            string offsetsClassSuffix = string.Format(CultureInfo.InvariantCulture,
                                                      "{0}{1}", version.Major, version.Minor);

            var thisAssembly = Assembly.GetExecutingAssembly();

            string className = "Python.Runtime.TypeOffset" + offsetsClassSuffix;
            Type typeOffsetsClass = thisAssembly.GetType(className, throwOnError: false);
            if (typeOffsetsClass is null)
                throw new NotSupportedException($"Python ABI v{version} is not supported");
            var typeOffsets = (ITypeOffsets)Activator.CreateInstance(typeOffsetsClass);
            TypeOffset.Use(typeOffsets);

            ManagedDataOffsets.Magic = Marshal.ReadInt32(pyType.DangerousGetAddress(), TypeOffset.tp_basicsize);
        }
    }
}
