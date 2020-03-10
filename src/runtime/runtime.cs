using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Python.Runtime.Platform;

namespace Python.Runtime
{

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
#elif PYTHON38
        internal const string _pyversion = "3.8";
        internal const string _pyver = "38";
#else
#error You must define one of PYTHON34 to PYTHON38 or PYTHON27
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
            ["armv7l"] = MachineType.armv7l,
            ["armv8"] = MachineType.armv8,
            ["aarch64"] = MachineType.aarch64,
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

        private static PyReferenceCollection _pyRefs = new PyReferenceCollection();

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

            GenericUtil.Reset();
            PyScopeManager.Reset();
            ClassManager.Reset();
            ClassDerivedObject.Reset();
            TypeManager.Reset();

            IntPtr op;
            {
                var builtins = GetBuiltins();
                SetPyMember(ref PyNotImplemented, PyObject_GetAttrString(builtins, "NotImplemented"),
                    () => PyNotImplemented = IntPtr.Zero);

                SetPyMember(ref PyBaseObjectType, PyObject_GetAttrString(builtins, "object"),
                    () => PyBaseObjectType = IntPtr.Zero);

                SetPyMember(ref PyNone, PyObject_GetAttrString(builtins, "None"),
                    () => PyNone = IntPtr.Zero);
                SetPyMember(ref PyTrue, PyObject_GetAttrString(builtins, "True"),
                    () => PyTrue = IntPtr.Zero);
                SetPyMember(ref PyFalse, PyObject_GetAttrString(builtins, "False"),
                    () => PyFalse = IntPtr.Zero);

                SetPyMember(ref PyBoolType, PyObject_Type(PyTrue),
                    () => PyBoolType = IntPtr.Zero);
                SetPyMember(ref PyNoneType, PyObject_Type(PyNone),
                    () => PyNoneType = IntPtr.Zero);
                SetPyMember(ref PyTypeType, PyObject_Type(PyNoneType),
                    () => PyTypeType = IntPtr.Zero);

                op = PyObject_GetAttrString(builtins, "len");
                SetPyMember(ref PyMethodType, PyObject_Type(op),
                    () => PyMethodType = IntPtr.Zero);
                XDecref(op);

                // For some arcane reason, builtins.__dict__.__setitem__ is *not*
                // a wrapper_descriptor, even though dict.__setitem__ is.
                //
                // object.__init__ seems safe, though.
                op = PyObject_GetAttrString(PyBaseObjectType, "__init__");
                SetPyMember(ref PyWrapperDescriptorType, PyObject_Type(op),
                    () => PyWrapperDescriptorType = IntPtr.Zero);
                XDecref(op);

                SetPyMember(ref PySuper_Type, PyObject_GetAttrString(builtins, "super"),
                    () => PySuper_Type = IntPtr.Zero);

                XDecref(builtins);
            }

            op = PyString_FromString("string");
            SetPyMember(ref PyStringType, PyObject_Type(op),
                () => PyStringType = IntPtr.Zero);
            XDecref(op);

            op = PyUnicode_FromString("unicode");
            SetPyMember(ref PyUnicodeType, PyObject_Type(op),
                () => PyUnicodeType = IntPtr.Zero);
            XDecref(op);

#if PYTHON3
            op = PyBytes_FromString("bytes");
            SetPyMember(ref PyBytesType, PyObject_Type(op),
                () => PyBytesType = IntPtr.Zero);
            XDecref(op);
#endif

            op = PyTuple_New(0);
            SetPyMember(ref PyTupleType, PyObject_Type(op),
                () => PyTupleType = IntPtr.Zero);
            XDecref(op);

            op = PyList_New(0);
            SetPyMember(ref PyListType, PyObject_Type(op),
                () => PyListType = IntPtr.Zero);
            XDecref(op);

            op = PyDict_New();
            SetPyMember(ref PyDictType, PyObject_Type(op),
                () => PyDictType = IntPtr.Zero);
            XDecref(op);

            op = PyInt_FromInt32(0);
            SetPyMember(ref PyIntType, PyObject_Type(op),
                () => PyIntType = IntPtr.Zero);
            XDecref(op);

            op = PyLong_FromLong(0);
            SetPyMember(ref PyLongType, PyObject_Type(op),
                () => PyLongType = IntPtr.Zero);
            XDecref(op);

