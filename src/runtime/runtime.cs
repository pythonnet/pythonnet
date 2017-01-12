using System;
using System.Runtime.InteropServices;
using System.Security;
#if (UCS4)
using System.Text;
using Mono.Unix;

#endif

#if (UCS2 && PYTHON3)
using System.Text;
#endif

namespace Python.Runtime
{
    [SuppressUnmanagedCodeSecurityAttribute()]
    static class NativeMethods
    {
#if (MONO_LINUX || MONO_OSX)
        static public IntPtr LoadLibrary(string fileName) {
            return dlopen(fileName, RTLD_NOW | RTLD_SHARED);
        }

        static public void FreeLibrary(IntPtr handle) {
            dlclose(handle);
        }

        static public IntPtr GetProcAddress(IntPtr dllHandle, string name) {
            // look in the exe if dllHandle is NULL
            if (IntPtr.Zero == dllHandle)
                dllHandle = RTLD_DEFAULT;

            // clear previous errors if any
            dlerror();
            var res = dlsym(dllHandle, name);
            var errPtr = dlerror();
            if (errPtr != IntPtr.Zero) {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
        }

#if (MONO_OSX)
        static int RTLD_NOW = 0x2;
        static int RTLD_SHARED = 0x20;
        static IntPtr RTLD_DEFAULT = new IntPtr(-2);

        [DllImport("__Internal")]
        private static extern IntPtr dlopen(String fileName, int flags);

        [DllImport("__Internal")]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport("__Internal")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("__Internal")]
        private static extern IntPtr dlerror();
#else
        static int RTLD_NOW = 0x2;
        static int RTLD_SHARED = 0x20;
        static IntPtr RTLD_DEFAULT = IntPtr.Zero;

        [DllImport("libdl.so")]
        private static extern IntPtr dlopen(String fileName, int flags);

        [DllImport("libdl.so")]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport("libdl.so")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so")]
        private static extern IntPtr dlerror();
#endif

#else
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
#endif
    }

    public class Runtime
    {
        /// <summary>
        /// Encapsulates the low-level Python C API. Note that it is
        /// the responsibility of the caller to have acquired the GIL
        /// before calling any of these methods.
        /// </summary>
#if (UCS4)
        public const int UCS = 4;
#endif
#if (UCS2)
        public const int UCS = 2;
#endif
#if ! (UCS2 || UCS4)
#error You must define either UCS2 or UCS4!
#endif

#if (PYTHON23)
        public const string pyversion = "2.3";
        public const int pyversionnumber = 23;
#endif
#if (PYTHON24)
        public const string pyversion = "2.4";
        public const int pyversionnumber = 24;
#endif
#if (PYTHON25)
        public const string pyversion = "2.5";
        public const int pyversionnumber = 25;
#endif
#if (PYTHON26)
        public const string pyversion = "2.6";
        public const int pyversionnumber = 26;
#endif
#if (PYTHON27)
        public const string pyversion = "2.7";
        public const int pyversionnumber = 27;
#endif
#if (PYTHON32)
        public const string pyversion = "3.2";
        public const int pyversionnumber = 32;
#endif
#if (PYTHON33)
        public const string pyversion = "3.3";
        public const int pyversionnumber = 33;
#endif
#if (PYTHON34)
        public const string pyversion = "3.4";
        public const int pyversionnumber = 34;
#endif
#if (PYTHON35)
        public const string pyversion = "3.5";
        public const int pyversionnumber = 35;
#endif
#if (PYTHON36)
        public const string pyversion = "3.6";
        public const int pyversionnumber = 36;
#endif
#if ! (PYTHON23 || PYTHON24 || PYTHON25 || PYTHON26 || PYTHON27 || PYTHON32 || PYTHON33 || PYTHON34 || PYTHON35 || PYTHON36)
#error You must define one of PYTHON23 to PYTHON36
#endif

#if (PYTHON23)
        internal const string dllBase = "python23";
#endif
#if (PYTHON24)
        internal const string dllBase = "python24";
#endif
#if (PYTHON25)
        internal const string dllBase = "python25";
#endif
#if (PYTHON26)
        internal const string dllBase = "python26";
#endif
#if (PYTHON27)
        internal const string dllBase = "python27";
#endif
#if (MONO_LINUX || MONO_OSX)
#if (PYTHON32)
        internal const string dllBase = "python3.2";
#endif
#if (PYTHON33)
        internal const string dllBase = "python3.3";
#endif
#if (PYTHON34)
        internal const string dllBase = "python3.4";
#endif
#if (PYTHON35)
        internal const string dllBase = "python3.5";
#endif
#if (PYTHON36)
        internal const string dllBase = "python3.6";
#endif
#else
#if (PYTHON32)
        internal const string dllBase = "python32";
#endif
#if (PYTHON33)
        internal const string dllBase = "python33";
#endif
#if (PYTHON34)
        internal const string dllBase = "python34";
#endif
#if (PYTHON35)
        internal const string dllBase = "python35";
#endif
#if (PYTHON36)
        internal const string dllBase = "python36";
#endif
#endif

#if (PYTHON_WITH_PYDEBUG)
        internal const string dllWithPyDebug = "d";
#else
        internal const string dllWithPyDebug = "";
#endif
#if (PYTHON_WITH_PYMALLOC)
        internal const string dllWithPyMalloc = "m";
#else
        internal const string dllWithPyMalloc = "";
#endif
#if (PYTHON_WITH_WIDE_UNICODE)
        internal const string dllWithWideUnicode = "u";
#else
        internal const string dllWithWideUnicode = "";
#endif

#if (PYTHON_WITHOUT_ENABLE_SHARED)
        public const string dll = "__Internal";
#else
        public const string dll = dllBase + dllWithPyDebug + dllWithPyMalloc + dllWithWideUnicode;
#endif

