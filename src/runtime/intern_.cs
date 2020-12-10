using System;

namespace Python.Runtime
{
    static class PyIdentifier
    {
         public static IntPtr __name__;
         public static IntPtr __dict__;
         public static IntPtr __doc__;
         public static IntPtr __class__;
         public static IntPtr __module__;
         public static IntPtr __file__;
         public static IntPtr __slots__;
         public static IntPtr __self__;
         public static IntPtr __annotations__;
         public static IntPtr __init__;
         public static IntPtr __repr__;
         public static IntPtr __import__;
         public static IntPtr __builtins__;
         public static IntPtr builtins;
         public static IntPtr __overloads__;
         public static IntPtr Overloads;
    }


    static partial class InternString
    {
        private static readonly string[] _builtinNames = new string[]
        {
            "__name__",
            "__dict__",
            "__doc__",
            "__class__",
            "__module__",
            "__file__",
            "__slots__",
            "__self__",
            "__annotations__",
            "__init__",
            "__repr__",
            "__import__",
            "__builtins__",
            "builtins",
            "__overloads__",
            "Overloads",
        };
    }
}
