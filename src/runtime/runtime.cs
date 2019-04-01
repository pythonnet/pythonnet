using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace Python.Runtime
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
#if MONO_LINUX || MONO_OSX
#if NETSTANDARD
        private static int RTLD_NOW = 0x2;
#if MONO_LINUX
        private static int RTLD_GLOBAL = 0x100;
        private static IntPtr RTLD_DEFAULT = IntPtr.Zero;
        private const string NativeDll = "libdl.so";
        public static IntPtr LoadLibrary(string fileName)
        {
            return dlopen($"lib{fileName}.so", RTLD_NOW | RTLD_GLOBAL);
        }
#elif MONO_OSX
        private static int RTLD_GLOBAL = 0x8;
        private const string NativeDll = "/usr/lib/libSystem.dylib";
        private static IntPtr RTLD_DEFAULT = new IntPtr(-2);

        public static IntPtr LoadLibrary(string fileName)
        {
            return dlopen($"lib{fileName}.dylib", RTLD_NOW | RTLD_GLOBAL);
        }
#endif
#else
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
#endif


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

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr dlopen(String fileName, int flags);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);

        [DllImport(NativeDll, CallingConvention = CallingConvention.Cdecl)]
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
        // C# compiler copies constants to the assemblies that references this library.
        // We needs to replace all public constants to static readonly fields to allow
        // binary substitution of different Python.Runtime.dll builds in a target application.

        public static int UCS => _UCS;

#if UCS4
        internal const int _UCS = 4;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS4_";
#elif UCS2
        internal const int _UCS = 2;

        /// <summary>
        /// EntryPoint to be used in DllImport to map to correct Unicode
        /// methods prior to PEP393. Only used for PY27.
        /// </summary>
        private const string PyUnicodeEntryPoint = "PyUnicodeUCS2_";
#else
#error You must define either UCS2 or UCS4!
#endif

        // C# compiler copies constants to the assemblies that references this library.
        // We needs to replace all public constants to static readonly fields to allow
        // binary substitution of different Python.Runtime.dll builds in a target application.

        public static string pyversion => _pyversion;
        public static string pyver => _pyver;

#if PYTHON27
        internal const string _pyversion = "2.7";
        internal const string _pyver = "27";
#elif PYTHON34
        internal const string _pyversion = "3.4";
        internal const string _pyver = "34";
#elif PYTHON35
        internal const string _pyversion = "3.5";
        internal const string _pyver = "35";
#elif PYTHON36
        internal const string _pyversion = "3.6";
        internal const string _pyver = "36";
#elif PYTHON37
        internal const string _pyversion = "3.7";
        internal const string _pyver = "37";
#else
#error You must define one of PYTHON34 to PYTHON37 or PYTHON27
#endif

#if MONO_LINUX || MONO_OSX // Linux/macOS use dotted version string
        internal const string dllBase = "python" + _pyversion;
#else // Windows
        internal const string dllBase = "python" + _pyver;
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

        // C# compiler copies constants to the assemblies that references this library.
        // We needs to replace all public constants to static readonly fields to allow
        // binary substitution of different Python.Runtime.dll builds in a target application.

        public static readonly string PythonDLL = _PythonDll;

#if PYTHON_WITHOUT_ENABLE_SHARED && !NETSTANDARD
        internal const string _PythonDll = "__Internal";
#else
        internal const string _PythonDll = dllBase + dllWithPyDebug + dllWithPyMalloc;
