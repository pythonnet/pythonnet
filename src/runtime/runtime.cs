using System.Reflection.Emit;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Python.Runtime.Platform;
using System.Linq;

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

#if PYTHON36
        const string _minor = "6";
#elif PYTHON37
        const string _minor = "7";
#elif PYTHON38
        const string _minor = "8";
#elif PYTHON39
        const string _minor = "9";
#else
#error You must define one of PYTHON36 to PYTHON38
#endif

#if WINDOWS
        internal const string dllBase = "python3" + _minor;
#else
        internal const string dllBase = "python3." + _minor;
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

        // set to true when python is finalizing
        internal static object IsFinalizingLock = new object();
        internal static bool IsFinalizing;

        private static bool _isInitialized = false;

        internal static readonly bool Is32Bit = IntPtr.Size == 4;

        // .NET core: System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        internal static bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        internal static Version InteropVersion { get; }
            = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        public static int MainManagedThreadId { get; private set; }

        /// <summary>
        /// Encoding to use to convert Unicode to/from Managed to Native
        /// </summary>
        internal static readonly Encoding PyEncoding = _UCS == 2 ? Encoding.Unicode : Encoding.UTF32;

        public static ShutdownMode ShutdownMode { get; internal set; }
        private static PyReferenceCollection _pyRefs = new PyReferenceCollection();

        internal static Version PyVersion
        {
            get
            {
                var versionTuple = new PyTuple(PySys_GetObject("version_info"));
                var major = versionTuple[0].As<int>();
                var minor = versionTuple[1].As<int>();
                var micro = versionTuple[2].As<int>();
                return new Version(major, minor, micro);
            }
        }


        /// <summary>
        /// Initialize the runtime...
        /// </summary>
        /// <remarks>Always call this method from the Main thread.  After the 
        /// first call to this method, the main thread has acquired the GIL.</remarks>
        internal static void Initialize(bool initSigs = false, ShutdownMode mode = ShutdownMode.Default)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            if (mode == ShutdownMode.Default)
            {
                mode = GetDefaultShutdownMode();
            }
            ShutdownMode = mode;

            if (Py_IsInitialized() == 0)
            {
                Py_InitializeEx(initSigs ? 1 : 0);
                if (PyEval_ThreadsInitialized() == 0)
                {
                    PyEval_InitThreads();
                }
                // XXX: Reload mode may reduct to Soft mode,
                // so even on Reload mode it still needs to save the RuntimeState
                if (mode == ShutdownMode.Soft || mode == ShutdownMode.Reload)
                {
                    RuntimeState.Save();
                }
            }
            else
            {
                // If we're coming back from a domain reload or a soft shutdown,
                // we have previously released the thread state. Restore the main
                // thread state here.
                PyGILState_Ensure();
            }
            MainManagedThreadId = Thread.CurrentThread.ManagedThreadId;

            IsFinalizing = false;

            GenericUtil.Reset();
            PyScopeManager.Reset();
            ClassManager.Reset();
            ClassDerivedObject.Reset();
            TypeManager.Initialize();

            InitPyMembers();

            // Initialize data about the platform we're running on. We need
            // this for the type manager and potentially other details. Must
            // happen after caching the python types, above.
            NativeCodePageHelper.InitializePlatformData();

            // Initialize modules that depend on the runtime class.
            AssemblyManager.Initialize();
            if (mode == ShutdownMode.Reload && RuntimeData.HasStashData())
            {
                RuntimeData.RestoreRuntimeData();
            }
            else
            {
                PyCLRMetaType = MetaType.Initialize(); // Steal a reference
                ImportHook.Initialize();
            }
            Exceptions.Initialize();

            // Need to add the runtime directory to sys.path so that we
            // can find built-in assemblies like System.Data, et. al.
            string rtdir = RuntimeEnvironment.GetRuntimeDirectory();
            IntPtr path = PySys_GetObject("path").DangerousGetAddress();
            IntPtr item = PyString_FromString(rtdir);
            if (PySequence_Contains(path, item) == 0)
            {
                PyList_Append(new BorrowedReference(path), item);
            }
            XDecref(item);
            AssemblyManager.UpdatePath();
        }

        private static void InitPyMembers()
        {
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

            op = PyBytes_FromString("bytes");
            SetPyMember(ref PyBytesType, PyObject_Type(op),
                () => PyBytesType = IntPtr.Zero);
            XDecref(op);

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

            PyClassType = IntPtr.Zero;
            PyInstanceType = IntPtr.Zero;

            Error = new IntPtr(-1);

            _PyObject_NextNotImplemented = Get_PyObject_NextNotImplemented();
            {
                IntPtr sys = PyImport_ImportModule("sys");
                PyModuleType = PyObject_Type(sys);
                XDecref(sys);
            }
        }

        private static IntPtr Get_PyObject_NextNotImplemented()
        {
            IntPtr pyType = SlotHelper.CreateObjectType();
            IntPtr iternext = Marshal.ReadIntPtr(pyType, TypeOffset.tp_iternext);
            Runtime.XDecref(pyType);
            return iternext;
        }

        /// <summary>
        /// Tries to downgrade the shutdown mode, if possible.
        /// The only possibles downgrades are:
        /// Soft -> Normal
        /// Reload -> Soft
        /// Reload -> Normal
        /// </summary>
        /// <param name="mode">The desired shutdown mode</param>
        /// <returns>The `mode` parameter if the downgrade is supported, the ShutdownMode
        ///  set at initialization otherwise.</returns>
        static ShutdownMode TryDowngradeShutdown(ShutdownMode mode)
        {
            if (
                mode == Runtime.ShutdownMode
                || mode == ShutdownMode.Normal
                || (mode == ShutdownMode.Soft && Runtime.ShutdownMode == ShutdownMode.Reload)
                )
            {
                return mode;
            }
            else // we can't downgrade
            {
                return Runtime.ShutdownMode;
            }
        }

        internal static void Shutdown(ShutdownMode mode)
        {
            if (Py_IsInitialized() == 0 || !_isInitialized)
            {
                return;
            }
            _isInitialized = false;

            // If the shutdown mode specified is not the the same as the one specified
            // during Initialization, we need to validate it; we can only downgrade,
            // not upgrade the shutdown mode.
            mode = TryDowngradeShutdown(mode);

            var state = PyGILState_Ensure();

            if (mode == ShutdownMode.Soft)
            {
                RunExitFuncs();
            }
            if (mode == ShutdownMode.Reload)
            {
                RuntimeData.Stash();
            }
            AssemblyManager.Shutdown();
            ImportHook.Shutdown();

            ClearClrModules();
            RemoveClrRootModule();

            MoveClrInstancesOnwershipToPython();
            ClassManager.DisposePythonWrappersForClrTypes();
            TypeManager.RemoveTypes();

            MetaType.Release();
            PyCLRMetaType = IntPtr.Zero;

            Exceptions.Shutdown();
            Finalizer.Shutdown();

            if (mode != ShutdownMode.Normal)
            {
                PyGC_Collect();
                if (mode == ShutdownMode.Soft)
                {
                    RuntimeState.Restore();
                }
                ResetPyMembers();
                GC.Collect();
                try
                {
                    GC.WaitForFullGCComplete();
                }
                catch (NotImplementedException)
                {
                    // Some clr runtime didn't implement GC.WaitForFullGCComplete yet.
                }
                GC.WaitForPendingFinalizers();
                PyGILState_Release(state);
                // Then release the GIL for good, if there is somehting to release
                // Use the unchecked version as the checked version calls `abort()`
                // if the current state is NULL.
                if (_PyThreadState_UncheckedGet() != IntPtr.Zero)
                {
                    PyEval_SaveThread();
                }
                
            }
            else
            {
                ResetPyMembers();
                Py_Finalize();
            }
        }

        internal static void Shutdown()
        {
            var mode = ShutdownMode;
            Shutdown(mode);
        }

        internal static ShutdownMode GetDefaultShutdownMode()
        {
            string modeEvn = Environment.GetEnvironmentVariable("PYTHONNET_SHUTDOWN_MODE");
            if (modeEvn == null)
            {
                return ShutdownMode.Normal;
            }
            ShutdownMode mode;
            if (Enum.TryParse(modeEvn, true, out mode))
            {
                return mode;
            }
            return ShutdownMode.Normal;
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

        private static void RunExitFuncs()
        {
            PyObject atexit;
            try
            {
                atexit = Py.Import("atexit");
            }
            catch (PythonException e)
            {
                if (!e.IsMatches(Exceptions.ImportError))
                {
                    throw;
                }
                e.Dispose();
                // The runtime may not provided `atexit` module.
                return;
            }
            using (atexit)
            {
                try
                {
                    atexit.InvokeMethod("_run_exitfuncs").Dispose();
                }
                catch (PythonException e)
                {
                    Console.Error.WriteLine(e);
                    e.Dispose();
                }
            }
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

        private static void ClearClrModules()
        {
            var modules = PyImport_GetModuleDict();
            var items = PyDict_Items(modules);
            long length = PyList_Size(items);
            for (long i = 0; i < length; i++)
            {
                var item = PyList_GetItem(items, i);
                var name = PyTuple_GetItem(item.DangerousGetAddress(), 0);
                var module = PyTuple_GetItem(item.DangerousGetAddress(), 1);
                if (ManagedType.IsManagedType(module))
                {
                    PyDict_DelItem(modules, name);
                }
            }
            items.Dispose();
        }

        private static void RemoveClrRootModule()
        {
            var modules = PyImport_GetModuleDict();
            PyDictTryDelItem(modules, "CLR");
            PyDictTryDelItem(modules, "clr");
            PyDictTryDelItem(modules, "clr._extra");
        }

        private static void PyDictTryDelItem(IntPtr dict, string key)
        {
            if (PyDict_DelItemString(dict, key) == 0)
            {
                return;
            }
            if (!PythonException.Matches(Exceptions.KeyError))
            {
                throw new PythonException();
            }
            PyErr_Clear();
        }

        private static void MoveClrInstancesOnwershipToPython()
        {
            var objs = ManagedType.GetManagedObjects();
            var copyObjs = objs.ToArray();
            foreach (var entry in copyObjs)
            {
                ManagedType obj = entry.Key;
                if (!objs.ContainsKey(obj))
                {
                    System.Diagnostics.Debug.Assert(obj.gcHandle == default);
                    continue;
                }
                if (entry.Value == ManagedType.TrackTypes.Extension)
                {
                    obj.CallTypeClear();
                    // obj's tp_type will degenerate to a pure Python type after TypeManager.RemoveTypes(),
                    // thus just be safe to give it back to GC chain.
                    if (!_PyObject_GC_IS_TRACKED(obj.pyHandle))
                    {
                        PyObject_GC_Track(obj.pyHandle);
                    }
                }
                if (obj.gcHandle.IsAllocated)
                {
                    obj.gcHandle.Free();
                }
                obj.gcHandle = default;
            }
            ManagedType.ClearTrackedObjects();
        }

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

        internal static IntPtr PyBytesType;
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

        public static PyObject None
        {
            get
            {
                var none = Runtime.PyNone;
                Runtime.XIncref(none);
                return new PyObject(none);
            }
        }

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

        /// <summary>
        /// Increase Python's ref counter for the given object, and get the object back.
        /// </summary>
        internal static IntPtr SelfIncRef(IntPtr op)
        {
            XIncref(op);
            return op;
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
                    NativeCall.Void_Call_1(new IntPtr(f), op);
                }
            }
#endif
        }

        [Pure]
        internal static unsafe long Refcount(IntPtr op)
        {
#if PYTHON_WITH_PYDEBUG
            var p = (void*)(op + TypeOffset.ob_refcnt);
#else
            var p = (void*)op;
#endif
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
        internal static extern IntPtr _PyThreadState_UncheckedGet();

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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Py_Main(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv
        );

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
        internal static extern NewReference PyRun_String([MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8Marshaler))] string code, RunFlagType st, IntPtr globals, IntPtr locals);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals);

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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ExecCodeModule(string name, IntPtr code);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyMethod_New(IntPtr func, IntPtr self, IntPtr cls);


        //====================================================================
        // Python abstract object API
        //====================================================================

        /// <summary>
        /// Return value: Borrowed reference.
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
            IntPtr tp_iter = Marshal.ReadIntPtr(ob_type, TypeOffset.tp_iter);
            return tp_iter != IntPtr.Zero;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_HasAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttrString(IntPtr pointer, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_GetAttrString(IntPtr pointer, IntPtr name);

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
            return (long)_PyObject_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyObject_Size")]
        private static extern IntPtr _PyObject_Size(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Hash(IntPtr op);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Repr(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Str(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyObject_Str")]
        internal static extern IntPtr PyObject_Unicode(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyObject_Dir(IntPtr pointer);

#if PYTHON_WITH_PYDEBUG
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void _Py_NewReference(IntPtr ob);
#endif

        //====================================================================
        // Python buffer API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyObject_GetBuffer(IntPtr exporter, ref Py_buffer view, int flags);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyBuffer_Release(ref Py_buffer view);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr PyBuffer_SizeFromFormat([MarshalAs(UnmanagedType.LPStr)] string format);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyBuffer_IsContiguous(ref Py_buffer view, char order);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyBuffer_GetPointer(ref Py_buffer view, IntPtr[] indices);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyBuffer_FromContiguous(ref Py_buffer view, IntPtr buf, IntPtr len, char fort);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyBuffer_ToContiguous(IntPtr buf, ref Py_buffer src, IntPtr len, char order);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyBuffer_FillContiguousStrides(int ndims, IntPtr shape, IntPtr strides, int itemsize, char order);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyBuffer_FillInfo(ref Py_buffer view, IntPtr exporter, IntPtr buf, IntPtr len, int _readonly, int flags);

        //====================================================================
        // Python number API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyNumber_Long")]
        internal static extern IntPtr PyNumber_Int(IntPtr ob);

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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromLong")]
        private static extern IntPtr PyInt_FromLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsLong")]
        internal static extern int PyInt_AsLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromString")]
        internal static extern IntPtr PyInt_FromString(string value, IntPtr end, int radix);

        internal static bool PyLong_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromLong(long value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern IntPtr PyLong_FromUnsignedLong32(uint value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_FromUnsignedLong")]
        internal static extern IntPtr PyLong_FromUnsignedLong64(ulong value);

        internal static IntPtr PyLong_FromUnsignedLong(object value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_FromUnsignedLong32(Convert.ToUInt32(value));
            else
                return PyLong_FromUnsignedLong64(Convert.ToUInt64(value));
        }

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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern uint PyLong_AsUnsignedLong32(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "PyLong_AsUnsignedLong")]
        internal static extern ulong PyLong_AsUnsignedLong64(IntPtr value);

        internal static object PyLong_AsUnsignedLong(IntPtr value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_AsUnsignedLong32(value);
            else
                return PyLong_AsUnsignedLong64(value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long PyLong_AsLongLong(IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong PyLong_AsUnsignedLongLong(IntPtr value);

        internal static bool PyFloat_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyFloatType;
        }

        /// <summary>
        /// Return value: New reference.
        /// Create a Python integer from the pointer p. The pointer value can be retrieved from the resulting value using PyLong_AsVoidPtr().
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_FromVoidPtr(IntPtr p);

        /// <summary>
        /// Convert a Python integer pylong to a C void pointer. If pylong cannot be converted, an OverflowError will be raised. This is only assured to produce a usable void pointer for values created with PyLong_FromVoidPtr().
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyLong_AsVoidPtr(IntPtr ob);

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
            return (long)_PySequence_Size(pointer);
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
            return (long)_PySequence_Count(pointer, value);
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
            return PyUnicode_FromKindAndData(_UCS, value, value.Length);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyBytes_FromString(string op);

        internal static long PyBytes_Size(IntPtr op)
        {
            return (long)_PyBytes_Size(op);
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

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyUnicode_AsUTF8(IntPtr unicode);

        internal static bool PyUnicode_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyUnicodeType;
        }

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

        internal static IntPtr PyUnicode_FromString(string s)
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
        internal static string GetManagedString(IntPtr op)
        {
            IntPtr type = PyObject_TYPE(op);

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
        internal static extern int PyDict_Next(IntPtr p, out IntPtr ppos, out IntPtr pkey, out IntPtr pvalue);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDictProxy_New(IntPtr dict);

        /// <summary>
        /// Return value: Borrowed reference.
        /// Return NULL if the key key is not present, but without setting an exception.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItem(IntPtr pointer, IntPtr key);

        /// <summary>
        /// Return value: Borrowed reference.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_GetItemString(IntPtr pointer, string key);

        /// <summary>
        /// Return 0 on success or -1 on failure.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_SetItem(IntPtr pointer, IntPtr key, IntPtr value);

        /// <summary>
        ///  Return 0 on success or -1 on failure.
        /// </summary>
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
        internal static extern NewReference PyDict_Items(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyDict_Copy(IntPtr pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyDict_Update(IntPtr pointer, IntPtr other);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyDict_Clear(IntPtr pointer);

        internal static long PyDict_Size(IntPtr pointer)
        {
            return (long)_PyDict_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyDict_Size")]
        internal static extern IntPtr _PyDict_Size(IntPtr pointer);


        /// <summary>
        /// Return value: New reference.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PySet_New(IntPtr iterable);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySet_Add(IntPtr set, IntPtr key);

        /// <summary>
        /// Return 1 if found, 0 if not found, and -1 if an error is encountered. 
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PySet_Contains(IntPtr anyset, IntPtr key);

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

        internal static BorrowedReference PyList_GetItem(BorrowedReference pointer, long index)
        {
            return PyList_GetItem(pointer, new IntPtr(index));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern BorrowedReference PyList_GetItem(BorrowedReference pointer, IntPtr index);

        internal static int PyList_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PyList_SetItem(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetItem(IntPtr pointer, IntPtr index, IntPtr value);

        internal static int PyList_Insert(BorrowedReference pointer, long index, IntPtr value)
        {
            return PyList_Insert(pointer, new IntPtr(index), value);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_Insert(BorrowedReference pointer, IntPtr index, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Append(BorrowedReference pointer, IntPtr value);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Reverse(BorrowedReference pointer);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyList_Sort(BorrowedReference pointer);

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

        internal static long PyList_Size(BorrowedReference pointer)
        {
            return (long)_PyList_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyList_Size")]
        private static extern IntPtr _PyList_Size(BorrowedReference pointer);

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
            return (long)_PyTuple_Size(pointer);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PyTuple_Size")]
        private static extern IntPtr _PyTuple_Size(IntPtr pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

        internal static bool PyIter_Check(IntPtr pointer)
        {
            var ob_type = Marshal.ReadIntPtr(pointer, ObjectOffset.ob_type);
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

#if PYTHON_WITH_PYDEBUG
        [DllImport(_PythonDll, EntryPoint = "PyModule_Create2TraceRefs", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
#endif
        internal static extern IntPtr PyModule_Create2(IntPtr module, int apiver);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_Import(IntPtr name);

        /// <summary>
        /// Return value: New reference.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ImportModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_ReloadModule(IntPtr module);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_AddModule(string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyImport_GetModuleDict();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PySys_SetArgvEx(
            int argc,
            [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(StrArrayMarshaler))] string[] argv,
            int updatepath
        );

        /// <summary>
        /// Return value: Borrowed reference.
        /// Return the object name from the sys module or NULL if it does not exist, without setting an exception.
        /// </summary>
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BorrowedReference PySys_GetObject(string name);

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

        internal static bool PyType_IsSameAsOrSubtype(IntPtr type, IntPtr ofType)
        {
            return (type == ofType) || PyType_IsSubtype(type, ofType);
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw);

        internal static IntPtr PyType_GenericAlloc(IntPtr type, long n)
        {
            return PyType_GenericAlloc(type, new IntPtr(n));
        }

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyType_GenericAlloc(IntPtr type, IntPtr n);

        /// <summary>
        /// Finalize a type object. This should be called on all type objects to finish their initialization. This function is responsible for adding inherited slots from a type’s base class. Return 0 on success, or return -1 and sets an exception on error.
        /// </summary>
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
        internal static extern void PyObject_GC_Del(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_Track(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyObject_GC_UnTrack(IntPtr tp);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void _PyObject_Dump(IntPtr ob);

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
        internal static extern void PyErr_SetObject(BorrowedReference type, BorrowedReference exceptionObject);

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
        internal static extern void PyErr_Fetch(out IntPtr ob, out IntPtr val, out IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Clear();

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PyErr_Print();

        //====================================================================
        // Cell API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NewReference PyCell_Get(BorrowedReference cell);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCell_Set(BorrowedReference cell, IntPtr value);

        //====================================================================
        // Python GC API
        //====================================================================

        internal const int _PyGC_REFS_SHIFT = 1;
        internal const long _PyGC_REFS_UNTRACKED = -2;
        internal const long _PyGC_REFS_REACHABLE = -3;
        internal const long _PyGC_REFS_TENTATIVELY_UNREACHABLE = -4;


        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyGC_Collect();

        internal static IntPtr _Py_AS_GC(IntPtr ob)
        {
            // XXX: PyGC_Head has a force alignment depend on platform.
            // See PyGC_Head in objimpl.h for more details.
            return Is32Bit ? ob - 16 : ob - 24;
        }

        internal static IntPtr _Py_FROM_GC(IntPtr gc)
        {
            return Is32Bit ? gc + 16 : gc + 24;
        }

        internal static IntPtr _PyGCHead_REFS(IntPtr gc)
        {
            unsafe
            {
                var pGC = (PyGC_Head*)gc;
                var refs = pGC->gc.gc_refs;
                if (Is32Bit)
                {
                    return new IntPtr(refs.ToInt32() >> _PyGC_REFS_SHIFT);
                }
                return new IntPtr(refs.ToInt64() >> _PyGC_REFS_SHIFT);
            }
        }

        internal static IntPtr _PyGC_REFS(IntPtr ob)
        {
            return _PyGCHead_REFS(_Py_AS_GC(ob));
        }

        internal static bool _PyObject_GC_IS_TRACKED(IntPtr ob)
        {
            return (long)_PyGC_REFS(ob) != _PyGC_REFS_UNTRACKED;
        }

        internal static void Py_CLEAR(ref IntPtr ob)
        {
            XDecref(ob);
            ob = IntPtr.Zero;
        }

        //====================================================================
        // Python Capsules API
        //====================================================================

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern NewReference PyCapsule_New(IntPtr pointer, string name, IntPtr destructor);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr PyCapsule_GetPointer(BorrowedReference capsule, string name);

        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PyCapsule_SetPointer(BorrowedReference capsule, IntPtr pointer);

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

        internal static void SetNoSiteFlag()
        {
            var loader = LibraryLoader.Get(NativeCodePageHelper.OperatingSystem);
            IntPtr dllLocal = IntPtr.Zero;
            if (_PythonDll != "__Internal")
            {
                dllLocal = loader.Load(_PythonDll);
                if (dllLocal == IntPtr.Zero)
                {
                    throw new Exception($"Cannot load {_PythonDll}");
                }
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
            return PyImport_ImportModule("builtins");
        }
    }


    public enum ShutdownMode
    {
        Default,
        Normal,
        Soft,
        Reload,
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