        // set to true when python is finalizing
        internal static Object IsFinalizingLock = new Object();
        internal static bool IsFinalizing = false;

        internal static bool is32bit;

        /// <summary>
        /// Intitialize the runtime...
        /// </summary>
        internal static void Initialize()
        {
            is32bit = IntPtr.Size == 4;

            if (0 == Runtime.Py_IsInitialized())
            {
                Runtime.Py_Initialize();
            }

            if (0 == Runtime.PyEval_ThreadsInitialized())
            {
                Runtime.PyEval_InitThreads();
            }

#if PYTHON3
            IntPtr op = Runtime.PyImport_ImportModule("builtins");
            IntPtr dict = Runtime.PyObject_GetAttrString(op, "__dict__");
#elif PYTHON2
            IntPtr dict = Runtime.PyImport_GetModuleDict();
            IntPtr op = Runtime.PyDict_GetItemString(dict, "__builtin__");
#endif
            PyNotImplemented = Runtime.PyObject_GetAttrString(op, "NotImplemented");
            PyBaseObjectType = Runtime.PyObject_GetAttrString(op, "object");

            PyModuleType = Runtime.PyObject_Type(op);
            PyNone = Runtime.PyObject_GetAttrString(op, "None");
            PyTrue = Runtime.PyObject_GetAttrString(op, "True");
            PyFalse = Runtime.PyObject_GetAttrString(op, "False");

            PyBoolType = Runtime.PyObject_Type(PyTrue);
            PyNoneType = Runtime.PyObject_Type(PyNone);
            PyTypeType = Runtime.PyObject_Type(PyNoneType);

            op = Runtime.PyObject_GetAttrString(dict, "keys");
            PyMethodType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            // For some arcane reason, builtins.__dict__.__setitem__ is *not*
            // a wrapper_descriptor, even though dict.__setitem__ is.
            //
            // object.__init__ seems safe, though.
            op = Runtime.PyObject_GetAttrString(PyBaseObjectType, "__init__");
            PyWrapperDescriptorType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

#if PYTHON3
            Runtime.XDecref(dict);
#endif

            op = Runtime.PyString_FromString("string");
            PyStringType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyUnicode_FromString("unicode");
            PyUnicodeType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

#if PYTHON3
            op = Runtime.PyBytes_FromString("bytes");
            PyBytesType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);
#endif

            op = Runtime.PyTuple_New(0);
            PyTupleType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyList_New(0);
            PyListType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyDict_New();
            PyDictType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyInt_FromInt32(0);
            PyIntType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyLong_FromLong(0);
            PyLongType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

            op = Runtime.PyFloat_FromDouble(0);
            PyFloatType = Runtime.PyObject_Type(op);
            Runtime.XDecref(op);

#if PYTHON3
        PyClassType = IntPtr.Zero;
        PyInstanceType = IntPtr.Zero;
#elif PYTHON2
            IntPtr s = Runtime.PyString_FromString("_temp");
            IntPtr d = Runtime.PyDict_New();

            IntPtr c = Runtime.PyClass_New(IntPtr.Zero, d, s);
            PyClassType = Runtime.PyObject_Type(c);

            IntPtr i = Runtime.PyInstance_New(c, IntPtr.Zero, IntPtr.Zero);
            PyInstanceType = Runtime.PyObject_Type(i);

            Runtime.XDecref(s);
            Runtime.XDecref(i);
            Runtime.XDecref(c);
            Runtime.XDecref(d);
#endif

