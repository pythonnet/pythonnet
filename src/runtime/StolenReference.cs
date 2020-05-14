namespace Python.Runtime
{
    using System;

    /// <summary>
    /// Should only be used for the arguments of Python C API functions, that steal references
    /// </summary>
    [NonCopyable]
    readonly ref struct StolenReference
    {
        readonly IntPtr pointer;

        internal StolenReference(IntPtr pointer)
        {
            this.pointer = pointer;
        }
    }
}