            op = PyFloat_FromDouble(0);
            SetPyMember(ref PyFloatType, PyObject_Type(op),
                () => PyFloatType = IntPtr.Zero);
            XDecref(op);

#if !PYTHON2
            PyClassType = IntPtr.Zero;
            PyInstanceType = IntPtr.Zero;
#else
            {
                IntPtr s = PyString_FromString("_temp");
                IntPtr d = PyDict_New();

                IntPtr c = PyClass_New(IntPtr.Zero, d, s);
                SetPyMember(ref PyClassType, PyObject_Type(c),
                    () => PyClassType = IntPtr.Zero);

                IntPtr i = PyInstance_New(c, IntPtr.Zero, IntPtr.Zero);
                SetPyMember(ref PyInstanceType, PyObject_Type(i),
                    () => PyInstanceType = IntPtr.Zero);

                XDecref(s);
                XDecref(i);
                XDecref(c);
                XDecref(d);
            }
#endif

            Error = new IntPtr(-1);

            // Initialize data about the platform we're running on. We need
            // this for the type manager and potentially other details. Must
            // happen after caching the python types, above.
            InitializePlatformData();

            IntPtr dllLocal = IntPtr.Zero;
            var loader = LibraryLoader.Get(OperatingSystem);

            if (_PythonDll != "__Internal")
            {
                dllLocal = loader.Load(_PythonDll);
            }
            _PyObject_NextNotImplemented = loader.GetFunction(dllLocal, "_PyObject_NextNotImplemented");
            PyModuleType = loader.GetFunction(dllLocal, "PyModule_Type");

            if (dllLocal != IntPtr.Zero)
            {
                loader.Free(dllLocal);
            }

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
            PyList_Append(new BorrowedReference(path), item);
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
            // TOOD: PyCLRMetaType's release operation still in #958
            PyCLRMetaType = IntPtr.Zero;
            ResetPyMembers();
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

        private static void SetPyMember(ref IntPtr obj, IntPtr value, Action onRelease)
        {
            // XXX: For current usages, value should not be null.
            PythonException.ThrowIfIsNull(value);
            obj = value;
            _pyRefs.Add(value, onRelease);
        }

        private static void ResetPyMembers()
        {
            _pyRefs.Release();
        }

        internal static IntPtr Py_single_input = (IntPtr)256;
        internal static IntPtr Py_file_input = (IntPtr)257;
        internal static IntPtr Py_eval_input = (IntPtr)258;

        internal static IntPtr PyBaseObjectType;
        internal static IntPtr PyModuleType;
        internal static IntPtr PyClassType;
        internal static IntPtr PyInstanceType;
        internal static IntPtr PySuper_Type;
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

        internal static IntPtr Py_NoSiteFlag;

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
            if (PyErr_Occurred() != PyHandle.Null)
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
        internal static unsafe void XIncref(PyHandle op)
        {
            op.XIncref();
        }

        /// <summary>
        /// Increase Python's ref counter for the given object, and get the object back.
        /// </summary>
        internal static IntPtr SelfIncRef(PyHandle op)
        {
            op.XIncref();
            return op;
        }

        internal static void XDecref(PyHandle op)
        {
            op.XDecref();
        }

        internal static long Refcount(PyHandle op)
        {
            return op.RefCount;
        }

        /// <summary>
        /// Export of Macro Py_XIncRef. Use XIncref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_IncRef(PyHandle ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Py_DecRef(PyHandle ob);

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

#if !PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyGILState_Check();
#else
        
#endif

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
        internal static extern NewReference PyRun_String(string code, IntPtr st, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals);

#if PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_CompileString(string code, string file, int start);
#else
        /// <summary>
        /// Return value: New reference.
        /// This is a simplified interface to Py_CompileStringFlags() below, leaving flags set to NULL.
        /// </summary>
        internal static IntPtr Py_CompileString(string str, string file, int start)
        {
            return Py_CompileStringFlags(str, file, start, IntPtr.Zero);
        }

        /// <summary>
        /// Return value: New reference.
        /// This is a simplified interface to Py_CompileStringExFlags() below, with optimize set to -1.
        /// </summary>
        internal static IntPtr Py_CompileStringFlags(string str, string file, int start, IntPtr flags)
        {
            return Py_CompileStringExFlags(str, file, start, flags, -1);
        }

