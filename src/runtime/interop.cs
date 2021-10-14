using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// This file defines objects to support binary interop with the Python
    /// runtime. Generally, the definitions here need to be kept up to date
    /// when moving to new Python versions.
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.All)]
    public class DocStringAttribute : Attribute
    {
        public DocStringAttribute(string docStr)
        {
            DocString = docStr;
        }

        public string DocString
        {
            get { return docStr; }
            set { docStr = value; }
        }

        private string docStr;
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class PythonMethodAttribute : Attribute
    {
        public PythonMethodAttribute()
        {
        }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ModuleFunctionAttribute : Attribute
    {
        public ModuleFunctionAttribute()
        {
        }
    }

    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ForbidPythonThreadsAttribute : Attribute
    {
        public ForbidPythonThreadsAttribute()
        {
        }
    }


    [Serializable]
    [AttributeUsage(AttributeTargets.Property)]
    internal class ModulePropertyAttribute : Attribute
    {
        public ModulePropertyAttribute()
        {
        }
    }

    /// <summary>
    /// TypeFlags(): The actual bit values for the Type Flags stored
    /// in a class.
    /// Note that the two values reserved for stackless have been put
    /// to good use as PythonNet specific flags (Managed and Subclass)
    /// </summary>
    // Py_TPFLAGS_*
    [Flags]
    public enum TypeFlags: int
    {
        HeapType = (1 << 9),
        BaseType = (1 << 10),
        Ready = (1 << 12),
        Readying = (1 << 13),
        HaveGC = (1 << 14),
        // 15 and 16 are reserved for stackless
        HaveStacklessExtension = 0,
        /* XXX Reusing reserved constants */
        /// <remarks>PythonNet specific</remarks>
        HasClrInstance = (1 << 15),
        /// <remarks>PythonNet specific</remarks>
        Subclass = (1 << 16),
        HaveIndex = (1 << 17),
        /* Objects support nb_index in PyNumberMethods */
        HaveVersionTag = (1 << 18),
        ValidVersionTag = (1 << 19),
        IsAbstract = (1 << 20),
        HaveNewBuffer = (1 << 21),
        // TODO: Implement FastSubclass functions
        IntSubclass = (1 << 23),
        LongSubclass = (1 << 24),
        ListSubclass = (1 << 25),
        TupleSubclass = (1 << 26),
        StringSubclass = (1 << 27),
        UnicodeSubclass = (1 << 28),
        DictSubclass = (1 << 29),
        BaseExceptionSubclass = (1 << 30),
        TypeSubclass = (1 << 31),

        Default = (
            HaveStacklessExtension |
            HaveVersionTag),
    }


    // This class defines the function prototypes (delegates) used for low
    // level integration with the CPython runtime. It also provides name
    // based lookup of the correct prototype for a particular Python type
    // slot and utilities for generating method thunks for managed methods.

    internal class Interop
    {
        private static Hashtable pmap;

        static Interop()
        {
            // Here we build a mapping of PyTypeObject slot names to the
            // appropriate prototype (delegate) type to use for the slot.

            Type[] items = typeof(Interop).GetNestedTypes();
            Hashtable p = new Hashtable();

            for (int i = 0; i < items.Length; i++)
            {
                Type item = items[i];
                p[item.Name] = item;
            }

            pmap = new Hashtable();

            pmap["tp_dealloc"] = p["DestructorFunc"];
            pmap["tp_print"] = p["PrintFunc"];
            pmap["tp_getattr"] = p["BinaryFunc"];
            pmap["tp_setattr"] = p["ObjObjArgFunc"];
            pmap["tp_compare"] = p["ObjObjFunc"];
            pmap["tp_repr"] = p["UnaryFunc"];
            pmap["tp_hash"] = p["UnaryFunc"];
            pmap["tp_call"] = p["TernaryFunc"];
            pmap["tp_str"] = p["UnaryFunc"];
            pmap["tp_getattro"] = p["BinaryFunc"];
            pmap["tp_setattro"] = p["ObjObjArgFunc"];
            pmap["tp_traverse"] = p["ObjObjArgFunc"];
            pmap["tp_clear"] = p["InquiryFunc"];
            pmap["tp_richcompare"] = p["RichCmpFunc"];
            pmap["tp_iter"] = p["UnaryFunc"];
            pmap["tp_iternext"] = p["UnaryFunc"];
            pmap["tp_descr_get"] = p["TernaryFunc"];
            pmap["tp_descr_set"] = p["ObjObjArgFunc"];
            pmap["tp_init"] = p["ObjObjArgFunc"];
            pmap["tp_alloc"] = p["IntArgFunc"];
            pmap["tp_new"] = p["TernaryFunc"];
            pmap["tp_free"] = p["DestructorFunc"];
            pmap["tp_is_gc"] = p["InquiryFunc"];

            pmap["nb_add"] = p["BinaryFunc"];
            pmap["nb_subtract"] = p["BinaryFunc"];
            pmap["nb_multiply"] = p["BinaryFunc"];
            pmap["nb_remainder"] = p["BinaryFunc"];
            pmap["nb_divmod"] = p["BinaryFunc"];
            pmap["nb_power"] = p["TernaryFunc"];
            pmap["nb_negative"] = p["UnaryFunc"];
            pmap["nb_positive"] = p["UnaryFunc"];
            pmap["nb_absolute"] = p["UnaryFunc"];
            pmap["nb_nonzero"] = p["InquiryFunc"];
            pmap["nb_invert"] = p["UnaryFunc"];
            pmap["nb_lshift"] = p["BinaryFunc"];
            pmap["nb_rshift"] = p["BinaryFunc"];
            pmap["nb_and"] = p["BinaryFunc"];
            pmap["nb_xor"] = p["BinaryFunc"];
            pmap["nb_or"] = p["BinaryFunc"];
            pmap["nb_coerce"] = p["ObjObjFunc"];
            pmap["nb_int"] = p["UnaryFunc"];
            pmap["nb_long"] = p["UnaryFunc"];
            pmap["nb_float"] = p["UnaryFunc"];
            pmap["nb_oct"] = p["UnaryFunc"];
            pmap["nb_hex"] = p["UnaryFunc"];
            pmap["nb_inplace_add"] = p["BinaryFunc"];
            pmap["nb_inplace_subtract"] = p["BinaryFunc"];
            pmap["nb_inplace_multiply"] = p["BinaryFunc"];
            pmap["nb_inplace_remainder"] = p["BinaryFunc"];
            pmap["nb_inplace_power"] = p["TernaryFunc"];
            pmap["nb_inplace_lshift"] = p["BinaryFunc"];
            pmap["nb_inplace_rshift"] = p["BinaryFunc"];
            pmap["nb_inplace_and"] = p["BinaryFunc"];
            pmap["nb_inplace_xor"] = p["BinaryFunc"];
            pmap["nb_inplace_or"] = p["BinaryFunc"];
            pmap["nb_floor_divide"] = p["BinaryFunc"];
            pmap["nb_true_divide"] = p["BinaryFunc"];
            pmap["nb_inplace_floor_divide"] = p["BinaryFunc"];
            pmap["nb_inplace_true_divide"] = p["BinaryFunc"];
            pmap["nb_index"] = p["UnaryFunc"];

            pmap["sq_length"] = p["InquiryFunc"];
            pmap["sq_concat"] = p["BinaryFunc"];
            pmap["sq_repeat"] = p["IntArgFunc"];
            pmap["sq_item"] = p["IntArgFunc"];
            pmap["sq_slice"] = p["IntIntArgFunc"];
            pmap["sq_ass_item"] = p["IntObjArgFunc"];
            pmap["sq_ass_slice"] = p["IntIntObjArgFunc"];
            pmap["sq_contains"] = p["ObjObjFunc"];
            pmap["sq_inplace_concat"] = p["BinaryFunc"];
            pmap["sq_inplace_repeat"] = p["IntArgFunc"];

            pmap["mp_length"] = p["InquiryFunc"];
            pmap["mp_subscript"] = p["BinaryFunc"];
            pmap["mp_ass_subscript"] = p["ObjObjArgFunc"];

            pmap["bf_getreadbuffer"] = p["IntObjArgFunc"];
            pmap["bf_getwritebuffer"] = p["IntObjArgFunc"];
            pmap["bf_getsegcount"] = p["ObjObjFunc"];
            pmap["bf_getcharbuffer"] = p["IntObjArgFunc"];
        }

        internal static Type GetPrototype(string name)
        {
            return pmap[name] as Type;
        }


        internal static Dictionary<IntPtr, Delegate> allocatedThunks = new Dictionary<IntPtr, Delegate>();

        internal static ThunkInfo GetThunk(MethodInfo method, string funcType = null)
        {
            Type dt;
            if (funcType != null)
                dt = typeof(Interop).GetNestedType(funcType) as Type;
            else
                dt = GetPrototype(method.Name);

            if (dt == null)
            {
                return ThunkInfo.Empty;
            }
            Delegate d = Delegate.CreateDelegate(dt, method);
            return GetThunk(d);
        }

        internal static ThunkInfo GetThunk(Delegate @delegate)
        {
            var info = new ThunkInfo(@delegate);
            allocatedThunks[info.Address] = @delegate;
            return info;
        }


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr UnaryFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr BinaryFunc(IntPtr ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr TernaryFunc(IntPtr ob, IntPtr a1, IntPtr a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int InquiryFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr IntArgFunc(IntPtr ob, int arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr IntIntArgFunc(IntPtr ob, int a1, int a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int IntObjArgFunc(IntPtr ob, int a1, IntPtr a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int IntIntObjArgFunc(IntPtr o, int a, int b, IntPtr c);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ObjObjArgFunc(IntPtr o, IntPtr a, IntPtr b);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ObjObjFunc(IntPtr ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestructorFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int PrintFunc(IntPtr ob, IntPtr a, int b);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr RichCmpFunc(IntPtr ob, IntPtr a, int b);
    }


    internal class ThunkInfo
    {
        public readonly Delegate Target;
        public readonly IntPtr Address;

        public static readonly ThunkInfo Empty = new ThunkInfo(null);

        public ThunkInfo(Delegate target)
        {
            if (target == null)
            {
                return;
            }
            Target = target;
            Address = Marshal.GetFunctionPointerForDelegate(target);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PyGC_Node
    {
        public IntPtr gc_next;
        public IntPtr gc_prev;
        public IntPtr gc_refs;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PyGC_Head
    {
        public PyGC_Node gc;
    }


    [StructLayout(LayoutKind.Sequential)]
    struct PyMethodDef
    {
        public IntPtr ml_name;
        public IntPtr ml_meth;
        public int ml_flags;
        public IntPtr ml_doc;
    }

}
