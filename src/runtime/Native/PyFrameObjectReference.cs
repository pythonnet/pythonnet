using System;

namespace Python.Runtime.Native
{
    readonly ref struct PyFrameObjectReference
    {
        readonly IntPtr pointer;

        public bool IsNull => this.pointer == IntPtr.Zero;
    }
}
