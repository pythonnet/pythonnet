using System;

namespace Python.Runtime.Native;

[Flags]
enum PyMethodFlags : int
{
    [Obsolete]
    OLDARGS = 0,
    VarArgs = 1,
    Keywords = 2,
    NoArgs = 4,
    O = 8,

    Class = 0x10,
    Static = 0x20,

    /// <summary>
    /// Allows a method to be entered even though a slot has
    /// already filled the entry.  When defined, the flag allows a separate
    /// method, "__contains__" for example, to coexist with a defined
    /// slot like sq_contains.
    /// </summary>
    Coexist = 0x40,

    /// <remarks>3.10+</remarks>
    FastCall = 0x80,

    /// <summary>
    /// The function stores an
    /// additional reference to the class that defines it;
    /// both self and class are passed to it.
    /// It uses PyCMethodObject instead of PyCFunctionObject.
    /// May not be combined with METH_NOARGS, METH_O, METH_CLASS or METH_STATIC.
    /// </summary>
    /// <remarks>3.9+</remarks>
    Method = 0x0200,
}
