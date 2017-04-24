using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Python.Runtime
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
#if MONO_LINUX || MONO_OSX
        private static int RTLD_NOW = 0x2;
        private static int RTLD_SHARED = 0x20;
#if MONO_OSX
        private static IntPtr RTLD_DEFAULT = new IntPtr(-2);
        private const string NativeDll = "__Internal";
#elif MONO_LINUX
        private static IntPtr RTLD_DEFAULT = IntPtr.Zero;
        private const string NativeDll = "libdl.so";
#endif

        public static IntPtr LoadLibrary(string fileName)
        {
            return dlopen(fileName, RTLD_NOW | RTLD_SHARED);
        }

        public static void FreeLibrary(IntPtr handle)
        {
            dlclose(handle);
        }

        public static IntPtr GetProcAddress(IntPtr dllHandle, string name)
        {
            // look in the exe if dllHandle is NULL
            if (dllHandle == IntPtr.Zero)
            {
                dllHandle = RTLD_DEFAULT;
            }

            // clear previous errors if any
            dlerror();
            IntPtr res = dlsym(dllHandle, name);
            IntPtr errPtr = dlerror();
            if (errPtr != IntPtr.Zero)
            {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
        }

        [DllImport(NativeDll)]
        private static extern IntPtr dlopen(String fileName, int flags);

        [DllImport(NativeDll)]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport(NativeDll)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll)]
        private static extern IntPtr dlerror();
#else // Windows
        private const string NativeDll = "kernel32.dll";

        [DllImport(NativeDll)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport(NativeDll)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport(NativeDll)]
        public static extern bool FreeLibrary(IntPtr hModule);
