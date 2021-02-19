namespace Python.Runtime.Native
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    abstract class GeneratedTypeOffsets
    {
        protected GeneratedTypeOffsets()
        {
            var type = this.GetType();
            var offsetProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            int fieldSize = IntPtr.Size;
            for (int fieldIndex = 0; fieldIndex < offsetProperties.Length; fieldIndex++)
            {
                int offset = fieldIndex * fieldSize;
                offsetProperties[fieldIndex].SetValue(this, offset, index: null);
            }
        }
    }
}
