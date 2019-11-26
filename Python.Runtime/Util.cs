using System;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    internal class Util
    {
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
    }
}