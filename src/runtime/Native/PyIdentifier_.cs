using System;

namespace Python.Runtime
{
    static class PyIdentifier
    {
#pragma warning disable CS0649 // indentifier is never assigned to (assigned with reflection)
         static IntPtr f__name__;
         public static BorrowedReference __name__ => new(f__name__);
         static IntPtr f__dict__;
         public static BorrowedReference __dict__ => new(f__dict__);
         static IntPtr f__doc__;
         public static BorrowedReference __doc__ => new(f__doc__);
         static IntPtr f__class__;
         public static BorrowedReference __class__ => new(f__class__);
         static IntPtr f__clear_reentry_guard__;
         public static BorrowedReference __clear_reentry_guard__ => new(f__clear_reentry_guard__);
         static IntPtr f__module__;
         public static BorrowedReference __module__ => new(f__module__);
         static IntPtr f__file__;
         public static BorrowedReference __file__ => new(f__file__);
         static IntPtr f__slots__;
         public static BorrowedReference __slots__ => new(f__slots__);
         static IntPtr f__self__;
         public static BorrowedReference __self__ => new(f__self__);
         static IntPtr f__annotations__;
         public static BorrowedReference __annotations__ => new(f__annotations__);
         static IntPtr f__init__;
         public static BorrowedReference __init__ => new(f__init__);
         static IntPtr f__repr__;
         public static BorrowedReference __repr__ => new(f__repr__);
         static IntPtr f__import__;
         public static BorrowedReference __import__ => new(f__import__);
         static IntPtr f__builtins__;
         public static BorrowedReference __builtins__ => new(f__builtins__);
         static IntPtr fbuiltins;
         public static BorrowedReference builtins => new(fbuiltins);
         static IntPtr f__overloads__;
         public static BorrowedReference __overloads__ => new(f__overloads__);
         static IntPtr fOverloads;
         public static BorrowedReference Overloads => new(fOverloads);
#pragma warning restore CS0649        // indentifier is never assigned to (assigned with reflection)
    }


    static partial class InternString
    {
        private static readonly string[] _builtinNames = new string[]
        {
            "__name__",
            "__dict__",
            "__doc__",
            "__class__",
            "__clear_reentry_guard__",
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