            Error = new IntPtr(-1);

#if PYTHON3
        IntPtr dll = IntPtr.Zero;
        if ("__Internal" != Runtime.dll) {
            NativeMethods.LoadLibrary(Runtime.dll);
        }
        _PyObject_NextNotImplemented = NativeMethods.GetProcAddress(dll, "_PyObject_NextNotImplemented");
#if !(MONO_LINUX || MONO_OSX)
        if (IntPtr.Zero != dll) {
            NativeMethods.FreeLibrary(dll);
        }
#endif
#endif

            // Initialize modules that depend on the runtime class.
            AssemblyManager.Initialize();
            PyCLRMetaType = MetaType.Initialize();
            Exceptions.Initialize();
            ImportHook.Initialize();

            // Need to add the runtime directory to sys.path so that we
            // can find built-in assemblies like System.Data, et. al.
            string rtdir = RuntimeEnvironment.GetRuntimeDirectory();
            IntPtr path = Runtime.PySys_GetObject("path");
            IntPtr item = Runtime.PyString_FromString(rtdir);
            Runtime.PyList_Append(path, item);
            Runtime.XDecref(item);
            AssemblyManager.UpdatePath();
        }

        internal static void Shutdown()
        {
            AssemblyManager.Shutdown();
            Exceptions.Shutdown();
            ImportHook.Shutdown();
            Py_Finalize();
        }

        // called *without* the GIL aquired by clr._AtExit
        internal static int AtExit()
        {
            lock (IsFinalizingLock)
            {
                IsFinalizing = true;
            }
            return 0;
        }

        internal static IntPtr Py_single_input = (IntPtr)256;
        internal static IntPtr Py_file_input = (IntPtr)257;
        internal static IntPtr Py_eval_input = (IntPtr)258;

        internal static IntPtr PyBaseObjectType;
        internal static IntPtr PyModuleType;
        internal static IntPtr PyClassType;
        internal static IntPtr PyInstanceType;
        internal static IntPtr PyCLRMetaType;
        internal static IntPtr PyMethodType;
        internal static IntPtr PyWrapperDescriptorType;

        internal static IntPtr PyUnicodeType;
        internal static IntPtr PyStringType;
        internal static IntPtr PyTupleType;
        internal static IntPtr PyListType;
        internal static IntPtr PyDictType;
        internal static IntPtr PyIntType;
        internal static IntPtr PyLongType;
        internal static IntPtr PyFloatType;
        internal static IntPtr PyBoolType;
        internal static IntPtr PyNoneType;
        internal static IntPtr PyTypeType;

#if PYTHON3
        internal static IntPtr PyBytesType;
        internal static IntPtr _PyObject_NextNotImplemented;
#endif

        internal static IntPtr PyNotImplemented;
        internal const int Py_LT = 0;
        internal const int Py_LE = 1;
        internal const int Py_EQ = 2;
        internal const int Py_NE = 3;
        internal const int Py_GT = 4;
        internal const int Py_GE = 5;

        internal static IntPtr PyTrue;
        internal static IntPtr PyFalse;
        internal static IntPtr PyNone;
        internal static IntPtr Error;

        internal static IntPtr GetBoundArgTuple(IntPtr obj, IntPtr args)
        {
            if (Runtime.PyObject_TYPE(args) != Runtime.PyTupleType)
            {
                Exceptions.SetError(Exceptions.TypeError, "tuple expected");
                return IntPtr.Zero;
            }
            int size = Runtime.PyTuple_Size(args);
            IntPtr items = Runtime.PyTuple_New(size + 1);
            Runtime.PyTuple_SetItem(items, 0, obj);
            Runtime.XIncref(obj);

            for (int i = 0; i < size; i++)
            {
                IntPtr item = Runtime.PyTuple_GetItem(args, i);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(items, i + 1, item);
            }

            return items;
        }


        internal static IntPtr ExtendTuple(IntPtr t, params IntPtr[] args)
        {
            int size = Runtime.PyTuple_Size(t);
            int add = args.Length;
            IntPtr item;

            IntPtr items = Runtime.PyTuple_New(size + add);
            for (int i = 0; i < size; i++)
            {
                item = Runtime.PyTuple_GetItem(t, i);
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(items, i, item);
            }

            for (int n = 0; n < add; n++)
            {
                item = args[n];
                Runtime.XIncref(item);
                Runtime.PyTuple_SetItem(items, size + n, item);
            }

            return items;
        }

        internal static Type[] PythonArgsToTypeArray(IntPtr arg)
        {
            return PythonArgsToTypeArray(arg, false);
        }