#endif
    }

    /// <summary>
    /// Encapsulates the low-level Python C API. Note that it is
    /// the responsibility of the caller to have acquired the GIL
    /// before calling any of these methods.
    /// </summary>
    public class Runtime
    {
#if UCS4
        public const int UCS = 4;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS4_";
#elif UCS2
        public const int UCS = 2;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS2_";
#else
#error You must define either UCS2 or UCS4!
#endif

#if PYTHON27
        public const string pyversion = "2.7";
        public const string pyver = "27";
#elif PYTHON33
        public const string pyversion = "3.3";
        public const string pyver = "33";
#elif PYTHON34
        public const string pyversion = "3.4";
        public const string pyver = "34";
#elif PYTHON35
        public const string pyversion = "3.5";
        public const string pyver = "35";
#elif PYTHON36
        public const string pyversion = "3.6";
        public const string pyver = "36";
#elif PYTHON37 // TODO: Add `interop37.cs` after PY37 is released
        public const string pyversion = "3.7";
        public const string pyver = "37";
#else
#error You must define one of PYTHON33 to PYTHON37 or PYTHON27
#endif

#if MONO_LINUX || MONO_OSX // Linux/macOS use dotted version string
        internal const string dllBase = "python" + pyversion;
#else // Windows
        internal const string dllBase = "python" + pyver;
#endif

#if PYTHON_WITH_PYDEBUG
        internal const string dllWithPyDebug = "d";
#else
        internal const string dllWithPyDebug = "";
#endif
#if PYTHON_WITH_PYMALLOC
        internal const string dllWithPyMalloc = "m";
#else
        internal const string dllWithPyMalloc = "";
#endif

#if PYTHON_WITHOUT_ENABLE_SHARED
        public const string PythonDll = "__Internal";
#else
        public const string PythonDll = dllBase + dllWithPyDebug + dllWithPyMalloc;
#endif

        public static readonly int pyversionnumber = Convert.ToInt32(pyver);

        // set to true when python is finalizing
        internal static object IsFinalizingLock = new object();
        internal static bool IsFinalizing;

        internal static bool Is32Bit = IntPtr.Size == 4;
        internal static bool IsPython2 = pyversionnumber < 30;
        internal static bool IsPython3 = pyversionnumber >= 30;

        /// <summary>
        /// Encoding to use to convert Unicode to/from Managed to Native
        /// </summary>
        internal static readonly Encoding PyEncoding = UCS == 2 ? Encoding.Unicode : Encoding.UTF32;

        /// <summary>
        /// Initialize the runtime...
        /// </summary>
        internal static void Initialize()
        {
            if (Py_IsInitialized() == 0)
            {
                Py_Initialize();
            }

            if (PyEval_ThreadsInitialized() == 0)
            {
                PyEval_InitThreads();
            }

            IntPtr op;
            IntPtr dict;
            if (IsPython3)
            {
                op = PyImport_ImportModule("builtins");
                dict = PyObject_GetAttrString(op, "__dict__");
            }
            else // Python2
            {
                dict = PyImport_GetModuleDict();
                op = PyDict_GetItemString(dict, "__builtin__");
            }
            PyNotImplemented = PyObject_GetAttrString(op, "NotImplemented");
            PyBaseObjectType = PyObject_GetAttrString(op, "object");

            PyModuleType = PyObject_Type(op);
            PyNone = PyObject_GetAttrString(op, "None");
            PyTrue = PyObject_GetAttrString(op, "True");
            PyFalse = PyObject_GetAttrString(op, "False");

            PyBoolType = PyObject_Type(PyTrue);
            PyNoneType = PyObject_Type(PyNone);
            PyTypeType = PyObject_Type(PyNoneType);

            op = PyObject_GetAttrString(dict, "keys");
            PyMethodType = PyObject_Type(op);
            XDecref(op);

            // For some arcane reason, builtins.__dict__.__setitem__ is *not*
            // a wrapper_descriptor, even though dict.__setitem__ is.
            //
            // object.__init__ seems safe, though.
            op = PyObject_GetAttrString(PyBaseObjectType, "__init__");
            PyWrapperDescriptorType = PyObject_Type(op);
            XDecref(op);

#if PYTHON3
            XDecref(dict);
#endif

            op = PyString_FromString("string");
            PyStringType = PyObject_Type(op);
            XDecref(op);

            op = PyUnicode_FromString("unicode");
            PyUnicodeType = PyObject_Type(op);
            XDecref(op);

#if PYTHON3
            op = PyBytes_FromString("bytes");
            PyBytesType = PyObject_Type(op);
            XDecref(op);
#endif

            op = PyTuple_New(0);
            PyTupleType = PyObject_Type(op);
            XDecref(op);

            op = PyList_New(0);
            PyListType = PyObject_Type(op);
            XDecref(op);

            op = PyDict_New();
            PyDictType = PyObject_Type(op);
            XDecref(op);

            op = PyInt_FromInt32(0);
            PyIntType = PyObject_Type(op);
            XDecref(op);

            op = PyLong_FromLong(0);
            PyLongType = PyObject_Type(op);
            XDecref(op);

            op = PyFloat_FromDouble(0);
            PyFloatType = PyObject_Type(op);
            XDecref(op);

#if PYTHON3
            PyClassType = IntPtr.Zero;
            PyInstanceType = IntPtr.Zero;
#elif PYTHON2
            IntPtr s = PyString_FromString("_temp");
            IntPtr d = PyDict_New();

            IntPtr c = PyClass_New(IntPtr.Zero, d, s);
            PyClassType = PyObject_Type(c);

            IntPtr i = PyInstance_New(c, IntPtr.Zero, IntPtr.Zero);
            PyInstanceType = PyObject_Type(i);

            XDecref(s);
            XDecref(i);
            XDecref(c);
            XDecref(d);
#endif

            Error = new IntPtr(-1);

#if PYTHON3
            IntPtr dllLocal = IntPtr.Zero;
            if (PythonDll != "__Internal")
            {
                NativeMethods.LoadLibrary(PythonDll);
            }
            _PyObject_NextNotImplemented = NativeMethods.GetProcAddress(dllLocal, "_PyObject_NextNotImplemented");
#if !(MONO_LINUX || MONO_OSX)
            if (dllLocal != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(dllLocal);
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
            IntPtr path = PySys_GetObject("path");
            IntPtr item = PyString_FromString(rtdir);
            PyList_Append(path, item);
            XDecref(item);
            AssemblyManager.UpdatePath();
        }

        internal static void Shutdown()
        {
            AssemblyManager.Shutdown();
            Exceptions.Shutdown();
            ImportHook.Shutdown();
            Py_Finalize();
        }

        // called *without* the GIL acquired by clr._AtExit
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

        /// <summary>
        /// Check if any Python Exceptions occurred.
        /// If any exist throw new PythonException.
        /// </summary>
        /// <remarks>
        /// Can be used instead of `obj == IntPtr.Zero` for example.
        /// </remarks>
        internal static void CheckExceptionOccurred()
        {
            if (PyErr_Occurred() != 0)
            {
                throw new PythonException();
            }
        }

        internal static IntPtr GetBoundArgTuple(IntPtr obj, IntPtr args)
        {
            if (PyObject_TYPE(args) != PyTupleType)
            {
                Exceptions.SetError(Exceptions.TypeError, "tuple expected");
                return IntPtr.Zero;
            }
            int size = PyTuple_Size(args);
            IntPtr items = PyTuple_New(size + 1);
            PyTuple_SetItem(items, 0, obj);
            XIncref(obj);

            for (var i = 0; i < size; i++)
            {
                IntPtr item = PyTuple_GetItem(args, i);
                XIncref(item);
                PyTuple_SetItem(items, i + 1, item);
            }

            return items;
        }


        internal static IntPtr ExtendTuple(IntPtr t, params IntPtr[] args)
        {
            int size = PyTuple_Size(t);
            int add = args.Length;
            IntPtr item;

            IntPtr items = PyTuple_New(size + add);
            for (var i = 0; i < size; i++)
            {
                item = PyTuple_GetItem(t, i);
                XIncref(item);
                PyTuple_SetItem(items, i, item);
            }

            for (var n = 0; n < add; n++)
            {
                item = args[n];
                XIncref(item);
                PyTuple_SetItem(items, size + n, item);
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
            var free = false;

            if (!PyTuple_Check(arg))
            {
                args = PyTuple_New(1);
                XIncref(arg);
                PyTuple_SetItem(args, 0, arg);
                free = true;
            }

            int n = PyTuple_Size(args);
            var types = new Type[n];
            Type t = null;

            for (var i = 0; i < n; i++)
            {
                IntPtr op = PyTuple_GetItem(args, i);
                if (mangleObjects && (!PyType_Check(op)))
                {
                    op = PyObject_TYPE(op);
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
                XDecref(args);
            }
            return types;
        }

        /// <summary>
        /// Managed exports of the Python C API. Where appropriate, we do
        /// some optimization to avoid managed &lt;--&gt; unmanaged transitions
        /// (mostly for heavily used methods).
        /// </summary>
        internal static unsafe void XIncref(IntPtr op)
        {
#if PYTHON_WITH_PYDEBUG
            Py_IncRef(op);
            return;
#else
            var p = (void*)op;
            if ((void*)0 != p)
            {
                if (Is32Bit)
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
#if PYTHON_WITH_PYDEBUG
            Py_DecRef(op);
            return;
#else
            var p = (void*)op;
            if ((void*)0 != p)
            {
                if (Is32Bit)
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
                    void* t = Is32Bit
                        ? (void*)(*((uint*)p + 1))
                        : (void*)(*((ulong*)p + 1));
                    // PyTypeObject: destructor tp_dealloc
                    void* f = Is32Bit
                        ? (void*)(*((uint*)t + 6))
                        : (void*)(*((ulong*)t + 6));
                    if ((void*)0 == f)
                    {
                        return;
                    }
                    NativeCall.Impl.Void_Call_1(new IntPtr(f), op);
                }
            }
#endif
        }

        internal static unsafe long Refcount(IntPtr op)
        {
            var p = (void*)op;
            if ((void*)0 == p)
            {
                return 0;
            }
            return Is32Bit ? (*(int*)p) : (*(long*)p);
        }

        /// <summary>
        /// Export of Macro Py_XIncRef. Use XIncref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_IncRef(IntPtr ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_DecRef(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Initialize();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_IsInitialized();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Finalize();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_NewInterpreter();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_EndInterpreter(IntPtr threadState);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_New(IntPtr istate);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Get();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThread_get_key_value(IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_get_thread_ident();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_set_key_value(IntPtr key, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Swap(IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_Ensure();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyGILState_Release(IntPtr gs);


        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_GetThisThreadState();

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv
        );
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(int argc, string[] argv);
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_InitThreads();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyEval_ThreadsInitialized();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireLock();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseLock();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireThread(IntPtr tstate);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseThread(IntPtr tstate);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_SaveThread();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_RestoreThread(IntPtr tstate);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetBuiltins();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetGlobals();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetLocals();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetProgramName();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetProgramName(IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPythonHome();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPythonHome(IntPtr home);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPath();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPath(IntPtr home);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetVersion();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPlatform();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCopyright();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCompiler();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetBuildInfo();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyRun_SimpleString(string code);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_CompileString(string code, string file, IntPtr tok);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_New(IntPtr cls, IntPtr args, IntPtr kw);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_NewRaw(IntPtr cls, IntPtr dict);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_New(IntPtr func, IntPtr self, IntPtr cls);


        //====================================================================
        // Python abstract object API
        //====================================================================

        /// <summary>
        /// A macro-like method to get the type of a Python object. This is
        /// designed to be lean and mean in IL &amp; avoid managed &lt;-&gt; unmanaged
        /// transitions. Note that this does not incref the type object.
        /// </summary>
        internal static unsafe IntPtr PyObject_TYPE(IntPtr op)
        {
            var p = (void*)op;
            if ((void*)0 == p)
            {
                return IntPtr.Zero;
            }
#if PYTHON_WITH_PYDEBUG
            var n = 3;
#else
            var n = 1;
#endif
            return Is32Bit
                ? new IntPtr((void*)(*((uint*)p + n)))
                : new IntPtr((void*)(*((ulong*)p + n)));
        }

        /// <summary>
        /// Managed version of the standard Python C API PyObject_Type call.
        /// This version avoids a managed  &lt;-&gt; unmanaged transition.
        /// This one does incref the returned type object.
        /// </summary>
        internal static IntPtr PyObject_Type(IntPtr op)
        {
            IntPtr tp = PyObject_TYPE(op);
            XIncref(tp);
            return tp;
        }

        internal static string PyObject_GetTypeName(IntPtr op)
        {
            IntPtr pyType = Marshal.ReadIntPtr(op, ObjectOffset.ob_type);
            IntPtr ppName = Marshal.ReadIntPtr(pyType, TypeOffset.tp_name);
            return Marshal.PtrToStringAnsi(ppName);
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttrString(IntPtr pointer, string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttrString(IntPtr pointer, string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttr(IntPtr pointer, IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttr(IntPtr pointer, IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetIter(IntPtr op);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_CallObject(IntPtr pointer, IntPtr args);

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_RichCompareBool(IntPtr value1, IntPtr value2, int opid);

        internal static int PyObject_Compare(IntPtr value1, IntPtr value2)
        {
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
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Compare(IntPtr value1, IntPtr value2);
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsInstance(IntPtr ob, IntPtr type);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsSubclass(IntPtr ob, IntPtr type);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCallable_Check(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsTrue(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Not(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Size(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Hash(IntPtr op);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Repr(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Str(IntPtr pointer);

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyObject_Str")]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Dir(IntPtr pointer);


        //====================================================================
        // Python number API
        //====================================================================

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyNumber_Long")]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Long(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Float(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyNumber_Check(IntPtr ob);

        internal static bool PyInt_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, PyIntType);
        }

        internal static bool PyBool_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, PyBoolType);
        }

        internal static IntPtr PyInt_FromInt32(int value)
        {
            var v = new IntPtr(value);
            return PyInt_FromLong(v);
        }

        internal static IntPtr PyInt_FromInt64(long value)
        {
            var v = new IntPtr(value);
            return PyInt_FromLong(v);
        }

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromLong")]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsLong")]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromString")]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_GetMax")]
        internal static extern int PyInt_GetMax();
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_GetMax();
#endif

        internal static bool PyLong_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLong(long value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromUnsignedLong(uint value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromDouble(double value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLongLong(long value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromUnsignedLongLong(ulong value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromString(string value, IntPtr end, int radix);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyLong_AsLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint PyLong_AsUnsignedLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long PyLong_AsLongLong(IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong PyLong_AsUnsignedLongLong(IntPtr value);

        internal static bool PyFloat_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyFloatType;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromDouble(double value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromString(IntPtr value, IntPtr junk);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double PyFloat_AsDouble(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Add(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Subtract(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Multiply(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Divide(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_And(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Xor(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Or(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Lshift(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Rshift(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Power(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Remainder(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAdd(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceSubtract(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceMultiply(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceDivide(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAnd(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceXor(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceOr(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceLshift(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRshift(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlacePower(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRemainder(IntPtr o1, IntPtr o2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Negative(IntPtr o1);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Positive(IntPtr o1);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Invert(IntPtr o1);


        //====================================================================
        // Python sequence API
        //====================================================================

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PySequence_Check(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_GetItem(IntPtr pointer, int index);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_DelItem(IntPtr pointer, int index);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_GetSlice(IntPtr pointer, int i1, int i2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_SetSlice(IntPtr pointer, int i1, int i2, IntPtr v);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_DelSlice(IntPtr pointer, int i1, int i2);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Size(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Contains(IntPtr pointer, IntPtr item);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Concat(IntPtr pointer, IntPtr other);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Repeat(IntPtr pointer, int count);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Index(IntPtr pointer, IntPtr item);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Count(IntPtr pointer, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Tuple(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_List(IntPtr pointer);


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
            return PyObject_TYPE(ob) == PyStringType;
        }

        internal static IntPtr PyString_FromString(string value)
        {
            return PyString_FromStringAndSize(value, value.Length);
        }

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyBytes_FromString(string op);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyBytes_Size(IntPtr op);

        internal static IntPtr PyBytes_AS_STRING(IntPtr ob)
        {
            return ob + BytesOffset.ob_sval;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicode_FromStringAndSize")]
        internal static extern IntPtr PyString_FromStringAndSize(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))] string value,
            int size
        );

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromStringAndSize(IntPtr value, int size);
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyString_FromStringAndSize(string value, int size);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyString_AsString(IntPtr op);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyString_Size(IntPtr pointer);
#endif

        internal static bool PyUnicode_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyUnicodeType;
        }

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromKindAndData(
            int kind,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            int size
        );

        internal static IntPtr PyUnicode_FromUnicode(string s, int size)
        {
            return PyUnicode_FromKindAndData(UCS, s, size);
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyUnicode_GetSize(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromOrdinal(int c);
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromObject")]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromEncodedObject")]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromUnicode")]
        internal static extern IntPtr PyUnicode_FromUnicode(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            int size
        );

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "GetSize")]
        internal static extern int PyUnicode_GetSize(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "AsUnicode")]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromOrdinal")]
        internal static extern IntPtr PyUnicode_FromOrdinal(int c);
#endif

        internal static IntPtr PyUnicode_FromString(string s)
        {
            return PyUnicode_FromUnicode(s, s.Length);
        }

        /// <summary>
        /// Function to access the internal PyUnicode/PyString object and
        /// convert it to a managed string with the correct encoding.
        /// </summary>
        /// <remarks>
        /// We can't easily do this through through the CustomMarshaler's on
        /// the returns because will have access to the IntPtr but not size.
        /// <para />
        /// For PyUnicodeType, we can't convert with Marshal.PtrToStringUni
        /// since it only works for UCS2.
        /// </remarks>
        /// <param name="op">PyStringType or PyUnicodeType object to convert</param>
        /// <returns>Managed String</returns>
        internal static string GetManagedString(IntPtr op)
        {
            IntPtr type = PyObject_TYPE(op);

#if PYTHON2 // Python 3 strings are all Unicode
            if (type == PyStringType)
            {
                return Marshal.PtrToStringAnsi(PyString_AsString(op), PyString_Size(op));
            }
#endif

            if (type == PyUnicodeType)
            {
                IntPtr p = PyUnicode_AsUnicode(op);
                int length = PyUnicode_GetSize(op);

                int size = length * UCS;
                var buffer = new byte[size];
                Marshal.Copy(p, buffer, 0, size);
                return PyEncoding.GetString(buffer, 0, size);
            }

            return null;
        }


        //====================================================================
        // Python dictionary API
        //====================================================================

        internal static bool PyDict_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyDictType;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_New();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDictProxy_New(IntPtr dict);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItemString(IntPtr pointer, string key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItemString(IntPtr pointer, string key, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItemString(IntPtr pointer, string key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyMapping_HasKey(IntPtr pointer, IntPtr key);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Keys(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Values(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Items(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Copy(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Update(IntPtr pointer, IntPtr other);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyDict_Clear(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Size(IntPtr pointer);


        //====================================================================
        // Python list API
        //====================================================================

        internal static bool PyList_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyListType;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_New(int size);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_AsTuple(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_GetItem(IntPtr pointer, int index);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Insert(IntPtr pointer, int index, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Append(IntPtr pointer, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Reverse(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Sort(IntPtr pointer);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_GetSlice(IntPtr pointer, int start, int end);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_SetSlice(IntPtr pointer, int start, int end, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Size(IntPtr pointer);


        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyTupleType;
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyTuple_New(int size);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyTuple_GetItem(IntPtr pointer, int index);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyTuple_SetItem(IntPtr pointer, int index, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyTuple_GetSlice(IntPtr pointer, int start, int end);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyTuple_Size(IntPtr pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

#if PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyIter_Check(IntPtr pointer);
#elif PYTHON3
        internal static bool PyIter_Check(IntPtr pointer)
        {
            var ob_type = (IntPtr)Marshal.PtrToStructure(pointer + ObjectOffset.ob_type, typeof(IntPtr));
            IntPtr tp_iternext = ob_type + TypeOffset.tp_iternext;
            return tp_iternext != null && tp_iternext != _PyObject_NextNotImplemented;
        }
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyIter_Next(IntPtr pointer);


        //====================================================================
        // Python module API
        //====================================================================

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_New(string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetName(IntPtr module);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_GetDict(IntPtr module);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetFilename(IntPtr module);

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_Create2(IntPtr module, int apiver);
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_Import(IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ImportModule(string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ReloadModule(IntPtr module);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_AddModule(string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_GetModuleDict();

#if PYTHON3
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv,
            int updatepath
        );
#elif PYTHON2
        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            string[] argv,
            int updatepath
        );
#endif

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySys_GetObject(string name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySys_SetObject(string name, IntPtr ob);


        //====================================================================
        // Python type object API
        //====================================================================

        internal static bool PyType_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, PyTypeType);
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyType_Modified(IntPtr type);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyType_IsSubtype(IntPtr t1, IntPtr t2);

        internal static bool PyObject_TypeCheck(IntPtr ob, IntPtr tp)
        {
            IntPtr t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyType_GenericAlloc(IntPtr type, int n);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyType_Ready(IntPtr type);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyType_Lookup(IntPtr type, IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GenericGetAttr(IntPtr obj, IntPtr name);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyObject_GetDictPtr(IntPtr obj);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GC_New(IntPtr tp);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Del(IntPtr tp);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Track(IntPtr tp);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_UnTrack(IntPtr tp);


        //====================================================================
        // Python memory API
        //====================================================================

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMem_Malloc(int size);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMem_Realloc(IntPtr ptr, int size);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyMem_Free(IntPtr ptr);


        //====================================================================
        // Python exception API
        //====================================================================

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetString(IntPtr ob, string message);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetObject(IntPtr ob, IntPtr message);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyErr_SetFromErrno(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetNone(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_ExceptionMatches(IntPtr exception);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_NormalizeException(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_Occurred();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Fetch(ref IntPtr ob, ref IntPtr val, ref IntPtr tb);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Clear();

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Print();


        //====================================================================
        // Miscellaneous
        //====================================================================

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Self(IntPtr ob);

        [DllImport(PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Function(IntPtr ob);
    }
}
