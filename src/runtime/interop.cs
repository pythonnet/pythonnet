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

    internal static partial class TypeOffset
    {
        static TypeOffset()
        {
            Type type = typeof(TypeOffset);
            FieldInfo[] fields = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fields.Length; i++)
            {
                int offset = i * size;
                FieldInfo fi = fields[i];
                fi.SetValue(null, offset);
            }
        }

        public static int magic() => ManagedDataOffsets.Magic;
    }

    internal static class ManagedDataOffsets
    {
        public static int Magic { get; private set; }
        public static readonly Dictionary<string, int> NameMapping = new Dictionary<string, int>();

        static class DataOffsets
        {
            public static readonly int ob_data;
            public static readonly int ob_dict;

            static DataOffsets()
            {
                FieldInfo[] fields = typeof(DataOffsets).GetFields(BindingFlags.Static | BindingFlags.Public);
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i].SetValue(null, -(i * IntPtr.Size) - IntPtr.Size);
                }
            }
        }

        static ManagedDataOffsets()
        {
            Type type = typeof(TypeOffset);
            foreach (FieldInfo fi in type.GetFields())
            {
                NameMapping[fi.Name] = (int)fi.GetValue(null);
            }
            // XXX: Use the members after PyHeapTypeObject as magic slot
            Magic = TypeOffset.members;

            FieldInfo[] fields = typeof(DataOffsets).GetFields(BindingFlags.Static | BindingFlags.Public);
            size = fields.Length * IntPtr.Size;
        }

        public static int GetSlotOffset(string name)
        {
            return NameMapping[name];
        }

        private static int BaseOffset(IntPtr type)
        {
            Debug.Assert(type != IntPtr.Zero);
            int typeSize = Marshal.ReadInt32(type, TypeOffset.tp_basicsize);
            Debug.Assert(typeSize > 0);
            return typeSize;
        }

        public static int DataOffset(IntPtr type)
        {
            return BaseOffset(type) + DataOffsets.ob_data;
        }

        public static int DictOffset(IntPtr type)
        {
            return BaseOffset(type) + DataOffsets.ob_dict;
        }

        public static int ob_data => DataOffsets.ob_data;
        public static int ob_dict => DataOffsets.ob_dict;
        public static int Size { get { return size; } }

        private static readonly int size;
    }

    internal static class OriginalObjectOffsets
    {
        static OriginalObjectOffsets()
        {
            int size = IntPtr.Size;
            var n = 0; // Py_TRACE_REFS add two pointers to PyObject_HEAD
#if PYTHON_WITH_PYDEBUG
            _ob_next = 0;
            _ob_prev = 1 * size;
            n = 2;
#endif
            ob_refcnt = (n + 0) * size;
            ob_type = (n + 1) * size;
        }

        public static int Size { get { return size; } }

        private static readonly int size =
#if PYTHON_WITH_PYDEBUG
            4 * IntPtr.Size;
#else
            2 * IntPtr.Size;
#endif

#if PYTHON_WITH_PYDEBUG
        public static int _ob_next;
        public static int _ob_prev;
#endif
        public static int ob_refcnt;
        public static int ob_type;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class ObjectOffset
    {
        static ObjectOffset()
        {
#if PYTHON_WITH_PYDEBUG
            _ob_next = OriginalObjectOffsets._ob_next;
            _ob_prev = OriginalObjectOffsets._ob_prev;
#endif
            ob_refcnt = OriginalObjectOffsets.ob_refcnt;
            ob_type = OriginalObjectOffsets.ob_type;

            size = OriginalObjectOffsets.Size + ManagedDataOffsets.Size;
        }

        public static int magic(IntPtr type)
        {
            return ManagedDataOffsets.DataOffset(type);
        }

        public static int TypeDictOffset(IntPtr type)
        {
            return ManagedDataOffsets.DictOffset(type);
        }

        public static int Size(IntPtr pyType)
        {
            if (IsException(pyType))
            {
                return ExceptionOffset.Size();
            }

            return size;
        }

#if PYTHON_WITH_PYDEBUG
        public static int _ob_next;
        public static int _ob_prev;
#endif
        public static int ob_refcnt;
        public static int ob_type;
        private static readonly int size;

        private static bool IsException(IntPtr pyObject)
        {
            var type = Runtime.PyObject_TYPE(pyObject);
            return Runtime.PyType_IsSameAsOrSubtype(type, ofType: Exceptions.BaseException)
                || Runtime.PyType_IsSameAsOrSubtype(type, ofType: Runtime.PyTypeType)
                && Runtime.PyType_IsSubtype(pyObject, Exceptions.BaseException);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class ExceptionOffset
    {
        static ExceptionOffset()
        {
            Type type = typeof(ExceptionOffset);
            FieldInfo[] fi = type.GetFields(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(null, (i * IntPtr.Size) + OriginalObjectOffsets.Size);
            }

            size = fi.Length * IntPtr.Size + OriginalObjectOffsets.Size + ManagedDataOffsets.Size;
        }

        public static int Size() { return size; }

        // PyException_HEAD
        // (start after PyObject_HEAD)
        public static int dict = 0;
        public static int args = 0;
        public static int traceback = 0;
        public static int context = 0;
        public static int cause = 0;
        public static int suppress_context = 0;

        private static readonly int size;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class BytesOffset
    {
        static BytesOffset()
        {
            Type type = typeof(BytesOffset);
            FieldInfo[] fi = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(null, i * size);
            }
        }

        /* The *real* layout of a type object when allocated on the heap */
        //typedef struct _heaptypeobject {
#if PYTHON_WITH_PYDEBUG
/* _PyObject_HEAD_EXTRA defines pointers to support a doubly-linked list of all live heap objects. */
        public static int _ob_next = 0;
        public static int _ob_prev = 0;
#endif
        // PyObject_VAR_HEAD {
        //     PyObject_HEAD {
        public static int ob_refcnt = 0;
        public static int ob_type = 0;
        // }
        public static int ob_size = 0; /* Number of items in _VAR_iable part */
        // }
        public static int ob_shash = 0;
        public static int ob_sval = 0; /* start of data */

        /* Invariants:
         *     ob_sval contains space for 'ob_size+1' elements.
         *     ob_sval[ob_size] == 0.
         *     ob_shash is the hash of the string or -1 if not computed yet.
         */
        //} PyBytesObject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class ModuleDefOffset
    {
        static ModuleDefOffset()
        {
            Type type = typeof(ModuleDefOffset);
            FieldInfo[] fi = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(null, (i * size) + TypeOffset.ob_size);
            }
        }

        public static IntPtr AllocModuleDef(string modulename)
        {
            byte[] ascii = Encoding.ASCII.GetBytes(modulename);
            int size = name + ascii.Length + 1;
            IntPtr ptr = Marshal.AllocHGlobal(size);
            for (int i = 0; i <= m_free; i += IntPtr.Size)
                Marshal.WriteIntPtr(ptr, i, IntPtr.Zero);
            Marshal.Copy(ascii, 0, (IntPtr)(ptr + name), ascii.Length);
            Marshal.WriteIntPtr(ptr, m_name, (IntPtr)(ptr + name));
            Marshal.WriteByte(ptr, name + ascii.Length, 0);
            return ptr;
        }

        public static void FreeModuleDef(IntPtr ptr)
        {
            Marshal.FreeHGlobal(ptr);
        }

        // typedef struct PyModuleDef{
        //  typedef struct PyModuleDef_Base {
        // starts after PyObject_HEAD (TypeOffset.ob_type + 1)
        public static int m_init = 0;
        public static int m_index = 0;
        public static int m_copy = 0;
        //  } PyModuleDef_Base
        public static int m_name = 0;
        public static int m_doc = 0;
        public static int m_size = 0;
        public static int m_methods = 0;
        public static int m_reload = 0;
        public static int m_traverse = 0;
        public static int m_clear = 0;
        public static int m_free = 0;
        // } PyModuleDef

        public static int name = 0;
    }


    /// <summary>
    /// TypeFlags(): The actual bit values for the Type Flags stored
    /// in a class.
    /// Note that the two values reserved for stackless have been put
    /// to good use as PythonNet specific flags (Managed and Subclass)
    /// </summary>
    internal class TypeFlags
    {
        public const int HeapType = (1 << 9);
        public const int BaseType = (1 << 10);
        public const int Ready = (1 << 12);
        public const int Readying = (1 << 13);
        public const int HaveGC = (1 << 14);
        // 15 and 16 are reserved for stackless
        public const int HaveStacklessExtension = 0;
        /* XXX Reusing reserved constants */
        public const int Managed = (1 << 15); // PythonNet specific
        public const int Subclass = (1 << 16); // PythonNet specific
        public const int HaveIndex = (1 << 17);
        /* Objects support nb_index in PyNumberMethods */
        public const int HaveVersionTag = (1 << 18);
        public const int ValidVersionTag = (1 << 19);
        public const int IsAbstract = (1 << 20);
        public const int HaveNewBuffer = (1 << 21);
        // TODO: Implement FastSubclass functions
        public const int IntSubclass = (1 << 23);
        public const int LongSubclass = (1 << 24);
        public const int ListSubclass = (1 << 25);
        public const int TupleSubclass = (1 << 26);
        public const int StringSubclass = (1 << 27);
        public const int UnicodeSubclass = (1 << 28);
        public const int DictSubclass = (1 << 29);
        public const int BaseExceptionSubclass = (1 << 30);
        public const int TypeSubclass = (1 << 31);

        public const int Default = (
            HaveStacklessExtension |
            HaveVersionTag);
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
            var info = new ThunkInfo(d);
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