        internal static Type[] PythonArgsToTypeArray(IntPtr arg, bool mangleObjects)
        {
            // Given a PyObject * that is either a single type object or a
            // tuple of (managed or unmanaged) type objects, return a Type[]
            // containing the CLR Type objects that map to those types.
            IntPtr args = arg;
            bool free = false;

            if (!Runtime.PyTuple_Check(arg))
            {
                args = Runtime.PyTuple_New(1);
                Runtime.XIncref(arg);
                Runtime.PyTuple_SetItem(args, 0, arg);
                free = true;
            }

            int n = Runtime.PyTuple_Size(args);
            Type[] types = new Type[n];
            Type t = null;

            for (int i = 0; i < n; i++)
            {
                IntPtr op = Runtime.PyTuple_GetItem(args, i);
                if (mangleObjects && (!Runtime.PyType_Check(op)))
                {
                    op = Runtime.PyObject_TYPE(op);
                }
                ManagedType mt = ManagedType.GetManagedObject(op);

                if (mt is ClassBase)
                {
                    t = ((ClassBase)mt).type;
                }
                else if (mt is CLRObject)
                {
                    object inst = ((CLRObject)mt).inst;
                    if (inst is Type)
                    {
                        t = inst as Type;
                    }
                }
                else
                {
                    t = Converter.GetTypeByAlias(op);
                }

                if (t == null)
                {
                    types = null;
                    break;
                }
                types[i] = t;
            }
            if (free)
            {
                Runtime.XDecref(args);
            }
            return types;
        }

        //===================================================================
        // Managed exports of the Python C API. Where appropriate, we do
        // some optimization to avoid managed <--> unmanaged transitions
        // (mostly for heavily used methods).
        //===================================================================

        internal unsafe static void XIncref(IntPtr op)
        {
#if (Py_DEBUG)
        // according to Python doc, Py_IncRef() is Py_XINCREF()
        Py_IncRef(op);
        return;
#else
            void* p = (void*)op;
            if ((void*)0 != p)
            {
                if (is32bit)
                {
                    (*(int*)p)++;
                }
                else
                {
                    (*(long*)p)++;
                }
            }
#endif
        }

        internal static unsafe void XDecref(IntPtr op)
        {
#if (Py_DEBUG)
        // Py_DecRef calls Python's Py_DECREF
        // according to Python doc, Py_DecRef() is Py_XDECREF()
        Py_DecRef(op);
        return;
#else
            void* p = (void*)op;
            if ((void*)0 != p)
            {
                if (is32bit)
                {
                    --(*(int*)p);
                }
                else
                {
                    --(*(long*)p);
                }
                if ((*(int*)p) == 0)
                {
                    // PyObject_HEAD: struct _typeobject *ob_type
                    void* t = is32bit
                        ? (void*)(*((uint*)p + 1))
                        : (void*)(*((ulong*)p + 1));
                    // PyTypeObject: destructor tp_dealloc
                    void* f = is32bit
                        ? (void*)(*((uint*)t + 6))
                        : (void*)(*((ulong*)t + 6));
                    if ((void*)0 == f)
                    {
                        return;
                    }
                    NativeCall.Impl.Void_Call_1(new IntPtr(f), op);
                    return;
                }
            }
#endif
        }

        internal unsafe static long Refcount(IntPtr op)
        {
            void* p = (void*)op;
            if ((void*)0 != p)
            {
                if (is32bit)
                {
                    return (*(int*)p);
                }
                else
                {
                    return (*(long*)p);
                }
            }
            return 0;
        }

#if (Py_DEBUG)
    // Py_IncRef and Py_DecRef are taking care of the extra payload
    // in Py_DEBUG builds of Python like _Py_RefTotal
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    private unsafe static extern void
    Py_IncRef(IntPtr ob);

