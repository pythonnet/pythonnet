using System;
using System.Collections;
using System.Collections.Specialized;
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


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class ObjectOffset
    {
        static ObjectOffset()
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
            ob_dict = (n + 2) * size;
            ob_data = (n + 3) * size;
        }

        public static int magic(IntPtr ob)
        {
            if ((Runtime.PyObject_TypeCheck(ob, Exceptions.BaseException) ||
                 (Runtime.PyType_Check(ob) && Runtime.PyType_IsSubtype(ob, Exceptions.BaseException))))
            {
                return ExceptionOffset.ob_data;
            }
            return ob_data;
        }

        public static int DictOffset(IntPtr ob)
        {
            if ((Runtime.PyObject_TypeCheck(ob, Exceptions.BaseException) ||
                 (Runtime.PyType_Check(ob) && Runtime.PyType_IsSubtype(ob, Exceptions.BaseException))))
            {
                return ExceptionOffset.ob_dict;
            }
            return ob_dict;
        }

        public static int Size(IntPtr ob)
        {
            if ((Runtime.PyObject_TypeCheck(ob, Exceptions.BaseException) ||
                 (Runtime.PyType_Check(ob) && Runtime.PyType_IsSubtype(ob, Exceptions.BaseException))))
            {
                return ExceptionOffset.Size();
            }
#if PYTHON_WITH_PYDEBUG
            return 6 * IntPtr.Size;
#else
            return 4 * IntPtr.Size;
#endif
        }

#if PYTHON_WITH_PYDEBUG
        public static int _ob_next;
        public static int _ob_prev;
#endif
        public static int ob_refcnt;
        public static int ob_type;
        private static int ob_dict;
        private static int ob_data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class ExceptionOffset
    {
        static ExceptionOffset()
        {
            Type type = typeof(ExceptionOffset);
            FieldInfo[] fi = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(null, (i * size) + ObjectOffset.ob_type + size);
            }
        }

        public static int Size()
        {
            return ob_data + IntPtr.Size;
        }

        // PyException_HEAD
        // (start after PyObject_HEAD)
        public static int dict = 0;
        public static int args = 0;
#if PYTHON2
        public static int message = 0;
#elif PYTHON3
        public static int traceback = 0;
        public static int context = 0;
        public static int cause = 0;
        public static int suppress_context = 0;
#endif

        // extra c# data
        public static int ob_dict;
        public static int ob_data;
    }


#if PYTHON3
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
            for (int i = 0; i < m_free; i += IntPtr.Size)
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
#endif // PYTHON3

    /// <summary>
    /// TypeFlags(): The actual bit values for the Type Flags stored
    /// in a class.
    /// Note that the two values reserved for stackless have been put
    /// to good use as PythonNet specific flags (Managed and Subclass)
    /// </summary>
    internal class TypeFlags
    {
#if PYTHON2 // these flags were removed in Python 3
        public static int HaveGetCharBuffer = (1 << 0);
        public static int HaveSequenceIn = (1 << 1);
        public static int GC = 0;
        public static int HaveInPlaceOps = (1 << 3);
        public static int CheckTypes = (1 << 4);
        public static int HaveRichCompare = (1 << 5);
        public static int HaveWeakRefs = (1 << 6);
        public static int HaveIter = (1 << 7);
        public static int HaveClass = (1 << 8);
#endif
        public static int HeapType = (1 << 9);
        public static int BaseType = (1 << 10);
        public static int Ready = (1 << 12);
        public static int Readying = (1 << 13);
        public static int HaveGC = (1 << 14);
        // 15 and 16 are reserved for stackless
        public static int HaveStacklessExtension = 0;
        /* XXX Reusing reserved constants */
        public static int Managed = (1 << 15); // PythonNet specific
        public static int Subclass = (1 << 16); // PythonNet specific
        public static int HaveIndex = (1 << 17);
        /* Objects support nb_index in PyNumberMethods */
        public static int HaveVersionTag = (1 << 18);
        public static int ValidVersionTag = (1 << 19);
        public static int IsAbstract = (1 << 20);
        public static int HaveNewBuffer = (1 << 21);
        // TODO: Implement FastSubclass functions
        public static int IntSubclass = (1 << 23);
        public static int LongSubclass = (1 << 24);
        public static int ListSubclass = (1 << 25);
        public static int TupleSubclass = (1 << 26);
        public static int StringSubclass = (1 << 27);
        public static int UnicodeSubclass = (1 << 28);
        public static int DictSubclass = (1 << 29);
        public static int BaseExceptionSubclass = (1 << 30);
        public static int TypeSubclass = (1 << 31);

#if PYTHON2 // Default flags for Python 2
        public static int Default = (
            HaveGetCharBuffer |
            HaveSequenceIn |
            HaveInPlaceOps |
            HaveRichCompare |
            HaveWeakRefs |
            HaveIter |
            HaveClass |
            HaveStacklessExtension |
            HaveIndex |
            0);
#elif PYTHON3 // Default flags for Python 3
        public static int Default = (
            HaveStacklessExtension |
            HaveVersionTag);
#endif
    }


    // This class defines the function prototypes (delegates) used for low
    // level integration with the CPython runtime. It also provides name
    // based lookup of the correct prototype for a particular Python type
    // slot and utilities for generating method thunks for managed methods.

    internal class Interop
    {
        private static ArrayList keepAlive;
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

            keepAlive = new ArrayList();
            Marshal.AllocHGlobal(IntPtr.Size);
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
#if PYTHON2
            pmap["nb_divide"] = p["BinaryFunc"];
#endif
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
#if PYTHON2
            pmap["nb_inplace_divide"] = p["BinaryFunc"];
#endif
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

        internal static IntPtr GetThunk(MethodInfo method, string funcType = null)
        {
            Type dt;
            if (funcType != null)
                dt = typeof(Interop).GetNestedType(funcType) as Type;
            else
                dt = GetPrototype(method.Name);

            if (dt != null)
            {
                IntPtr tmp = Marshal.AllocHGlobal(IntPtr.Size);
                Delegate d = Delegate.CreateDelegate(dt, method);
                Thunk cb = new Thunk(d);
                Marshal.StructureToPtr(cb, tmp, false);
                IntPtr fp = Marshal.ReadIntPtr(tmp, 0);
                Marshal.FreeHGlobal(tmp);
                keepAlive.Add(d);
                return fp;
            }
            return IntPtr.Zero;
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


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct Thunk
    {
        public Delegate fn;

        public Thunk(Delegate d)
        {
            fn = d;
        }
    }
}
