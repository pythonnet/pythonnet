using System;
using System.Runtime.InteropServices;

namespace Python.Runtime.Native;

/// <remarks><c>PyGILState_STATE</c></remarks>
[StructLayout(LayoutKind.Sequential)]
struct PyGILState
{
    IntPtr handle;
}