   [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    private unsafe static extern void
    Py_DecRef(IntPtr ob);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_Initialize();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            Py_IsInitialized();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_Finalize();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            Py_NewInterpreter();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_EndInterpreter(IntPtr threadState);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyThreadState_New(IntPtr istate);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyThreadState_Get();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyThread_get_key_value(IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyThread_get_thread_ident();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyThread_set_key_value(IntPtr key, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyThreadState_Swap(IntPtr key);


        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyGILState_Ensure();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyGILState_Release(IntPtr gs);


        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyGILState_GetThisThreadState();

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    public unsafe static extern int
    Py_Main(int argc, [MarshalAsAttribute(UnmanagedType.LPArray, ArraySubType=UnmanagedType.LPWStr)] string[] argv);
#elif PYTHON2
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        public unsafe static extern int
            Py_Main(int argc, string[] argv);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_InitThreads();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyEval_ThreadsInitialized();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_AcquireLock();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_ReleaseLock();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_AcquireThread(IntPtr tstate);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_ReleaseThread(IntPtr tstate);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyEval_SaveThread();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyEval_RestoreThread(IntPtr tstate);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyEval_GetBuiltins();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyEval_GetGlobals();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyEval_GetLocals();


#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
         ExactSpelling=true, CharSet=CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
        internal unsafe static extern string
        Py_GetProgramName();

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
        Py_SetProgramName([MarshalAsAttribute(UnmanagedType.LPWStr)]string name);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
        internal unsafe static extern string
        Py_GetPythonHome();

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
        Py_SetPythonHome([MarshalAsAttribute(UnmanagedType.LPWStr)]string home);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
        internal unsafe static extern string
        Py_GetPath();

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
        internal unsafe static extern void
        Py_SetPath([MarshalAsAttribute(UnmanagedType.LPWStr)]string home);
#elif PYTHON2
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetProgramName();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_SetProgramName(string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetPythonHome();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_SetPythonHome(string home);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetPath();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            Py_SetPath(string home);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetVersion();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetPlatform();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetCopyright();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetCompiler();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            Py_GetBuildInfo();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyRun_SimpleString(string code);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            Py_CompileString(string code, string file, IntPtr tok);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyInstance_New(IntPtr cls, IntPtr args, IntPtr kw);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyInstance_NewRaw(IntPtr cls, IntPtr dict);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyMethod_New(IntPtr func, IntPtr self, IntPtr cls);


        //====================================================================
        // Python abstract object API
        //====================================================================

        // A macro-like method to get the type of a Python object. This is
        // designed to be lean and mean in IL & avoid managed <-> unmanaged
        // transitions. Note that this does not incref the type object.

        internal unsafe static IntPtr
            PyObject_TYPE(IntPtr op)
        {
            void* p = (void*)op;
            if ((void*)0 == p)
            {
                return IntPtr.Zero;
            }
#if (Py_DEBUG)
        int n = 3;
#else
            int n = 1;
#endif
            if (is32bit)
            {
                return new IntPtr((void*)(*((uint*)p + n)));
            }
            else
            {
                return new IntPtr((void*)(*((ulong*)p + n)));
            }
        }

        // Managed version of the standard Python C API PyObject_Type call.
        // This version avoids a managed <-> unmanaged transition. This one
        // does incref the returned type object.

        internal unsafe static IntPtr
            PyObject_Type(IntPtr op)
        {
            IntPtr tp = PyObject_TYPE(op);
            Runtime.XIncref(tp);
            return tp;
        }

        internal static string PyObject_GetTypeName(IntPtr op)
        {
            IntPtr pyType = Marshal.ReadIntPtr(op, ObjectOffset.ob_type);
            IntPtr ppName = Marshal.ReadIntPtr(pyType, TypeOffset.tp_name);
            return Marshal.PtrToStringAnsi(ppName);
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_HasAttrString(IntPtr pointer, string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GetAttrString(IntPtr pointer, string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_HasAttr(IntPtr pointer, IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GetAttr(IntPtr pointer, IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GetIter(IntPtr op);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_CallObject(IntPtr pointer, IntPtr args);

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern int
    PyObject_RichCompareBool(IntPtr value1, IntPtr value2, int opid);

    internal static int PyObject_Compare(IntPtr value1, IntPtr value2) {
        int res;
        res = PyObject_RichCompareBool(value1, value2, Py_LT);
        if (-1 == res)
            return -1;
        else if (1 == res)
            return -1;

        res = PyObject_RichCompareBool(value1, value2, Py_EQ);
        if (-1 == res)
            return -1;
        else if (1 == res)
            return 0;

        res = PyObject_RichCompareBool(value1, value2, Py_GT);
        if (-1 == res)
            return -1;
        else if (1 == res)
            return 1;

        Exceptions.SetError(Exceptions.SystemError, "Error comparing objects");
        return -1;
    }
#elif PYTHON2
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_Compare(IntPtr value1, IntPtr value2);
#endif


        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_IsInstance(IntPtr ob, IntPtr type);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_IsSubclass(IntPtr ob, IntPtr type);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyCallable_Check(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_IsTrue(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_Not(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_Size(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Hash(IntPtr op);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Repr(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Str(IntPtr pointer);

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        EntryPoint="PyObject_Str",
        ExactSpelling = true, CharSet = CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyObject_Unicode(IntPtr pointer);
#elif PYTHON2
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Unicode(IntPtr pointer);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_Dir(IntPtr pointer);


        //====================================================================
        // Python number API
        //====================================================================

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        EntryPoint = "PyNumber_Long",
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyNumber_Int(IntPtr ob);
#elif PYTHON2

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Int(IntPtr ob);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Long(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Float(IntPtr ob);


        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern bool
            PyNumber_Check(IntPtr ob);


        internal static bool PyInt_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, Runtime.PyIntType);
        }

        internal static bool PyBool_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, Runtime.PyBoolType);
        }

        internal static IntPtr PyInt_FromInt32(int value)
        {
            IntPtr v = new IntPtr(value);
            return PyInt_FromLong(v);
        }

        internal static IntPtr PyInt_FromInt64(long value)
        {
            IntPtr v = new IntPtr(value);
            return PyInt_FromLong(v);
        }

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "PyLong_FromLong",
        ExactSpelling = true, CharSet = CharSet.Ansi)]
    private unsafe static extern IntPtr
    PyInt_FromLong(IntPtr value);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        EntryPoint = "PyLong_AsLong",
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern int
    PyInt_AsLong(IntPtr value);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        EntryPoint = "PyLong_FromString",
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyInt_FromString(string value, IntPtr end, int radix);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        EntryPoint = "PyLong_GetMax",
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern int
    PyInt_GetMax();
#elif PYTHON2

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        private unsafe static extern IntPtr
            PyInt_FromLong(IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyInt_AsLong(IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyInt_GetMax();
#endif

        internal static bool PyLong_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyLongType;
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromLong(long value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromUnsignedLong(uint value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromDouble(double value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromLongLong(long value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromUnsignedLongLong(ulong value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyLong_FromString(string value, IntPtr end, int radix);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyLong_AsLong(IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern uint
            PyLong_AsUnsignedLong(IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern long
            PyLong_AsLongLong(IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern ulong
            PyLong_AsUnsignedLongLong(IntPtr value);


        internal static bool PyFloat_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyFloatType;
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyFloat_FromDouble(double value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyFloat_FromString(IntPtr value, IntPtr junk);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern double
            PyFloat_AsDouble(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Add(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Subtract(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Multiply(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Divide(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_And(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Xor(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Or(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Lshift(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Rshift(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Power(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Remainder(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceAdd(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceSubtract(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceMultiply(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceDivide(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceAnd(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceXor(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceOr(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceLshift(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceRshift(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlacePower(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_InPlaceRemainder(IntPtr o1, IntPtr o2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Negative(IntPtr o1);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Positive(IntPtr o1);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyNumber_Invert(IntPtr o1);

        //====================================================================
        // Python sequence API
        //====================================================================

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern bool
            PySequence_Check(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_GetItem(IntPtr pointer, int index);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_DelItem(IntPtr pointer, int index);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_GetSlice(IntPtr pointer, int i1, int i2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_SetSlice(IntPtr pointer, int i1, int i2, IntPtr v);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_DelSlice(IntPtr pointer, int i1, int i2);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_Size(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_Contains(IntPtr pointer, IntPtr item);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_Concat(IntPtr pointer, IntPtr other);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_Repeat(IntPtr pointer, int count);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_Index(IntPtr pointer, IntPtr item);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySequence_Count(IntPtr pointer, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_Tuple(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySequence_List(IntPtr pointer);


        //====================================================================
        // Python string API
        //====================================================================

        internal static bool IsStringType(IntPtr op)
        {
            IntPtr t = PyObject_TYPE(op);
            return (t == PyStringType) || (t == PyUnicodeType);
        }

        internal static bool PyString_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyStringType;
        }

        internal static IntPtr PyString_FromString(string value)
        {
            return PyString_FromStringAndSize(value, value.Length);
        }

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyBytes_FromString(string op);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Ansi)]
    internal unsafe static extern int
    PyBytes_Size(IntPtr op);

    internal static IntPtr PyBytes_AS_STRING(IntPtr ob) {
        return ob + BytesOffset.ob_sval;
    }

    internal static IntPtr PyString_FromStringAndSize(string value, int length)
    {
        // copy the string into an unmanaged UTF-8 buffer
        int len = Encoding.UTF8.GetByteCount(value);
        byte[] buffer = new byte[len + 1];
        Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
        IntPtr nativeUtf8 = Marshal.AllocHGlobal(buffer.Length);
        try {
            Marshal.Copy(buffer, 0, nativeUtf8, buffer.Length);
            return PyUnicode_FromStringAndSize(nativeUtf8, length);
        }
        finally {
            Marshal.FreeHGlobal(nativeUtf8);
        }
    }

#if (PYTHON33 || PYTHON34 || PYTHON35 || PYTHON36)
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromStringAndSize(IntPtr value, int size);
#elif (UCS2)
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "PyUnicodeUCS2_FromStringAndSize",
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromStringAndSize(IntPtr value, int size);
#else
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "PyUnicodeUCS4_FromStringAndSize",
        ExactSpelling = true, CharSet = CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyUnicode_FromStringAndSize(IntPtr value, int size);
#endif

#else // Python2x

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyString_FromStringAndSize(string value, int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyString_AsString",
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyString_AS_STRING(IntPtr op);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyString_Size(IntPtr pointer);
#endif

        internal static bool PyUnicode_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyUnicodeType;
        }

#if (UCS2)
#if (PYTHON33 || PYTHON34 || PYTHON35 || PYTHON36)
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromObject(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        EntryPoint="PyUnicode_FromKindAndData",
        ExactSpelling=true,
        CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromKindAndString(int kind, string s, int size);

    internal static IntPtr PyUnicode_FromUnicode(string s, int size) {
        return PyUnicode_FromKindAndString(2, s, size);
    }

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern int
    PyUnicode_GetSize(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern char *
    PyUnicode_AsUnicode(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
           EntryPoint = "PyUnicode_AsUnicode",
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_AS_UNICODE(IntPtr op);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromOrdinal(int c);

#else
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_FromObject",
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromObject(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_FromEncodedObject",
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_FromUnicode",
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromUnicode(string s, int size);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_GetSize",
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern int
    PyUnicode_GetSize(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_AsUnicode",
        ExactSpelling=true)]
    internal unsafe static extern char *
    PyUnicode_AsUnicode(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_AsUnicode",
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_AS_UNICODE(IntPtr op);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
           EntryPoint="PyUnicodeUCS2_FromOrdinal",
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromOrdinal(int c);
#endif

    internal static IntPtr PyUnicode_FromString(string s)
    {
        return PyUnicode_FromUnicode(s, (s.Length));
    }

    internal unsafe static string GetManagedString(IntPtr op)
    {
        IntPtr type = PyObject_TYPE(op);

// Python 3 strings are all unicode
#if PYTHON2
        if (type == Runtime.PyStringType)
        {
            return Marshal.PtrToStringAnsi(
                       PyString_AS_STRING(op),
                       Runtime.PyString_Size(op)
                       );
        }
#endif

        if (type == Runtime.PyUnicodeType)
        {
            char* p = Runtime.PyUnicode_AsUnicode(op);
            int size = Runtime.PyUnicode_GetSize(op);
            return new String(p, 0, size);
        }

        return null;
    }

#endif
#if (UCS4)
#if (PYTHON33 || PYTHON34 || PYTHON35 || PYTHON36)
    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromObject(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
           EntryPoint = "PyUnicode_FromKindAndData",
        ExactSpelling = true)]
    internal unsafe static extern IntPtr
    PyUnicode_FromKindAndString(int kind,
                                [MarshalAs (UnmanagedType.CustomMarshaler,
                                 MarshalTypeRef=typeof(Utf32Marshaler))] string s,
                                int size);

    internal static IntPtr PyUnicode_FromUnicode(string s, int size) {
        return PyUnicode_FromKindAndString(4, s, size);
    }

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Ansi)]
    internal unsafe static extern int
    PyUnicode_GetSize(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true)]
    internal unsafe static extern IntPtr
    PyUnicode_AsUnicode(IntPtr ob);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
           EntryPoint = "PyUnicode_AsUnicode",
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_AS_UNICODE(IntPtr op);

    [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
        ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal unsafe static extern IntPtr
    PyUnicode_FromOrdinal(int c);

#else
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_FromObject",
            ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal unsafe static extern IntPtr
            PyUnicode_FromObject(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_FromEncodedObject",
            ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal unsafe static extern IntPtr
            PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_FromUnicode",
            ExactSpelling = true)]
        internal unsafe static extern IntPtr
            PyUnicode_FromUnicode(
            [MarshalAs(UnmanagedType.CustomMarshaler,
                MarshalTypeRef = typeof(Utf32Marshaler))] string s, int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_GetSize",
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyUnicode_GetSize(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_AsUnicode",
            ExactSpelling = true)]
        internal unsafe static extern IntPtr
            PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_AsUnicode",
            ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal unsafe static extern IntPtr
            PyUnicode_AS_UNICODE(IntPtr op);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicodeUCS4_FromOrdinal",
            ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal unsafe static extern IntPtr
            PyUnicode_FromOrdinal(int c);

#endif

        internal static IntPtr PyUnicode_FromString(string s)
        {
            return PyUnicode_FromUnicode(s, (s.Length));
        }

        internal unsafe static string GetManagedString(IntPtr op)
        {
            IntPtr type = PyObject_TYPE(op);

// Python 3 strings are all unicode
#if PYTHON2
            if (type == Runtime.PyStringType)
            {
                return Marshal.PtrToStringAnsi(
                    PyString_AS_STRING(op),
                    Runtime.PyString_Size(op)
                    );
            }
#endif

            if (type == Runtime.PyUnicodeType)
            {
                IntPtr p = Runtime.PyUnicode_AsUnicode(op);
                int length = Runtime.PyUnicode_GetSize(op);
                int size = length*4;
                byte[] buffer = new byte[size];
                Marshal.Copy(p, buffer, 0, size);
                return Encoding.UTF32.GetString(buffer, 0, size);
            }

            return null;
        }
#endif

        //====================================================================
        // Python dictionary API
        //====================================================================

        internal static bool PyDict_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyDictType;
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_New();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDictProxy_New(IntPtr dict);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_GetItemString(IntPtr pointer, string key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_SetItemString(IntPtr pointer, string key, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_DelItemString(IntPtr pointer, string key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyMapping_HasKey(IntPtr pointer, IntPtr key);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_Keys(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_Values(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_Items(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyDict_Copy(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_Update(IntPtr pointer, IntPtr other);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyDict_Clear(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyDict_Size(IntPtr pointer);


        //====================================================================
        // Python list API
        //====================================================================

        internal static bool PyList_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyListType;
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyList_New(int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyList_AsTuple(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyList_GetItem(IntPtr pointer, int index);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_Insert(IntPtr pointer, int index, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_Append(IntPtr pointer, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_Reverse(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_Sort(IntPtr pointer);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyList_GetSlice(IntPtr pointer, int start, int end);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_SetSlice(IntPtr pointer, int start, int end, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyList_Size(IntPtr pointer);


        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == Runtime.PyTupleType;
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyTuple_New(int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyTuple_GetItem(IntPtr pointer, int index);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyTuple_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyTuple_GetSlice(IntPtr pointer, int start, int end);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyTuple_Size(IntPtr pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

#if PYTHON2
        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern bool
            PyIter_Check(IntPtr pointer);
#elif PYTHON3
    internal static bool
    PyIter_Check(IntPtr pointer)
    {
        IntPtr ob_type = (IntPtr)Marshal.PtrToStructure(pointer + ObjectOffset.ob_type, typeof(IntPtr));
        IntPtr tp_iternext = ob_type + TypeOffset.tp_iternext;
        return tp_iternext != null && tp_iternext != _PyObject_NextNotImplemented;
    }
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyIter_Next(IntPtr pointer);

        //====================================================================
        // Python module API
        //====================================================================

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyModule_New(string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            PyModule_GetName(IntPtr module);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyModule_GetDict(IntPtr module);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern string
            PyModule_GetFilename(IntPtr module);

#if PYTHON3
    [DllImport(Runtime.dll, CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true, CharSet=CharSet.Ansi)]
    internal unsafe static extern IntPtr
    PyModule_Create2(IntPtr module, int apiver);
#endif

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_Import(IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_ImportModule(string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_ReloadModule(IntPtr module);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_AddModule(string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyImport_GetModuleDict();


        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PySys_SetArgv(int argc, IntPtr argv);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PySys_GetObject(string name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PySys_SetObject(string name, IntPtr ob);


        //====================================================================
        // Python type object API
        //====================================================================

        internal static bool PyType_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, Runtime.PyTypeType);
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyType_Modified(IntPtr type);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern bool
            PyType_IsSubtype(IntPtr t1, IntPtr t2);

        internal static bool PyObject_TypeCheck(IntPtr ob, IntPtr tp)
        {
            IntPtr t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyType_GenericAlloc(IntPtr type, int n);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyType_Ready(IntPtr type);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            _PyType_Lookup(IntPtr type, IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GenericGetAttr(IntPtr obj, IntPtr name);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            _PyObject_GetDictPtr(IntPtr obj);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyObject_GC_New(IntPtr tp);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyObject_GC_Del(IntPtr tp);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyObject_GC_Track(IntPtr tp);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyObject_GC_UnTrack(IntPtr tp);


        //====================================================================
        // Python memory API
        //====================================================================

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyMem_Malloc(int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyMem_Realloc(IntPtr ptr, int size);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyMem_Free(IntPtr ptr);


        //====================================================================
        // Python exception API
        //====================================================================

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_SetString(IntPtr ob, string message);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_SetObject(IntPtr ob, IntPtr message);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyErr_SetFromErrno(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_SetNone(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyErr_ExceptionMatches(IntPtr exception);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_NormalizeException(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern int
            PyErr_Occurred();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_Fetch(ref IntPtr ob, ref IntPtr val, ref IntPtr tb);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_Clear();

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern void
            PyErr_Print();


        //====================================================================
        // Miscellaneous
        //====================================================================

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyMethod_Self(IntPtr ob);

        [DllImport(Runtime.dll, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal unsafe static extern IntPtr
            PyMethod_Function(IntPtr ob);
    }
}