        /// <summary>
        /// Return value: New reference.
        /// Like Py_CompileStringObject(), but filename is a byte string decoded from the filesystem encoding(os.fsdecode()).
        /// </summary>
        /// <returns></returns>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Py_CompileStringExFlags(string str, string file, int start, IntPtr flags, int optimize);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

#if PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyClass_New(IntPtr bases, IntPtr dict, IntPtr name);
#endif

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
        internal static unsafe PyHandle PyObject_TYPE(PyHandle op)
        {
            var p = (void*)op;
            if ((void*)0 == p)
            {
                return PyHandle.Null;
            }
#if PYTHON_WITH_PYDEBUG
            var n = 3;
#else
            var n = 1;
#endif
            return Is32Bit
                ? new PyHandle((void*)(*((uint*)p + n)))
                : new PyHandle((void*)(*((ulong*)p + n)));
        }

        /// <summary>
        /// Managed version of the standard Python C API PyObject_Type call.
        /// This version avoids a managed  &lt;-&gt; unmanaged transition.
        /// This one does incref the returned type object.
        /// </summary>
        internal static PyHandle PyObject_Type(IntPtr op)
        {
            PyHandle tp = PyObject_TYPE(op);
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
        internal static extern int PyObject_IsInstance(PyHandle ob, PyHandle type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsSubclass(PyHandle ob, PyHandle type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCallable_Check(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_IsTrue(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_Not(PyHandle pointer);

        internal static long PyObject_Size(PyHandle pointer)
        {
            return (long)_PyObject_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyObject_Size")]
        private static extern IntPtr _PyObject_Size(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Hash(PyHandle op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Repr(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Str(PyHandle pointer);

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyObject_Str")]
        internal static extern IntPtr PyObject_Unicode(PyHandle pointer);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Dir(PyHandle pointer);


        //====================================================================
        // Python number API
        //====================================================================

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyNumber_Long")]
        internal static extern PyHandle PyNumber_Int(PyHandle ob);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Long(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Float(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyNumber_Check(PyHandle ob);

        internal static bool PyInt_Check(PyHandle ob)
        {
            return PyObject_TypeCheck(ob, PyIntType);
        }

        internal static bool PyBool_Check(PyHandle ob)
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
        private static extern PyHandle PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsLong")]
        internal static extern int PyInt_AsLong(PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromString")]
        internal static extern PyHandle PyInt_FromString(string value, IntPtr end, int radix);
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

        internal static bool PyLong_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern PyHandle PyLong_FromUnsignedLong32(uint value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern PyHandle PyLong_FromUnsignedLong64(ulong value);

        internal static PyHandle PyLong_FromUnsignedLong(object value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_FromUnsignedLong32(Convert.ToUInt32(value));
            else
                return PyLong_FromUnsignedLong64(Convert.ToUInt64(value));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyLong_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyLong_FromLongLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyLong_FromUnsignedLongLong(ulong value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyLong_FromString(string value, IntPtr end, int radix);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyLong_AsLong(PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern uint PyLong_AsUnsignedLong32(PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern ulong PyLong_AsUnsignedLong64(PyHandle value);

        internal static object PyLong_AsUnsignedLong(PyHandle value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_AsUnsignedLong32(value);
            else
                return PyLong_AsUnsignedLong64(value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long PyLong_AsLongLong(PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong PyLong_AsUnsignedLongLong(PyHandle value);

        internal static bool PyFloat_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyFloatType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyFloat_FromDouble(double value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyFloat_FromString(PyHandle value, IntPtr junk);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double PyFloat_AsDouble(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Add(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Subtract(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Multiply(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_TrueDivide(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_And(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Xor(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Or(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Lshift(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Rshift(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Power(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Remainder(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceAdd(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceSubtract(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceMultiply(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceTrueDivide(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceAnd(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceXor(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceOr(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceLshift(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceRshift(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlacePower(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_InPlaceRemainder(PyHandle o1, PyHandle o2);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Negative(PyHandle o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Positive(PyHandle o1);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyNumber_Invert(PyHandle o1);


        //====================================================================
        // Python sequence API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PySequence_Check(PyHandle pointer);

        internal static PyHandle PySequence_GetItem(PyHandle pointer, long index)
        {
            return PySequence_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PySequence_GetItem(PyHandle pointer, IntPtr index);

        internal static int PySequence_SetItem(PyHandle pointer, long index, PyHandle value)
        {
            return PySequence_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetItem(PyHandle pointer, IntPtr index, PyHandle value);

        internal static int PySequence_DelItem(PyHandle pointer, long index)
        {
            return PySequence_DelItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelItem(PyHandle pointer, IntPtr index);

        internal static PyHandle PySequence_GetSlice(PyHandle pointer, long i1, long i2)
        {
            return PySequence_GetSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PySequence_GetSlice(PyHandle pointer, IntPtr i1, IntPtr i2);

        internal static int PySequence_SetSlice(PyHandle pointer, long i1, long i2, PyHandle v)
        {
            return PySequence_SetSlice(pointer, new IntPtr(i1), new IntPtr(i2), v);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_SetSlice(PyHandle pointer, IntPtr i1, IntPtr i2, PyHandle v);

        internal static int PySequence_DelSlice(PyHandle pointer, long i1, long i2)
        {
            return PySequence_DelSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PySequence_DelSlice(PyHandle pointer, IntPtr i1, IntPtr i2);

        internal static long PySequence_Size(PyHandle pointer)
        {
            return (long)_PySequence_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Size")]
        private static extern IntPtr _PySequence_Size(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Contains(PyHandle pointer, PyHandle item);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PySequence_Concat(PyHandle pointer, PyHandle other);

        internal static PyHandle PySequence_Repeat(PyHandle pointer, long count)
        {
            return PySequence_Repeat(pointer, new IntPtr(count));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PySequence_Repeat(PyHandle pointer, IntPtr count);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySequence_Index(PyHandle pointer, PyHandle item);

        internal static long PySequence_Count(PyHandle pointer, PyHandle value)
        {
            return (long)_PySequence_Count(pointer, value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PySequence_Count")]
        private static extern IntPtr _PySequence_Count(PyHandle pointer, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PySequence_Tuple(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PySequence_List(PyHandle pointer);


        //====================================================================
        // Python string API
        //====================================================================

        internal static bool IsStringType(PyHandle op)
        {
            PyHandle t = PyObject_TYPE(op);
            return (t == PyStringType) || (t == PyUnicodeType);
        }

        internal static bool PyString_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyStringType;
        }

        internal static PyHandle PyString_FromString(string value)
        {
#if PYTHON3
            return PyUnicode_FromKindAndData(_UCS, value, value.Length);
#elif PYTHON2
            return PyString_FromStringAndSize(value, value.Length);
#endif
        }

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyBytes_FromString(string op);

        internal static long PyBytes_Size(PyHandle op)
        {
            return (long)_PyBytes_Size(op);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyBytes_Size")]
        private static extern IntPtr _PyBytes_Size(PyHandle op);

        internal static PyHandle PyBytes_AS_STRING(PyHandle ob)
        {
            return (IntPtr)ob + BytesOffset.ob_sval;
        }

        internal static PyHandle PyString_FromStringAndSize(string value, long size)
        {
            return _PyString_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyUnicode_FromStringAndSize")]
        internal static extern PyHandle _PyString_FromStringAndSize(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))] string value,
            IntPtr size
        );

        internal static PyHandle PyUnicode_FromStringAndSize(PyHandle value, long size)
        {
            return PyUnicode_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyUnicode_FromStringAndSize(PyHandle value, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUTF8(PyHandle unicode);

#elif PYTHON2
        internal static PyHandle PyString_FromStringAndSize(string value, long size)
        {
            return PyString_FromStringAndSize(value, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyString_FromStringAndSize(string value, IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyString_AsString(PyHandle op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyString_Size(PyHandle pointer);
#endif

        internal static bool PyUnicode_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyUnicodeType;
        }

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyUnicode_FromObject(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyUnicode_FromEncodedObject(PyHandle ob, IntPtr enc, IntPtr err);

        internal static PyHandle PyUnicode_FromKindAndData(int kind, string s, long size)
        {
            return PyUnicode_FromKindAndData(kind, s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyUnicode_FromKindAndData(
            int kind,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static PyHandle PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromKindAndData(_UCS, s, size);
        }

        internal static long PyUnicode_GetSize(PyHandle ob)
        {
            return (long)_PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyUnicode_GetSize")]
        private static extern IntPtr _PyUnicode_GetSize(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUnicode(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyUnicode_FromOrdinal(int c);
#elif PYTHON2
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromObject")]
        internal static extern PyHandle PyUnicode_FromObject(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromEncodedObject")]
        internal static extern PyHandle PyUnicode_FromEncodedObject(PyHandle ob, IntPtr enc, IntPtr err);

        internal static PyHandle PyUnicode_FromUnicode(string s, long size)
        {
            return PyUnicode_FromUnicode(s, new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromUnicode")]
        private static extern PyHandle PyUnicode_FromUnicode(
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(UcsMarshaler))] string s,
            IntPtr size
        );

        internal static long PyUnicode_GetSize(PyHandle ob)
        {
            return (long) _PyUnicode_GetSize(ob);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "GetSize")]
        internal static extern IntPtr _PyUnicode_GetSize(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "AsUnicode")]
        internal static extern IntPtr PyUnicode_AsUnicode(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = PyUnicodeEntryPoint + "FromOrdinal")]
        internal static extern PyHandle PyUnicode_FromOrdinal(int c);
#endif

        internal static PyHandle PyUnicode_FromString(string s)
        {
            return PyUnicode_FromUnicode(s, s.Length);
        }

        internal static string GetManagedString(in BorrowedReference borrowedReference)
            => GetManagedString(borrowedReference.DangerousGetAddress());
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
        internal static string GetManagedString(PyHandle op)
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

        internal static bool PyDict_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyDictType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_New();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDictProxy_New(PyHandle dict);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_GetItem(PyHandle pointer, PyHandle key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_GetItemString(PyHandle pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItem(PyHandle pointer, PyHandle key, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItemString(PyHandle pointer, string key, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItem(PyHandle pointer, PyHandle key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_DelItemString(PyHandle pointer, string key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyMapping_HasKey(PyHandle pointer, PyHandle key);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_Keys(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_Values(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NewReference PyDict_Items(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyDict_Copy(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Update(PyHandle pointer, PyHandle other);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyDict_Clear(PyHandle pointer);

        internal static long PyDict_Size(PyHandle pointer)
        {
            return (long)_PyDict_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyDict_Size")]
        internal static extern IntPtr _PyDict_Size(PyHandle pointer);


        //====================================================================
        // Python list API
        //====================================================================

        internal static bool PyList_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyListType;
        }

        internal static PyHandle PyList_New(long size)
        {
            return PyList_New(new IntPtr(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyList_New(IntPtr size);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyList_AsTuple(PyHandle pointer);

        internal static BorrowedReference PyList_GetItem(PyHandle pointer, long index)
        {
            return PyList_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern BorrowedReference PyList_GetItem(PyHandle pointer, IntPtr index);

        internal static int PyList_SetItem(PyHandle pointer, long index, PyHandle value)
        {
            return PyList_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetItem(PyHandle pointer, IntPtr index, PyHandle value);

        internal static int PyList_Insert(PyHandle pointer, long index, PyHandle value)
        {
            return PyList_Insert(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_Insert(PyHandle pointer, IntPtr index, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Append(PyHandle pointer, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Reverse(PyHandle pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Sort(PyHandle pointer);

        internal static PyHandle PyList_GetSlice(PyHandle pointer, long start, long end)
        {
            return PyList_GetSlice(pointer, new IntPtr(start), new IntPtr(end));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyList_GetSlice(PyHandle pointer, IntPtr start, IntPtr end);

        internal static int PyList_SetSlice(PyHandle pointer, long start, long end, IntPtr value)
        {
            return PyList_SetSlice(pointer, new IntPtr(start), new IntPtr(end), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetSlice(PyHandle pointer, IntPtr start, IntPtr end, IntPtr value);

        internal static long PyList_Size(PyHandle pointer)
        {
            return (long)_PyList_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyList_Size")]
        private static extern IntPtr _PyList_Size(PyHandle pointer);

        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(PyHandle ob)
        {
            return PyObject_TYPE(ob) == PyTupleType;
        }

        internal static PyHandle PyTuple_New(long size)
        {
            return PyTuple_New(new PyHandle(size));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyTuple_New(PyHandle size);

        internal static PyHandle PyTuple_GetItem(PyHandle pointer, long index)
        {
            return PyTuple_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyTuple_GetItem(PyHandle pointer, IntPtr index);

        internal static int PyTuple_SetItem(PyHandle pointer, long index, PyHandle value)
        {
            return PyTuple_SetItem(pointer, new PyHandle(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyTuple_SetItem(PyHandle pointer, IntPtr index, PyHandle value);

        internal static PyHandle PyTuple_GetSlice(PyHandle pointer, long start, long end)
        {
            return PyTuple_GetSlice(pointer, new PyHandle(start), new IntPtr(end));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyTuple_GetSlice(PyHandle PyHadnle, IntPtr start, IntPtr end);

        internal static long PyTuple_Size(PyHandle pointer)
        {
            return (long)_PyTuple_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyTuple_Size")]
        private static extern IntPtr _PyTuple_Size(PyHandle pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

        internal static bool PyIter_Check(PyHandle pointer)
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
        internal static extern PyHandle PyIter_Next(PyHandle pointer);


        //====================================================================
        // Python module API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyModule_New(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetName(PyHandle module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyModule_GetDict(PyHandle module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern string PyModule_GetFilename(PyHandle module);

#if PYTHON3
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyModule_Create2(IntPtr module, int apiver);
#endif

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyImport_Import(PyHandle name);

        /// <summary>
        /// Return value: New reference.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyImport_ImportModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyImport_ReloadModule(PyHandle module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyImport_AddModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyImport_GetModuleDict();

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
        internal static extern PyHandle PySys_GetObject(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySys_SetObject(string name, PyHandle ob);


        //====================================================================
        // Python type object API
        //====================================================================

        internal static bool PyType_Check(PyHandle ob)
        {
            return PyObject_TypeCheck(ob, PyTypeType);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyType_Modified(PyHandle type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool PyType_IsSubtype(PyHandle t1, PyHandle t2);

        internal static bool PyObject_TypeCheck(PyHandle ob, PyHandle tp)
        {
            PyHandle t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyType_GenericNew(PyHandle type, PyHandle args, PyHandle kw);

        internal static PyHandle PyType_GenericAlloc(PyHandle type, long n)
        {
            return PyType_GenericAlloc(type, new IntPtr(n));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern PyHandle PyType_GenericAlloc(PyHandle type, IntPtr n);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyType_Ready(PyHandle type);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle _PyType_Lookup(PyHandle type, PyHandle name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyObject_GenericGetAttr(PyHandle obj, PyHandle name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_GenericSetAttr(PyHandle obj, PyHandle name, PyHandle value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _PyObject_GetDictPtr(PyHandle obj);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyObject_GC_New(PyHandle tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Del(PyHandle tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Track(PyHandle tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_UnTrack(PyHandle tp);


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
        internal static extern void PyErr_SetString(PyHandle ob, string message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetObject(PyHandle ob, PyHandle message);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyErr_SetFromErrno(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_SetNone(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_ExceptionMatches(PyHandle exception);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyErr_GivenExceptionMatches(PyHandle ob, PyHandle val);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_NormalizeException(PyHandle ob, PyHandle val, PyHandle tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyErr_Occurred();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Fetch(out PyHandle ob, out PyHandle val, out PyHandle tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Restore(PyHandle ob, PyHandle val, PyHandle tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Clear();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Print();


        //====================================================================
        // Miscellaneous
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyMethod_Self(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PyHandle PyMethod_Function(PyHandle ob);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_AddPendingCall(PyHandle func, PyHandle arg);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Py_MakePendingCalls();

        internal static void SetNoSiteFlag()
        {
            var loader = LibraryLoader.Get(OperatingSystem);

            IntPtr dllLocal;
            if (_PythonDll != "__Internal")
            {
                dllLocal = loader.Load(_PythonDll);
            }

            try
            {
                Py_NoSiteFlag = loader.GetFunction(dllLocal, "Py_NoSiteFlag");
                Marshal.WriteInt32(Py_NoSiteFlag, 1);
            }
            finally
            {
                if (dllLocal != IntPtr.Zero)
                {
                    loader.Free(dllLocal);
                }
            }
        }

        /// <summary>
        /// Return value: New reference.
        /// </summary>
        internal static IntPtr GetBuiltins()
        {
            return IsPython3 ? PyImport_ImportModule("builtins")
                    : PyImport_ImportModule("__builtin__");
        }
    }


    class PyReferenceCollection
    {
        private List<KeyValuePair<IntPtr, Action>> _actions = new List<KeyValuePair<IntPtr, Action>>();

        /// <summary>
        /// Record obj's address to release the obj in the future,
        /// obj must alive before calling Release.
        /// </summary>
        public void Add(IntPtr ob, Action onRelease)
        {
            _actions.Add(new KeyValuePair<IntPtr, Action>(ob, onRelease));
        }

        public void Release()
        {
            foreach (var item in _actions)
            {
                Runtime.XDecref(item.Key);
                item.Value?.Invoke();
            }
            _actions.Clear();
        }
    }
}