#endif

        public static readonly int pyversionnumber = Convert.ToInt32(_pyver);

        // set to true when python is finalizing
        internal static object IsFinalizingLock = new object();
        internal static bool IsFinalizing;

        internal static bool Is32Bit = IntPtr.Size == 4;

        // .NET core: System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        internal static bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        /// Operating system type as reported by Python.
        /// </summary>
        public enum OperatingSystemType
        {
            Windows,
            Darwin,
            Linux,
            Other
        }

        static readonly Dictionary<string, OperatingSystemType> OperatingSystemTypeMapping = new Dictionary<string, OperatingSystemType>()
        {
            { "Windows", OperatingSystemType.Windows },
            { "Darwin", OperatingSystemType.Darwin },
            { "Linux", OperatingSystemType.Linux },
        };

        /// <summary>
        /// Gets the operating system as reported by python's platform.system().
        /// </summary>
        public static OperatingSystemType OperatingSystem { get; private set; }

        /// <summary>
        /// Gets the operating system as reported by python's platform.system().
        /// </summary>
        public static string OperatingSystemName { get; private set; }

        public enum MachineType
        {
            i386,
            x86_64,
            Other
        };

        /// <summary>
        /// Map lower-case version of the python machine name to the processor
        /// type. There are aliases, e.g. x86_64 and amd64 are two names for
        /// the same thing. Make sure to lower-case the search string, because
        /// capitalization can differ.
        /// </summary>
        static readonly Dictionary<string, MachineType> MachineTypeMapping = new Dictionary<string, MachineType>()
        {
            ["i386"] = MachineType.i386,
            ["i686"] = MachineType.i386,
            ["x86"] = MachineType.i386,
            ["x86_64"] = MachineType.x86_64,
            ["amd64"] = MachineType.x86_64,
            ["x64"] = MachineType.x86_64,
            ["em64t"] = MachineType.x86_64,
        };

        /// <summary>
        /// Gets the machine architecture as reported by python's platform.machine().
        /// </summary>
        public static MachineType Machine { get; private set; }/* set in Initialize using python's platform.machine */

        /// <summary>
        /// Gets the machine architecture as reported by python's platform.machine().
        /// </summary>
        public static string MachineName { get; private set; }

        internal static bool IsPython2 = pyversionnumber < 30;
        internal static bool IsPython3 = pyversionnumber >= 30;

        public static int MainManagedThreadId { get; private set; }

        /// <summary>
        /// Encoding to use to convert Unicode to/from Managed to Native
        /// </summary>
        internal static readonly Encoding PyEncoding = _UCS == 2 ? Encoding.Unicode : Encoding.UTF32;

        /// <summary>
        /// Initialize the runtime...
        /// </summary>
        internal static void Initialize(bool initSigs = false)
        {
            if (Py_IsInitialized() == 0)
            {
                Py_InitializeEx(initSigs ? 1 : 0);
                MainManagedThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            if (PyEval_ThreadsInitialized() == 0)
            {
                PyEval_InitThreads();
            }

            IsFinalizing = false;

            CLRModule.Reset();
            GenericUtil.Reset();
            PyScopeManager.Reset();
            ClassManager.Reset();
            ClassDerivedObject.Reset();
            TypeManager.Reset();

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

            IntPtr dllLocal = IntPtr.Zero;

            if (_PythonDll != "__Internal")
            {
                dllLocal = NativeMethods.LoadLibrary(_PythonDll);
            }
            _PyObject_NextNotImplemented = NativeMethods.GetProcAddress(dllLocal, "_PyObject_NextNotImplemented");

#if !(MONO_LINUX || MONO_OSX)
            if (dllLocal != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(dllLocal);
            }
#endif
            // Initialize data about the platform we're running on. We need
            // this for the type manager and potentially other details. Must
            // happen after caching the python types, above.
            InitializePlatformData();

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

        /// <summary>
        /// Initializes the data about platforms.
        ///
        /// This must be the last step when initializing the runtime:
        /// GetManagedString needs to have the cached values for types.
        /// But it must run before initializing anything outside the runtime
        /// because those rely on the platform data.
        /// </summary>
        private static void InitializePlatformData()
        {
            IntPtr op;
            IntPtr fn;
            IntPtr platformModule = PyImport_ImportModule("platform");
            IntPtr emptyTuple = PyTuple_New(0);

            fn = PyObject_GetAttrString(platformModule, "system");
            op = PyObject_Call(fn, emptyTuple, IntPtr.Zero);
            OperatingSystemName = GetManagedString(op);
            XDecref(op);
            XDecref(fn);

            fn = PyObject_GetAttrString(platformModule, "machine");
            op = PyObject_Call(fn, emptyTuple, IntPtr.Zero);
            MachineName = GetManagedString(op);
            XDecref(op);
            XDecref(fn);

            XDecref(emptyTuple);
            XDecref(platformModule);

            // Now convert the strings into enum values so we can do switch
            // statements rather than constant parsing.
            OperatingSystemType OSType;
            if (!OperatingSystemTypeMapping.TryGetValue(OperatingSystemName, out OSType))
            {
                OSType = OperatingSystemType.Other;
            }
            OperatingSystem = OSType;

            MachineType MType;
            if (!MachineTypeMapping.TryGetValue(MachineName.ToLower(), out MType))
            {
                MType = MachineType.Other;
            }
            Machine = MType;
        }

        internal static void Shutdown()
        {
            AssemblyManager.Shutdown();
            Exceptions.Shutdown();
            ImportHook.Shutdown();
            Finalizer.Shutdown();
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
#endif
        internal static IntPtr _PyObject_NextNotImplemented;

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
            if (PyErr_Occurred() != IntPtr.Zero)
            {
                throw new PythonException();
            }
        }

        internal static IntPtr ExtendTuple(IntPtr t, params IntPtr[] args)
        {
            var size = PyTuple_Size(t);
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

            var n = PyTuple_Size(args);
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
#if PYTHON_WITH_PYDEBUG || NETSTANDARD
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
#if PYTHON_WITH_PYDEBUG || NETSTANDARD
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
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_IncRef(IntPtr ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_DecRef(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Initialize();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_InitializeEx(int initsigs);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_IsInitialized();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_Finalize();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_NewInterpreter();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_EndInterpreter(IntPtr threadState);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_New(IntPtr istate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Get();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThread_get_key_value(IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_get_thread_ident();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyThread_set_key_value(IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyThreadState_Swap(IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_Ensure();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyGILState_Release(IntPtr gs);


        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGILState_GetThisThreadState();

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv
        );
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(int argc, string[] argv);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_InitThreads();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyEval_ThreadsInitialized();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireLock();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseLock();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_AcquireThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_ReleaseThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_SaveThread();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyEval_RestoreThread(IntPtr tstate);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetBuiltins();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetGlobals();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_GetLocals();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetProgramName();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetProgramName(IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPythonHome();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPythonHome(IntPtr home);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPath();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_SetPath(IntPtr home);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetVersion();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetPlatform();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCopyright();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetCompiler();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_GetBuildInfo();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyRun_SimpleString(string code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_CompileString(string code, string file, IntPtr tok);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_New(IntPtr cls, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInstance_NewRaw(IntPtr cls, IntPtr dict);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
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

        /// <summary>
        /// Test whether the Python object is an iterable.
        /// </summary>
        internal static bool PyObject_IsIterable(IntPtr pointer)
        {
            var ob_type = Marshal.ReadIntPtr(pointer, ObjectOffset.ob_type);
#if PYTHON2
            long tp_flags = Util.ReadCLong(ob_type, TypeOffset.tp_flags);
            if ((tp_flags & TypeFlags.HaveIter) == 0)
                return false;
#endif
            IntPtr tp_iter = Marshal.ReadIntPtr(ob_type, TypeOffset.tp_iter);
            return tp_iter != IntPtr.Zero;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttr(IntPtr pointer, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttr(IntPtr pointer, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetIter(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_CallObject(IntPtr pointer, IntPtr args);

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
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
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Compare(IntPtr value1, IntPtr value2);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsInstance(IntPtr ob, IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsSubclass(IntPtr ob, IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCallable_Check(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsTrue(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Not(IntPtr pointer);

        internal static long PyObject_Size(IntPtr pointer)
        {
            return (long) _PyObject_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyObject_Size")]
        private static extern IntPtr _PyObject_Size(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Hash(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Repr(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Str(IntPtr pointer);

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyObject_Str")]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Dir(IntPtr pointer);


        //====================================================================
        // Python number API
        //====================================================================

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyNumber_Long")]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Long(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Float(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
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
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromLong")]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsLong")]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromString")]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_GetMax")]
        internal static extern int PyInt_GetMax();
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyInt_GetMax();
#endif

        internal static bool PyLong_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromUnsignedLong(uint value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLongLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromUnsignedLongLong(ulong value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyLong_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint PyLong_AsUnsignedLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long PyLong_AsLongLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong PyLong_AsUnsignedLongLong(IntPtr value);

        internal static bool PyFloat_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyFloatType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyFloat_FromString(IntPtr value, IntPtr junk);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double PyFloat_AsDouble(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Add(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Subtract(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Multiply(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_TrueDivide(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_And(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Xor(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Or(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Lshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Rshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Power(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Remainder(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAdd(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceSubtract(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceMultiply(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceTrueDivide(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceAnd(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceXor(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceOr(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceLshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRshift(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlacePower(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_InPlaceRemainder(IntPtr o1, IntPtr o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Negative(IntPtr o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Positive(IntPtr o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Invert(IntPtr o1);


        //====================================================================
        // Python sequence API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PySequence_Check(IntPtr pointer);

        internal static IntPtr PySequence_GetItem(IntPtr pointer, long index)
        {
            return PySequence_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_GetItem(IntPtr pointer, IntPtr index);

        internal static int PySequence_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PySequence_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        internal static int PySequence_DelItem(IntPtr pointer, long index)
        {
            return PySequence_DelItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelItem(IntPtr pointer, IntPtr index);

        internal static IntPtr PySequence_GetSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_GetSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_GetSlice(IntPtr pointer, IntPtr i1, IntPtr i2);

        internal static int PySequence_SetSlice(IntPtr pointer, long i1, long i2, IntPtr v)
        {
            return PySequence_SetSlice(pointer, new IntPtr(i1), new IntPtr(i2), v);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetSlice(IntPtr pointer, IntPtr i1, IntPtr i2, IntPtr v);

        internal static int PySequence_DelSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_DelSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelSlice(IntPtr pointer, IntPtr i1, IntPtr i2);

        internal static long PySequence_Size(IntPtr pointer)
        {
            return (long) _PySequence_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Size")]
        private static extern IntPtr _PySequence_Size(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Contains(IntPtr pointer, IntPtr item);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Concat(IntPtr pointer, IntPtr other);

        internal static IntPtr PySequence_Repeat(IntPtr pointer, long count)
        {
            return PySequence_Repeat(pointer, new IntPtr(count));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PySequence_Repeat(IntPtr pointer, IntPtr count);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Index(IntPtr pointer, IntPtr item);

        internal static long PySequence_Count(IntPtr pointer, IntPtr value)
        {
            return (long) _PySequence_Count(pointer, value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Count")]
        private static extern IntPtr _PySequence_Count(IntPtr pointer, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySequence_Tuple(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
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
#if PYTHON3
            return PyUnicode_FromKindAndData(_UCS, value, value.Length);
#elif PYTHON2
            return PyString_FromStringAndSize(value, value.Length);
#endif
        }

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyBytes_FromString(string op);

        internal static long PyBytes_Size(IntPtr op)
        {
            return (long) _PyBytes_Size(op);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyBytes_Size")]
        private static extern IntPtr _PyBytes_Size(IntPtr op);

        internal static IntPtr PyBytes_AS_STRING(IntPtr ob)
        {
            return ob + BytesOffset.ob_sval;
        }

        internal static IntPtr PyString_FromStringAndSize(string value, long size)
        {
            return _PyString_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicode_FromStringAndSize")]
        internal static extern IntPtr _PyString_FromStringAndSize(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))] string value,
            IntPtr size
        );

        internal static IntPtr PyUnicode_FromStringAndSize(IntPtr value, long size)
        {
            return PyUnicode_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyUnicode_FromStringAndSize(IntPtr value, IntPtr size);
#elif PYTHON2
        internal static IntPtr PyString_FromStringAndSize(string value, long size)
        {
            return PyString_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyString_FromStringAndSize(string value, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyString_AsString(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyString_Size(IntPtr pointer);
#endif

        internal static bool PyUnicode_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyUnicodeType;
        }

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        internal static IntPtr PyUnicode_FromKindAndData(int kind, string s, long size)
        {
            return PyUnicode_FromKindAndData(kind, s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyUnicode_FromKindAndData(
            int kind,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static IntPtr PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromKindAndData(_UCS, s, size);
        }

        internal static long PyUnicode_GetSize(IntPtr ob)
        {
            return (long)_PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyUnicode_GetSize")]
        private static extern IntPtr _PyUnicode_GetSize(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_FromOrdinal(int c);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromObject")]
        internal static extern IntPtr PyUnicode_FromObject(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromEncodedObject")]
        internal static extern IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err);

        internal static IntPtr PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromUnicode(s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromUnicode")]
        private static extern IntPtr PyUnicode_FromUnicode(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static long PyUnicode_GetSize(IntPtr ob)
        {
            return (long) _PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "GetSize")]
        internal static extern IntPtr _PyUnicode_GetSize(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "AsUnicode")]
        internal static extern IntPtr PyUnicode_AsUnicode(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
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
                int length = (int)PyUnicode_GetSize(op);

                int size = length * _UCS;
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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_New();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDictProxy_New(IntPtr dict);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItemString(IntPtr pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItemString(IntPtr pointer, string key, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItem(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItemString(IntPtr pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyMapping_HasKey(IntPtr pointer, IntPtr key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Keys(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Values(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Items(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Copy(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Update(IntPtr pointer, IntPtr other);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyDict_Clear(IntPtr pointer);

        internal static long PyDict_Size(IntPtr pointer)
        {
            return (long) _PyDict_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyDict_Size")]
        internal static extern IntPtr _PyDict_Size(IntPtr pointer);


        //====================================================================
        // Python list API
        //====================================================================

        internal static bool PyList_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyListType;
        }

        internal static IntPtr PyList_New(long size)
        {
            return PyList_New(new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_New(IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyList_AsTuple(IntPtr pointer);

        internal static IntPtr PyList_GetItem(IntPtr pointer, long index)
        {
            return PyList_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_GetItem(IntPtr pointer, IntPtr index);

        internal static int PyList_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PyList_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        internal static int PyList_Insert(IntPtr pointer, long index, IntPtr value)
        {
            return PyList_Insert(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_Insert(IntPtr pointer, IntPtr index, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Append(IntPtr pointer, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Reverse(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Sort(IntPtr pointer);

        internal static IntPtr PyList_GetSlice(IntPtr pointer, long start, long end)
        {
            return PyList_GetSlice(pointer, new IntPtr(start), new IntPtr(end));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_GetSlice(IntPtr pointer, IntPtr start, IntPtr end);

        internal static int PyList_SetSlice(IntPtr pointer, long start, long end, IntPtr value)
        {
            return PyList_SetSlice(pointer, new IntPtr(start), new IntPtr(end), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetSlice(IntPtr pointer, IntPtr start, IntPtr end, IntPtr value);

        internal static long PyList_Size(IntPtr pointer)
        {
            return (long) _PyList_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyList_Size")]
        private static extern IntPtr _PyList_Size(IntPtr pointer);

        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyTupleType;
        }

        internal static IntPtr PyTuple_New(long size)
        {
            return PyTuple_New(new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_New(IntPtr size);

        internal static IntPtr PyTuple_GetItem(IntPtr pointer, long index)
        {
            return PyTuple_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_GetItem(IntPtr pointer, IntPtr index);

        internal static int PyTuple_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PyTuple_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyTuple_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        internal static IntPtr PyTuple_GetSlice(IntPtr pointer, long start, long end)
        {
            return PyTuple_GetSlice(pointer, new IntPtr(start), new IntPtr(end));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyTuple_GetSlice(IntPtr pointer, IntPtr start, IntPtr end);

        internal static long PyTuple_Size(IntPtr pointer)
        {
            return (long) _PyTuple_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyTuple_Size")]
        private static extern IntPtr _PyTuple_Size(IntPtr pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

        internal static bool PyIter_Check(IntPtr pointer)
        {
            var ob_type = Marshal.ReadIntPtr(pointer, ObjectOffset.ob_type);
#if PYTHON2
            long tp_flags = Util.ReadCLong(ob_type, TypeOffset.tp_flags);
            if ((tp_flags & TypeFlags.HaveIter) == 0)
                return false;
#endif
            IntPtr tp_iternext = Marshal.ReadIntPtr(ob_type, TypeOffset.tp_iternext);
            return tp_iternext != IntPtr.Zero && tp_iternext != _PyObject_NextNotImplemented;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyIter_Next(IntPtr pointer);


        //====================================================================
        // Python module API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_New(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetName(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_GetDict(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetFilename(IntPtr module);

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyModule_Create2(IntPtr module, int apiver);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_Import(IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ImportModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ReloadModule(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_AddModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_GetModuleDict();

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv,
            int updatepath
        );
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            string[] argv,
            int updatepath
        );
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySys_GetObject(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySys_SetObject(string name, IntPtr ob);


        //====================================================================
        // Python type object API
        //====================================================================

        internal static bool PyType_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, PyTypeType);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyType_Modified(IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyType_IsSubtype(IntPtr t1, IntPtr t2);

        internal static bool PyObject_TypeCheck(IntPtr ob, IntPtr tp)
        {
            IntPtr t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

        internal static IntPtr PyType_GenericAlloc(IntPtr type, long n)
        {
            return PyType_GenericAlloc(type, new IntPtr(n));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyType_GenericAlloc(IntPtr type, IntPtr n);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyType_Ready(IntPtr type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyType_Lookup(IntPtr type, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GenericGetAttr(IntPtr obj, IntPtr name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyObject_GetDictPtr(IntPtr obj);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GC_New(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Del(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Track(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_UnTrack(IntPtr tp);


        //====================================================================
        // Python memory API
        //====================================================================

        internal static IntPtr PyMem_Malloc(long size)
        {
            return PyMem_Malloc(new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyMem_Malloc(IntPtr size);

        internal static IntPtr PyMem_Realloc(IntPtr ptr, long size)
        {
            return PyMem_Realloc(ptr, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyMem_Realloc(IntPtr ptr, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyMem_Free(IntPtr ptr);


        //====================================================================
        // Python exception API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetString(IntPtr ob, string message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetObject(IntPtr ob, IntPtr message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyErr_SetFromErrno(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetNone(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_ExceptionMatches(IntPtr exception);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_NormalizeException(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyErr_Occurred();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Fetch(ref IntPtr ob, ref IntPtr val, ref IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Clear();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Print();


        //====================================================================
        // Miscellaneous
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Self(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_Function(IntPtr ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_AddPendingCall(IntPtr func, IntPtr arg);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_MakePendingCalls();
    }
}
