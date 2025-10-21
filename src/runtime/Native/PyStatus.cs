using System;
using System.Runtime.InteropServices;

namespace Python.Runtime.Native;

[StructLayout(LayoutKind.Sequential)]
struct PyStatus
{
    int exitcode;
    IntPtr err_msg;
    IntPtr func;
}
