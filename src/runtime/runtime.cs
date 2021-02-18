using System.Reflection.Emit;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Python.Runtime.Native;
using Python.Runtime.Platform;
using System.Linq;
using static System.FormattableString;

namespace Python.Runtime
{
    /// <summary>
    /// Encapsulates the low-level Python C API. Note that it is
    /// the responsibility of the caller to have acquired the GIL
    /// before calling any of these methods.
    /// </summary>
    public unsafe class Runtime
    {
        public static string PythonDLL
        {
            get => _PythonDll;
            set
            {
                if (_isInitialized)
                    throw new InvalidOperationException("This property must be set before runtime is initialized");
                _PythonDll = value;
            }
        }

        static string _PythonDll = GetDefaultDllName();
        private static string GetDefaultDllName()
        {
            string dll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            if (dll is not null) return dll;

            try
            {
                LibraryLoader.Instance.GetFunction(IntPtr.Zero, "PyUnicode_GetMax");
                return null;
            } catch (MissingMethodException) { }

            string verString = Environment.GetEnvironmentVariable("PYTHONNET_PYVER");
            if (!Version.TryParse(verString, out var version)) return null;

            return GetDefaultDllName(version);
        }

        private static string GetDefaultDllName(Version version)
        {
            string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";
            string suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Invariant($"{version.Major}{version.Minor}")
                : Invariant($"{version.Major}.{version.Minor}");
            string ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib"
                : ".so";
            return prefix + "python" + suffix + ext;
        }

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

        public static ShutdownMode ShutdownMode { get; internal set; }
        private static PyReferenceCollection _pyRefs = new PyReferenceCollection();

        internal static Version PyVersion
        {
            get
            {
                using (var versionTuple = new PyTuple(PySys_GetObject("version_info")))
                {
                    var major = versionTuple[0].As<int>();
                    var minor = versionTuple[1].As<int>();
                    var micro = versionTuple[2].As<int>();
                    return new Version(major, minor, micro);
                }
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
                Console.WriteLine("Runtime.Initialize(): Py_Initialize...");
                Py_InitializeEx(initSigs ? 1 : 0);
                if (PyEval_ThreadsInitialized() == 0)
                {
                    Console.WriteLine("Runtime.Initialize(): PyEval_InitThreads...");
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
                if (mode != ShutdownMode.Extension)
                {
                    PyGILState_Ensure();
                }
            }
            MainManagedThreadId = Thread.CurrentThread.ManagedThreadId;

            IsFinalizing = false;
            InternString.Initialize();

            Console.WriteLine("Runtime.Initialize(): Initialize types...");
            InitPyMembers();
            Console.WriteLine("Runtime.Initialize(): Initialize types end.");

            ABI.Initialize(PyVersion,
                           pyType: new BorrowedReference(PyTypeType));

            GenericUtil.Reset();
            PyScopeManager.Reset();
            ClassManager.Reset();
            ClassDerivedObject.Reset();
            TypeManager.Initialize();

            // Initialize modules that depend on the runtime class.
            Console.WriteLine("Runtime.Initialize(): AssemblyManager.Initialize()...");
            AssemblyManager.Initialize();
            OperatorMethod.Initialize();
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
            AddToPyPath(RuntimeEnvironment.GetRuntimeDirectory());
            AddToPyPath(Directory.GetCurrentDirectory());

            Console.WriteLine("Runtime.Initialize(): AssemblyManager.UpdatePath()...");
            AssemblyManager.UpdatePath();
        }

        private static void AddToPyPath(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            IntPtr path = PySys_GetObject("path").DangerousGetAddress();
            IntPtr item = PyString_FromString(directory);
            if (PySequence_Contains(path, item) == 0)
            {
                PyList_Append(new BorrowedReference(path), item);
            }

            XDecref(item);
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
                op = PyObject_GetAttr(PyBaseObjectType, PyIdentifier.__init__);
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

            op = EmptyPyBytes();
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

            IntPtr decimalMod = PyImport_ImportModule("_pydecimal");
            IntPtr decimalCtor = PyObject_GetAttrString(decimalMod, "Decimal");
            op = PyObject_CallObject(decimalCtor, IntPtr.Zero);
            PyDecimalType = PyObject_Type(op);
            XDecref(op);
            XDecref(decimalMod);
            XDecref(decimalCtor);

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
            OperatorMethod.Shutdown();
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
            InternString.Shutdown();

            if (mode != ShutdownMode.Normal && mode != ShutdownMode.Extension)
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
                if (mode != ShutdownMode.Extension)
                {
                    Py_Finalize();
                }
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
                var name = PyTuple_GetItem(item, 0);
                var module = PyTuple_GetItem(item, 1);
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
            PyDictTryDelItem(modules, "clr");
            PyDictTryDelItem(modules, "clr._extra");
        }

        private static void PyDictTryDelItem(BorrowedReference dict, string key)
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
                    if (!_PyObject_GC_IS_TRACKED(obj.ObjectReference))
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
        internal static IntPtr PyDecimalType;

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
                    MaybeType _type = ((ClassBase)mt).type;
                    t = _type.Valid ?  _type.Value : null;
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
#if !CUSTOM_INCDEC_REF
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
#if !CUSTOM_INCDEC_REF
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

        internal static void Py_IncRef(IntPtr ob) => Delegates.Py_IncRef(ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>

        internal static void Py_DecRef(IntPtr ob) => Delegates.Py_DecRef(ob);


        internal static void Py_Initialize() => Delegates.Py_Initialize();


        internal static void Py_InitializeEx(int initsigs) => Delegates.Py_InitializeEx(initsigs);


        internal static int Py_IsInitialized() => Delegates.Py_IsInitialized();


        internal static void Py_Finalize() => Delegates.Py_Finalize();


        internal static IntPtr Py_NewInterpreter() => Delegates.Py_NewInterpreter();


        internal static void Py_EndInterpreter(IntPtr threadState) => Delegates.Py_EndInterpreter(threadState);


        internal static IntPtr PyThreadState_New(IntPtr istate) => Delegates.PyThreadState_New(istate);


        internal static IntPtr PyThreadState_Get() => Delegates.PyThreadState_Get();


        internal static IntPtr _PyThreadState_UncheckedGet() => Delegates._PyThreadState_UncheckedGet();


        internal static IntPtr PyThread_get_key_value(IntPtr key) => Delegates.PyThread_get_key_value(key);


        internal static int PyThread_get_thread_ident() => Delegates.PyThread_get_thread_ident();


        internal static int PyThread_set_key_value(IntPtr key, IntPtr value) => Delegates.PyThread_set_key_value(key, value);


        internal static IntPtr PyThreadState_Swap(IntPtr key) => Delegates.PyThreadState_Swap(key);


        internal static IntPtr PyGILState_Ensure() => Delegates.PyGILState_Ensure();


        internal static void PyGILState_Release(IntPtr gs) => Delegates.PyGILState_Release(gs);



        internal static IntPtr PyGILState_GetThisThreadState() => Delegates.PyGILState_GetThisThreadState();


        public static int Py_Main(int argc, string[] argv)
        {
            var marshaler = StrArrayMarshaler.GetInstance(null);
            var argvPtr = marshaler.MarshalManagedToNative(argv);
            try
            {
                return Delegates.Py_Main(argc, argvPtr);
            }
            finally
            {
                marshaler.CleanUpNativeData(argvPtr);
            }
        }

        internal static void PyEval_InitThreads() => Delegates.PyEval_InitThreads();


        internal static int PyEval_ThreadsInitialized() => Delegates.PyEval_ThreadsInitialized();


        internal static void PyEval_AcquireLock() => Delegates.PyEval_AcquireLock();


        internal static void PyEval_ReleaseLock() => Delegates.PyEval_ReleaseLock();


        internal static void PyEval_AcquireThread(IntPtr tstate) => Delegates.PyEval_AcquireThread(tstate);


        internal static void PyEval_ReleaseThread(IntPtr tstate) => Delegates.PyEval_ReleaseThread(tstate);


        internal static IntPtr PyEval_SaveThread() => Delegates.PyEval_SaveThread();


        internal static void PyEval_RestoreThread(IntPtr tstate) => Delegates.PyEval_RestoreThread(tstate);


        internal static BorrowedReference PyEval_GetBuiltins() => Delegates.PyEval_GetBuiltins();


        internal static BorrowedReference PyEval_GetGlobals() => Delegates.PyEval_GetGlobals();


        internal static IntPtr PyEval_GetLocals() => Delegates.PyEval_GetLocals();


        internal static IntPtr Py_GetProgramName() => Delegates.Py_GetProgramName();


        internal static void Py_SetProgramName(IntPtr name) => Delegates.Py_SetProgramName(name);


        internal static IntPtr Py_GetPythonHome() => Delegates.Py_GetPythonHome();


        internal static void Py_SetPythonHome(IntPtr home) => Delegates.Py_SetPythonHome(home);


        internal static IntPtr Py_GetPath() => Delegates.Py_GetPath();


        internal static void Py_SetPath(IntPtr home) => Delegates.Py_SetPath(home);


        internal static IntPtr Py_GetVersion() => Delegates.Py_GetVersion();


        internal static IntPtr Py_GetPlatform() => Delegates.Py_GetPlatform();


        internal static IntPtr Py_GetCopyright() => Delegates.Py_GetCopyright();


        internal static IntPtr Py_GetCompiler() => Delegates.Py_GetCompiler();


        internal static IntPtr Py_GetBuildInfo() => Delegates.Py_GetBuildInfo();

        const PyCompilerFlags Utf8String = PyCompilerFlags.IGNORE_COOKIE | PyCompilerFlags.SOURCE_IS_UTF8;

        internal static int PyRun_SimpleString(string code)
        {
            using var codePtr = new StrPtr(code, Encoding.UTF8);
            return Delegates.PyRun_SimpleStringFlags(codePtr, Utf8String);
        }

        internal static NewReference PyRun_String(string code, RunFlagType st, BorrowedReference globals, BorrowedReference locals)
        {
            using var codePtr = new StrPtr(code, Encoding.UTF8);
            return Delegates.PyRun_StringFlags(codePtr, st, globals, locals, Utf8String);
        }

        internal static IntPtr PyEval_EvalCode(IntPtr co, IntPtr globals, IntPtr locals) => Delegates.PyEval_EvalCode(co, globals, locals);

        /// <summary>
        /// Return value: New reference.
        /// This is a simplified interface to Py_CompileStringFlags() below, leaving flags set to NULL.
        /// </summary>
        internal static IntPtr Py_CompileString(string str, string file, int start)
        {
            using var strPtr = new StrPtr(str, Encoding.UTF8);
            using var fileObj = new PyString(file);
            return Delegates.Py_CompileStringObject(strPtr, fileObj.Reference, start, Utf8String, -1);
        }

        internal static IntPtr PyImport_ExecCodeModule(string name, IntPtr code)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyImport_ExecCodeModule(namePtr, code);
        }

        internal static IntPtr PyCFunction_NewEx(IntPtr ml, IntPtr self, IntPtr mod) => Delegates.PyCFunction_NewEx(ml, self, mod);


        internal static IntPtr PyCFunction_Call(IntPtr func, IntPtr args, IntPtr kw) => Delegates.PyCFunction_Call(func, args, kw);


        internal static IntPtr PyMethod_New(IntPtr func, IntPtr self, IntPtr cls) => Delegates.PyMethod_New(func, self, cls);


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
        internal static unsafe BorrowedReference PyObject_TYPE(BorrowedReference op)
            => new BorrowedReference(PyObject_TYPE(op.DangerousGetAddress()));

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


        internal static int PyObject_HasAttrString(BorrowedReference pointer, string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_HasAttrString(pointer, namePtr);
        }

        internal static IntPtr PyObject_GetAttrString(IntPtr pointer, string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_GetAttrString(pointer, namePtr);
        }


        internal static IntPtr PyObject_GetAttrString(IntPtr pointer, StrPtr name) => Delegates.PyObject_GetAttrString(pointer, name);


        internal static int PyObject_SetAttrString(IntPtr pointer, string name, IntPtr value)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_SetAttrString(pointer, namePtr, value);
        }

        internal static int PyObject_HasAttr(BorrowedReference pointer, BorrowedReference name) => Delegates.PyObject_HasAttr(pointer, name);


        internal static NewReference PyObject_GetAttr(BorrowedReference pointer, IntPtr name)
            => Delegates.PyObject_GetAttr(pointer, new BorrowedReference(name));
        internal static IntPtr PyObject_GetAttr(IntPtr pointer, IntPtr name)
            => Delegates.PyObject_GetAttr(new BorrowedReference(pointer), new BorrowedReference(name))
                        .DangerousMoveToPointerOrNull();
        internal static NewReference PyObject_GetAttr(BorrowedReference pointer, BorrowedReference name) => Delegates.PyObject_GetAttr(pointer, name);


        internal static int PyObject_SetAttr(IntPtr pointer, IntPtr name, IntPtr value) => Delegates.PyObject_SetAttr(pointer, name, value);


        internal static IntPtr PyObject_GetItem(IntPtr pointer, IntPtr key) => Delegates.PyObject_GetItem(pointer, key);


        internal static int PyObject_SetItem(IntPtr pointer, IntPtr key, IntPtr value) => Delegates.PyObject_SetItem(pointer, key, value);


        internal static int PyObject_DelItem(IntPtr pointer, IntPtr key) => Delegates.PyObject_DelItem(pointer, key);


        internal static IntPtr PyObject_GetIter(IntPtr op) => Delegates.PyObject_GetIter(op);


        internal static IntPtr PyObject_Call(IntPtr pointer, IntPtr args, IntPtr kw) => Delegates.PyObject_Call(pointer, args, kw);


        internal static IntPtr PyObject_CallObject(IntPtr pointer, IntPtr args) => Delegates.PyObject_CallObject(pointer, args);


        internal static int PyObject_RichCompareBool(IntPtr value1, IntPtr value2, int opid) => Delegates.PyObject_RichCompareBool(value1, value2, opid);

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


        internal static int PyObject_IsInstance(IntPtr ob, IntPtr type) => Delegates.PyObject_IsInstance(ob, type);


        internal static int PyObject_IsSubclass(IntPtr ob, IntPtr type) => Delegates.PyObject_IsSubclass(ob, type);


        internal static int PyCallable_Check(IntPtr pointer) => Delegates.PyCallable_Check(pointer);


        internal static int PyObject_IsTrue(IntPtr pointer) => PyObject_IsTrue(new BorrowedReference(pointer));
        internal static int PyObject_IsTrue(BorrowedReference pointer) => Delegates.PyObject_IsTrue(pointer);


        internal static int PyObject_Not(IntPtr pointer) => Delegates.PyObject_Not(pointer);

        internal static long PyObject_Size(IntPtr pointer)
        {
            return (long)_PyObject_Size(pointer);
        }


        private static IntPtr _PyObject_Size(IntPtr pointer) => Delegates._PyObject_Size(pointer);


        internal static nint PyObject_Hash(IntPtr op) => Delegates.PyObject_Hash(op);


        internal static IntPtr PyObject_Repr(IntPtr pointer) => Delegates.PyObject_Repr(pointer);


        internal static IntPtr PyObject_Str(IntPtr pointer) => Delegates.PyObject_Str(pointer);


        internal static IntPtr PyObject_Unicode(IntPtr pointer) => Delegates.PyObject_Unicode(pointer);


        internal static IntPtr PyObject_Dir(IntPtr pointer) => Delegates.PyObject_Dir(pointer);

#if PYTHON_WITH_PYDEBUG
        [DllImport(_PythonDll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void _Py_NewReference(IntPtr ob);
#endif

        //====================================================================
        // Python buffer API
        //====================================================================


        internal static int PyObject_GetBuffer(IntPtr exporter, ref Py_buffer view, int flags) => Delegates.PyObject_GetBuffer(exporter, ref view, flags);


        internal static void PyBuffer_Release(ref Py_buffer view) => Delegates.PyBuffer_Release(ref view);


        internal static IntPtr PyBuffer_SizeFromFormat(string format)
        {
            using var formatPtr = new StrPtr(format, Encoding.ASCII);
            return Delegates.PyBuffer_SizeFromFormat(formatPtr);
        }

        internal static int PyBuffer_IsContiguous(ref Py_buffer view, char order) => Delegates.PyBuffer_IsContiguous(ref view, order);


        internal static IntPtr PyBuffer_GetPointer(ref Py_buffer view, IntPtr[] indices) => Delegates.PyBuffer_GetPointer(ref view, indices);


        internal static int PyBuffer_FromContiguous(ref Py_buffer view, IntPtr buf, IntPtr len, char fort) => Delegates.PyBuffer_FromContiguous(ref view, buf, len, fort);


        internal static int PyBuffer_ToContiguous(IntPtr buf, ref Py_buffer src, IntPtr len, char order) => Delegates.PyBuffer_ToContiguous(buf, ref src, len, order);


        internal static void PyBuffer_FillContiguousStrides(int ndims, IntPtr shape, IntPtr strides, int itemsize, char order) => Delegates.PyBuffer_FillContiguousStrides(ndims, shape, strides, itemsize, order);


        internal static int PyBuffer_FillInfo(ref Py_buffer view, IntPtr exporter, IntPtr buf, IntPtr len, int _readonly, int flags) => Delegates.PyBuffer_FillInfo(ref view, exporter, buf, len, _readonly, flags);

        //====================================================================
        // Python number API
        //====================================================================


        internal static IntPtr PyNumber_Int(IntPtr ob) => Delegates.PyNumber_Int(ob);


        internal static IntPtr PyNumber_Long(IntPtr ob) => Delegates.PyNumber_Long(ob);


        internal static IntPtr PyNumber_Float(IntPtr ob) => Delegates.PyNumber_Float(ob);


        internal static bool PyNumber_Check(IntPtr ob) => Delegates.PyNumber_Check(ob);

        internal static bool PyInt_Check(BorrowedReference ob)
            => PyObject_TypeCheck(ob, new BorrowedReference(PyIntType));
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


        private static IntPtr PyInt_FromLong(IntPtr value) => Delegates.PyInt_FromLong(value);


        internal static int PyInt_AsLong(IntPtr value) => Delegates.PyInt_AsLong(value);


        internal static bool PyLong_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }


        internal static IntPtr PyLong_FromLong(long value) => Delegates.PyLong_FromLong(value);


        internal static IntPtr PyLong_FromUnsignedLong32(uint value) => Delegates.PyLong_FromUnsignedLong32(value);


        internal static IntPtr PyLong_FromUnsignedLong64(ulong value) => Delegates.PyLong_FromUnsignedLong64(value);

        internal static IntPtr PyLong_FromUnsignedLong(object value)
        {
            if (Is32Bit || IsWindows)
                return PyLong_FromUnsignedLong32(Convert.ToUInt32(value));
            else
                return PyLong_FromUnsignedLong64(Convert.ToUInt64(value));
        }


        internal static IntPtr PyLong_FromDouble(double value) => Delegates.PyLong_FromDouble(value);


        internal static IntPtr PyLong_FromLongLong(long value) => Delegates.PyLong_FromLongLong(value);


        internal static IntPtr PyLong_FromUnsignedLongLong(ulong value) => Delegates.PyLong_FromUnsignedLongLong(value);


        internal static IntPtr PyLong_FromString(string value, IntPtr end, int radix)
        {
            using var valPtr = new StrPtr(value, Encoding.UTF8);
            return Delegates.PyLong_FromString(valPtr, end, radix);
        }



        internal static nuint PyLong_AsUnsignedSize_t(IntPtr value) => Delegates.PyLong_AsUnsignedSize_t(value);

        internal static nint PyLong_AsSignedSize_t(IntPtr value) => Delegates.PyLong_AsSignedSize_t(new BorrowedReference(value));

        internal static nint PyLong_AsSignedSize_t(BorrowedReference value) => Delegates.PyLong_AsSignedSize_t(value);

        /// <summary>
        /// This function is a rename of PyLong_AsLongLong, which has a commonly undesired
        /// behavior to convert everything (including floats) to integer type, before returning
        /// the value as <see cref="Int64"/>.
        ///
        /// <para>In most cases you need to check that value is an instance of PyLongObject
        /// before using this function using <see cref="PyLong_Check(IntPtr)"/>.</para>
        /// </summary>

        internal static long PyExplicitlyConvertToInt64(IntPtr value) => Delegates.PyExplicitlyConvertToInt64(value);

        internal static ulong PyLong_AsUnsignedLongLong(IntPtr value) => Delegates.PyLong_AsUnsignedLongLong(value);

        internal static bool PyFloat_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyFloatType;
        }

        /// <summary>
        /// Return value: New reference.
        /// Create a Python integer from the pointer p. The pointer value can be retrieved from the resulting value using PyLong_AsVoidPtr().
        /// </summary>
        internal static NewReference PyLong_FromVoidPtr(IntPtr p) => Delegates.PyLong_FromVoidPtr(p);

        /// <summary>
        /// Convert a Python integer pylong to a C void pointer. If pylong cannot be converted, an OverflowError will be raised. This is only assured to produce a usable void pointer for values created with PyLong_FromVoidPtr().
        /// </summary>

        internal static IntPtr PyLong_AsVoidPtr(BorrowedReference ob) => Delegates.PyLong_AsVoidPtr(ob);


        internal static IntPtr PyFloat_FromDouble(double value) => Delegates.PyFloat_FromDouble(value);


        internal static NewReference PyFloat_FromString(BorrowedReference value) => Delegates.PyFloat_FromString(value);


        internal static double PyFloat_AsDouble(IntPtr ob) => Delegates.PyFloat_AsDouble(ob);


        internal static IntPtr PyNumber_Add(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Add(o1, o2);


        internal static IntPtr PyNumber_Subtract(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Subtract(o1, o2);


        internal static IntPtr PyNumber_Multiply(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Multiply(o1, o2);


        internal static IntPtr PyNumber_TrueDivide(IntPtr o1, IntPtr o2) => Delegates.PyNumber_TrueDivide(o1, o2);


        internal static IntPtr PyNumber_And(IntPtr o1, IntPtr o2) => Delegates.PyNumber_And(o1, o2);


        internal static IntPtr PyNumber_Xor(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Xor(o1, o2);


        internal static IntPtr PyNumber_Or(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Or(o1, o2);


        internal static IntPtr PyNumber_Lshift(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Lshift(o1, o2);


        internal static IntPtr PyNumber_Rshift(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Rshift(o1, o2);


        internal static IntPtr PyNumber_Power(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Power(o1, o2);


        internal static IntPtr PyNumber_Remainder(IntPtr o1, IntPtr o2) => Delegates.PyNumber_Remainder(o1, o2);


        internal static IntPtr PyNumber_InPlaceAdd(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceAdd(o1, o2);


        internal static IntPtr PyNumber_InPlaceSubtract(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceSubtract(o1, o2);


        internal static IntPtr PyNumber_InPlaceMultiply(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceMultiply(o1, o2);


        internal static IntPtr PyNumber_InPlaceTrueDivide(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceTrueDivide(o1, o2);


        internal static IntPtr PyNumber_InPlaceAnd(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceAnd(o1, o2);


        internal static IntPtr PyNumber_InPlaceXor(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceXor(o1, o2);


        internal static IntPtr PyNumber_InPlaceOr(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceOr(o1, o2);


        internal static IntPtr PyNumber_InPlaceLshift(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceLshift(o1, o2);


        internal static IntPtr PyNumber_InPlaceRshift(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceRshift(o1, o2);


        internal static IntPtr PyNumber_InPlacePower(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlacePower(o1, o2);


        internal static IntPtr PyNumber_InPlaceRemainder(IntPtr o1, IntPtr o2) => Delegates.PyNumber_InPlaceRemainder(o1, o2);


        internal static IntPtr PyNumber_Negative(IntPtr o1) => Delegates.PyNumber_Negative(o1);


        internal static IntPtr PyNumber_Positive(IntPtr o1) => Delegates.PyNumber_Positive(o1);


        internal static IntPtr PyNumber_Invert(IntPtr o1) => Delegates.PyNumber_Invert(o1);


        //====================================================================
        // Python sequence API
        //====================================================================


        internal static bool PySequence_Check(IntPtr pointer) => Delegates.PySequence_Check(pointer);

        internal static NewReference PySequence_GetItem(BorrowedReference pointer, nint index) => Delegates.PySequence_GetItem(pointer, index);

        internal static int PySequence_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PySequence_SetItem(pointer, new IntPtr(index), value);
        }


        private static int PySequence_SetItem(IntPtr pointer, IntPtr index, IntPtr value) => Delegates.PySequence_SetItem(pointer, index, value);

        internal static int PySequence_DelItem(IntPtr pointer, long index)
        {
            return PySequence_DelItem(pointer, new IntPtr(index));
        }


        private static int PySequence_DelItem(IntPtr pointer, IntPtr index) => Delegates.PySequence_DelItem(pointer, index);

        internal static IntPtr PySequence_GetSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_GetSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }


        private static IntPtr PySequence_GetSlice(IntPtr pointer, IntPtr i1, IntPtr i2) => Delegates.PySequence_GetSlice(pointer, i1, i2);

        internal static int PySequence_SetSlice(IntPtr pointer, long i1, long i2, IntPtr v)
        {
            return PySequence_SetSlice(pointer, new IntPtr(i1), new IntPtr(i2), v);
        }


        private static int PySequence_SetSlice(IntPtr pointer, IntPtr i1, IntPtr i2, IntPtr v) => Delegates.PySequence_SetSlice(pointer, i1, i2, v);

        internal static int PySequence_DelSlice(IntPtr pointer, long i1, long i2)
        {
            return PySequence_DelSlice(pointer, new IntPtr(i1), new IntPtr(i2));
        }


        private static int PySequence_DelSlice(IntPtr pointer, IntPtr i1, IntPtr i2) => Delegates.PySequence_DelSlice(pointer, i1, i2);

        [Obsolete]
        internal static nint PySequence_Size(IntPtr pointer) => PySequence_Size(new BorrowedReference(pointer));
        internal static nint PySequence_Size(BorrowedReference pointer) => Delegates.PySequence_Size(pointer);


        internal static int PySequence_Contains(IntPtr pointer, IntPtr item) => Delegates.PySequence_Contains(pointer, item);


        internal static IntPtr PySequence_Concat(IntPtr pointer, IntPtr other) => Delegates.PySequence_Concat(pointer, other);

        internal static IntPtr PySequence_Repeat(IntPtr pointer, long count)
        {
            return PySequence_Repeat(pointer, new IntPtr(count));
        }


        private static IntPtr PySequence_Repeat(IntPtr pointer, IntPtr count) => Delegates.PySequence_Repeat(pointer, count);


        internal static int PySequence_Index(IntPtr pointer, IntPtr item) => Delegates.PySequence_Index(pointer, item);

        internal static long PySequence_Count(IntPtr pointer, IntPtr value)
        {
            return (long)_PySequence_Count(pointer, value);
        }


        private static IntPtr _PySequence_Count(IntPtr pointer, IntPtr value) => Delegates._PySequence_Count(pointer, value);


        internal static IntPtr PySequence_Tuple(IntPtr pointer) => Delegates.PySequence_Tuple(pointer);


        internal static IntPtr PySequence_List(IntPtr pointer) => Delegates.PySequence_List(pointer);


        //====================================================================
        // Python string API
        //====================================================================
        internal static bool IsStringType(BorrowedReference op)
        {
            BorrowedReference t = PyObject_TYPE(op);
            return (t == new BorrowedReference(PyStringType))
                || (t == new BorrowedReference(PyUnicodeType));
        }
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
            fixed(char* ptr = value)
                return PyUnicode_FromKindAndData(2, (IntPtr)ptr, value.Length);
        }


        internal static IntPtr EmptyPyBytes()
        {
            byte* bytes = stackalloc byte[1];
            bytes[0] = 0;
            return Delegates.PyBytes_FromString((IntPtr)bytes);
        }

        internal static long PyBytes_Size(IntPtr op)
        {
            return (long)_PyBytes_Size(op);
        }


        private static IntPtr _PyBytes_Size(IntPtr op) => Delegates._PyBytes_Size(op);

        internal static IntPtr PyBytes_AS_STRING(IntPtr ob)
        {
            return ob + BytesOffset.ob_sval;
        }


        internal static IntPtr PyUnicode_FromStringAndSize(IntPtr value, long size)
        {
            return PyUnicode_FromStringAndSize(value, new IntPtr(size));
        }


        private static IntPtr PyUnicode_FromStringAndSize(IntPtr value, IntPtr size) => Delegates.PyUnicode_FromStringAndSize(value, size);


        internal static IntPtr PyUnicode_AsUTF8(IntPtr unicode) => Delegates.PyUnicode_AsUTF8(unicode);

        internal static bool PyUnicode_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyUnicodeType;
        }


        internal static IntPtr PyUnicode_FromObject(IntPtr ob) => Delegates.PyUnicode_FromObject(ob);


        internal static IntPtr PyUnicode_FromEncodedObject(IntPtr ob, IntPtr enc, IntPtr err) => Delegates.PyUnicode_FromEncodedObject(ob, enc, err);

        internal static IntPtr PyUnicode_FromKindAndData(int kind, IntPtr s, long size)
        {
            return PyUnicode_FromKindAndData(kind, s, new IntPtr(size));
        }


        private static IntPtr PyUnicode_FromKindAndData(int kind, IntPtr s, IntPtr size)
            => Delegates.PyUnicode_FromKindAndData(kind, s, size);

        internal static IntPtr PyUnicode_FromUnicode(string s, long size)
        {
            fixed(char* ptr = s)
                return PyUnicode_FromKindAndData(2, (IntPtr)ptr, size);
        }


        internal static int PyUnicode_GetMax() => Delegates.PyUnicode_GetMax();

        internal static long PyUnicode_GetSize(IntPtr ob)
        {
            return (long)_PyUnicode_GetSize(ob);
        }


        private static IntPtr _PyUnicode_GetSize(IntPtr ob) => Delegates._PyUnicode_GetSize(ob);


        internal static IntPtr PyUnicode_AsUnicode(IntPtr ob) => Delegates.PyUnicode_AsUnicode(ob);
        internal static NewReference PyUnicode_AsUTF16String(BorrowedReference ob) => Delegates.PyUnicode_AsUTF16String(ob);



        internal static IntPtr PyUnicode_FromOrdinal(int c) => Delegates.PyUnicode_FromOrdinal(c);

        internal static IntPtr PyUnicode_FromString(string s)
        {
            return PyUnicode_FromUnicode(s, s.Length);
        }


        internal static IntPtr PyUnicode_InternFromString(string s)
        {
            using var ptr = new StrPtr(s, Encoding.UTF8);
            return Delegates.PyUnicode_InternFromString(ptr);
        }

        internal static int PyUnicode_Compare(IntPtr left, IntPtr right) => Delegates.PyUnicode_Compare(left, right);

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
                using var p = PyUnicode_AsUTF16String(new BorrowedReference(op));
                int length = (int)PyUnicode_GetSize(op);
                char* codePoints = (char*)PyBytes_AS_STRING(p.DangerousGetAddress());
                return new string(codePoints,
                                  startIndex: 1, // skip BOM
                                  length: length);
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


        internal static IntPtr PyDict_New() => Delegates.PyDict_New();


        internal static int PyDict_Next(IntPtr p, out IntPtr ppos, out IntPtr pkey, out IntPtr pvalue) => Delegates.PyDict_Next(p, out ppos, out pkey, out pvalue);


        internal static IntPtr PyDictProxy_New(IntPtr dict) => Delegates.PyDictProxy_New(dict);

        /// <summary>
        /// Return value: Borrowed reference.
        /// Return NULL if the key is not present, but without setting an exception.
        /// </summary>
        internal static IntPtr PyDict_GetItem(IntPtr pointer, IntPtr key)
            => Delegates.PyDict_GetItem(new BorrowedReference(pointer), new BorrowedReference(key))
                .DangerousGetAddressOrNull();
        /// <summary>
        /// Return NULL if the key is not present, but without setting an exception.
        /// </summary>
        internal static BorrowedReference PyDict_GetItem(BorrowedReference pointer, BorrowedReference key) => Delegates.PyDict_GetItem(pointer, key);

        internal static BorrowedReference PyDict_GetItemString(BorrowedReference pointer, string key)
        {
            using var keyStr = new StrPtr(key, Encoding.UTF8);
            return Delegates.PyDict_GetItemString(pointer, keyStr);
        }

        internal static BorrowedReference PyDict_GetItemWithError(BorrowedReference pointer, BorrowedReference key) => Delegates.PyDict_GetItemWithError(pointer, key);

        /// <summary>
        /// Return 0 on success or -1 on failure.
        /// </summary>
        [Obsolete]
        internal static int PyDict_SetItem(IntPtr dict, IntPtr key, IntPtr value) => Delegates.PyDict_SetItem(new BorrowedReference(dict), new BorrowedReference(key), new BorrowedReference(value));
        /// <summary>
        /// Return 0 on success or -1 on failure.
        /// </summary>
        internal static int PyDict_SetItem(BorrowedReference dict, IntPtr key, BorrowedReference value) => Delegates.PyDict_SetItem(dict, new BorrowedReference(key), value);
        /// <summary>
        /// Return 0 on success or -1 on failure.
        /// </summary>
        internal static int PyDict_SetItem(BorrowedReference dict, BorrowedReference key, BorrowedReference value) => Delegates.PyDict_SetItem(dict, key, value);

        /// <summary>
        ///  Return 0 on success or -1 on failure.
        /// </summary>
        internal static int PyDict_SetItemString(IntPtr dict, string key, IntPtr value)
            => PyDict_SetItemString(new BorrowedReference(dict), key, new BorrowedReference(value));

        /// <summary>
        ///  Return 0 on success or -1 on failure.
        /// </summary>
        internal static int PyDict_SetItemString(BorrowedReference dict, string key, BorrowedReference value)
        {
            using var keyPtr = new StrPtr(key, Encoding.UTF8);
            return Delegates.PyDict_SetItemString(dict, keyPtr, value);
        }

        internal static int PyDict_DelItem(BorrowedReference pointer, BorrowedReference key) => Delegates.PyDict_DelItem(pointer, key);


        internal static int PyDict_DelItemString(BorrowedReference pointer, string key)
        {
            using var keyPtr = new StrPtr(key, Encoding.UTF8);
            return Delegates.PyDict_DelItemString(pointer, keyPtr);
        }

        internal static int PyMapping_HasKey(IntPtr pointer, IntPtr key) => Delegates.PyMapping_HasKey(pointer, key);


        [Obsolete]
        internal static IntPtr PyDict_Keys(IntPtr pointer)
            => Delegates.PyDict_Keys(new BorrowedReference(pointer))
                        .DangerousMoveToPointerOrNull();
        internal static NewReference PyDict_Keys(BorrowedReference pointer) => Delegates.PyDict_Keys(pointer);


        internal static IntPtr PyDict_Values(IntPtr pointer) => Delegates.PyDict_Values(pointer);


        internal static NewReference PyDict_Items(BorrowedReference pointer) => Delegates.PyDict_Items(pointer);


        internal static IntPtr PyDict_Copy(IntPtr pointer) => Delegates.PyDict_Copy(pointer);


        internal static int PyDict_Update(BorrowedReference pointer, BorrowedReference other) => Delegates.PyDict_Update(pointer, other);


        internal static void PyDict_Clear(IntPtr pointer) => Delegates.PyDict_Clear(pointer);

        internal static long PyDict_Size(IntPtr pointer)
        {
            return (long)_PyDict_Size(pointer);
        }


        internal static IntPtr _PyDict_Size(IntPtr pointer) => Delegates._PyDict_Size(pointer);


        internal static NewReference PySet_New(BorrowedReference iterable) => Delegates.PySet_New(iterable);


        internal static int PySet_Add(BorrowedReference set, BorrowedReference key) => Delegates.PySet_Add(set, key);

        /// <summary>
        /// Return 1 if found, 0 if not found, and -1 if an error is encountered.
        /// </summary>

        internal static int PySet_Contains(BorrowedReference anyset, BorrowedReference key) => Delegates.PySet_Contains(anyset, key);

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


        private static IntPtr PyList_New(IntPtr size) => Delegates.PyList_New(size);


        internal static IntPtr PyList_AsTuple(IntPtr pointer) => Delegates.PyList_AsTuple(pointer);

        internal static BorrowedReference PyList_GetItem(BorrowedReference pointer, long index)
        {
            return PyList_GetItem(pointer, new IntPtr(index));
        }


        private static BorrowedReference PyList_GetItem(BorrowedReference pointer, IntPtr index) => Delegates.PyList_GetItem(pointer, index);

        internal static int PyList_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PyList_SetItem(pointer, new IntPtr(index), value);
        }


        private static int PyList_SetItem(IntPtr pointer, IntPtr index, IntPtr value) => Delegates.PyList_SetItem(pointer, index, value);

        internal static int PyList_Insert(BorrowedReference pointer, long index, IntPtr value)
        {
            return PyList_Insert(pointer, new IntPtr(index), value);
        }


        private static int PyList_Insert(BorrowedReference pointer, IntPtr index, IntPtr value) => Delegates.PyList_Insert(pointer, index, value);


        internal static int PyList_Append(BorrowedReference pointer, IntPtr value) => Delegates.PyList_Append(pointer, value);


        internal static int PyList_Reverse(BorrowedReference pointer) => Delegates.PyList_Reverse(pointer);


        internal static int PyList_Sort(BorrowedReference pointer) => Delegates.PyList_Sort(pointer);

        internal static IntPtr PyList_GetSlice(IntPtr pointer, long start, long end)
        {
            return PyList_GetSlice(pointer, new IntPtr(start), new IntPtr(end));
        }


        private static IntPtr PyList_GetSlice(IntPtr pointer, IntPtr start, IntPtr end) => Delegates.PyList_GetSlice(pointer, start, end);

        internal static int PyList_SetSlice(IntPtr pointer, long start, long end, IntPtr value)
        {
            return PyList_SetSlice(pointer, new IntPtr(start), new IntPtr(end), value);
        }


        private static int PyList_SetSlice(IntPtr pointer, IntPtr start, IntPtr end, IntPtr value) => Delegates.PyList_SetSlice(pointer, start, end, value);


        internal static nint PyList_Size(BorrowedReference pointer) => Delegates.PyList_Size(pointer);

        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == new BorrowedReference(PyTupleType);
        }
        internal static bool PyTuple_Check(IntPtr ob)
        {
            return PyObject_TYPE(ob) == PyTupleType;
        }

        internal static IntPtr PyTuple_New(long size)
        {
            return PyTuple_New(new IntPtr(size));
        }


        private static IntPtr PyTuple_New(IntPtr size) => Delegates.PyTuple_New(size);

        internal static BorrowedReference PyTuple_GetItem(BorrowedReference pointer, long index)
            => PyTuple_GetItem(pointer, new IntPtr(index));
        internal static IntPtr PyTuple_GetItem(IntPtr pointer, long index)
        {
            return PyTuple_GetItem(new BorrowedReference(pointer), new IntPtr(index))
                .DangerousGetAddressOrNull();
        }


        private static BorrowedReference PyTuple_GetItem(BorrowedReference pointer, IntPtr index) => Delegates.PyTuple_GetItem(pointer, index);

        internal static int PyTuple_SetItem(IntPtr pointer, long index, IntPtr value)
        {
            return PyTuple_SetItem(pointer, new IntPtr(index), value);
        }


        private static int PyTuple_SetItem(IntPtr pointer, IntPtr index, IntPtr value) => Delegates.PyTuple_SetItem(pointer, index, value);

        internal static IntPtr PyTuple_GetSlice(IntPtr pointer, long start, long end)
        {
            return PyTuple_GetSlice(pointer, new IntPtr(start), new IntPtr(end));
        }


        private static IntPtr PyTuple_GetSlice(IntPtr pointer, IntPtr start, IntPtr end) => Delegates.PyTuple_GetSlice(pointer, start, end);


        internal static nint PyTuple_Size(IntPtr pointer) => PyTuple_Size(new BorrowedReference(pointer));
        internal static nint PyTuple_Size(BorrowedReference pointer) => Delegates.PyTuple_Size(pointer);


        //====================================================================
        // Python iterator API
        //====================================================================

        internal static bool PyIter_Check(IntPtr pointer)
        {
            var ob_type = Marshal.ReadIntPtr(pointer, ObjectOffset.ob_type);
            IntPtr tp_iternext = Marshal.ReadIntPtr(ob_type, TypeOffset.tp_iternext);
            return tp_iternext != IntPtr.Zero && tp_iternext != _PyObject_NextNotImplemented;
        }


        internal static IntPtr PyIter_Next(IntPtr pointer)
            => Delegates.PyIter_Next(new BorrowedReference(pointer)).DangerousMoveToPointerOrNull();
        internal static NewReference PyIter_Next(BorrowedReference pointer) => Delegates.PyIter_Next(pointer);


        //====================================================================
        // Python module API
        //====================================================================


        internal static NewReference PyModule_New(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyModule_New(namePtr);
        }

        internal static string PyModule_GetName(IntPtr module)
            => Delegates.PyModule_GetName(module).ToString(Encoding.UTF8);

        internal static BorrowedReference PyModule_GetDict(BorrowedReference module) => Delegates.PyModule_GetDict(module);


        internal static string PyModule_GetFilename(IntPtr module)
            => Delegates.PyModule_GetFilename(module).ToString(Encoding.UTF8);

#if PYTHON_WITH_PYDEBUG
        [DllImport(_PythonDll, EntryPoint = "PyModule_Create2TraceRefs", CallingConvention = CallingConvention.Cdecl)]
#else

#endif
        internal static IntPtr PyModule_Create2(IntPtr module, int apiver) => Delegates.PyModule_Create2(module, apiver);


        internal static IntPtr PyImport_Import(IntPtr name) => Delegates.PyImport_Import(name);

        /// <summary>
        /// Return value: New reference.
        /// </summary>

        internal static IntPtr PyImport_ImportModule(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyImport_ImportModule(namePtr);
        }

        internal static IntPtr PyImport_ReloadModule(IntPtr module) => Delegates.PyImport_ReloadModule(module);


        internal static BorrowedReference PyImport_AddModule(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyImport_AddModule(namePtr);
        }

        internal static BorrowedReference PyImport_GetModuleDict() => Delegates.PyImport_GetModuleDict();


        internal static void PySys_SetArgvEx(int argc, string[] argv, int updatepath)
        {
            var marshaler = StrArrayMarshaler.GetInstance(null);
            var argvPtr = marshaler.MarshalManagedToNative(argv);
            try
            {
                Delegates.PySys_SetArgvEx(argc, argvPtr, updatepath);
            }
            finally
            {
                marshaler.CleanUpNativeData(argvPtr);
            }
        }

        /// <summary>
        /// Return value: Borrowed reference.
        /// Return the object name from the sys module or NULL if it does not exist, without setting an exception.
        /// </summary>

        internal static BorrowedReference PySys_GetObject(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PySys_GetObject(namePtr);
        }

        internal static int PySys_SetObject(string name, BorrowedReference ob)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PySys_SetObject(namePtr, ob);
        }


        //====================================================================
        // Python type object API
        //====================================================================
        internal static bool PyType_Check(IntPtr ob)
        {
            return PyObject_TypeCheck(ob, PyTypeType);
        }


        internal static void PyType_Modified(IntPtr type) => Delegates.PyType_Modified(type);
        internal static bool PyType_IsSubtype(BorrowedReference t1, IntPtr ofType)
            => PyType_IsSubtype(t1, new BorrowedReference(ofType));
        internal static bool PyType_IsSubtype(BorrowedReference t1, BorrowedReference t2) => Delegates.PyType_IsSubtype(t1, t2);

        internal static bool PyObject_TypeCheck(IntPtr ob, IntPtr tp)
            => PyObject_TypeCheck(new BorrowedReference(ob), new BorrowedReference(tp));
        internal static bool PyObject_TypeCheck(BorrowedReference ob, BorrowedReference tp)
        {
            BorrowedReference t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        internal static bool PyType_IsSameAsOrSubtype(BorrowedReference type, IntPtr ofType)
            => PyType_IsSameAsOrSubtype(type, new BorrowedReference(ofType));
        internal static bool PyType_IsSameAsOrSubtype(BorrowedReference type, BorrowedReference ofType)
        {
            return (type == ofType) || PyType_IsSubtype(type, ofType);
        }


        internal static IntPtr PyType_GenericNew(IntPtr type, IntPtr args, IntPtr kw) => Delegates.PyType_GenericNew(type, args, kw);

        internal static IntPtr PyType_GenericAlloc(IntPtr type, long n)
        {
            return PyType_GenericAlloc(type, new IntPtr(n));
        }


        private static IntPtr PyType_GenericAlloc(IntPtr type, IntPtr n) => Delegates.PyType_GenericAlloc(type, n);

        /// <summary>
        /// Finalize a type object. This should be called on all type objects to finish their initialization. This function is responsible for adding inherited slots from a type’s base class. Return 0 on success, or return -1 and sets an exception on error.
        /// </summary>

        internal static int PyType_Ready(IntPtr type) => Delegates.PyType_Ready(type);


        internal static IntPtr _PyType_Lookup(IntPtr type, IntPtr name) => Delegates._PyType_Lookup(type, name);


        internal static IntPtr PyObject_GenericGetAttr(IntPtr obj, IntPtr name) => Delegates.PyObject_GenericGetAttr(obj, name);


        internal static int PyObject_GenericSetAttr(IntPtr obj, IntPtr name, IntPtr value) => Delegates.PyObject_GenericSetAttr(obj, name, value);


        internal static BorrowedReference* _PyObject_GetDictPtr(BorrowedReference obj) => Delegates._PyObject_GetDictPtr(obj);


        internal static void PyObject_GC_Del(IntPtr tp) => Delegates.PyObject_GC_Del(tp);


        internal static void PyObject_GC_Track(IntPtr tp) => Delegates.PyObject_GC_Track(tp);


        internal static void PyObject_GC_UnTrack(IntPtr tp) => Delegates.PyObject_GC_UnTrack(tp);


        internal static void _PyObject_Dump(IntPtr ob) => Delegates._PyObject_Dump(ob);

        //====================================================================
        // Python memory API
        //====================================================================

        internal static IntPtr PyMem_Malloc(long size)
        {
            return PyMem_Malloc(new IntPtr(size));
        }


        private static IntPtr PyMem_Malloc(IntPtr size) => Delegates.PyMem_Malloc(size);

        internal static IntPtr PyMem_Realloc(IntPtr ptr, long size)
        {
            return PyMem_Realloc(ptr, new IntPtr(size));
        }


        private static IntPtr PyMem_Realloc(IntPtr ptr, IntPtr size) => Delegates.PyMem_Realloc(ptr, size);


        internal static void PyMem_Free(IntPtr ptr) => Delegates.PyMem_Free(ptr);


        //====================================================================
        // Python exception API
        //====================================================================


        internal static void PyErr_SetString(IntPtr ob, string message)
        {
            using var msgPtr = new StrPtr(message, Encoding.UTF8);
            Delegates.PyErr_SetString(ob, msgPtr);
        }

        internal static void PyErr_SetObject(BorrowedReference type, BorrowedReference exceptionObject) => Delegates.PyErr_SetObject(type, exceptionObject);


        internal static IntPtr PyErr_SetFromErrno(IntPtr ob) => Delegates.PyErr_SetFromErrno(ob);


        internal static void PyErr_SetNone(IntPtr ob) => Delegates.PyErr_SetNone(ob);


        internal static int PyErr_ExceptionMatches(IntPtr exception) => Delegates.PyErr_ExceptionMatches(exception);


        internal static int PyErr_GivenExceptionMatches(IntPtr ob, IntPtr val) => Delegates.PyErr_GivenExceptionMatches(ob, val);


        internal static void PyErr_NormalizeException(ref IntPtr ob, ref IntPtr val, ref IntPtr tb) => Delegates.PyErr_NormalizeException(ref ob, ref val, ref tb);


        internal static IntPtr PyErr_Occurred() => Delegates.PyErr_Occurred();


        internal static void PyErr_Fetch(out IntPtr ob, out IntPtr val, out IntPtr tb) => Delegates.PyErr_Fetch(out ob, out val, out tb);


        internal static void PyErr_Restore(IntPtr ob, IntPtr val, IntPtr tb) => Delegates.PyErr_Restore(ob, val, tb);


        internal static void PyErr_Clear() => Delegates.PyErr_Clear();


        internal static void PyErr_Print() => Delegates.PyErr_Print();

        /// <summary>
        /// Set the cause associated with the exception to cause. Use NULL to clear it. There is no type check to make sure that cause is either an exception instance or None. This steals a reference to cause.
        /// </summary>

        internal static void PyException_SetCause(IntPtr ex, IntPtr cause) => Delegates.PyException_SetCause(ex, cause);

        //====================================================================
        // Cell API
        //====================================================================


        internal static NewReference PyCell_Get(BorrowedReference cell) => Delegates.PyCell_Get(cell);


        internal static int PyCell_Set(BorrowedReference cell, IntPtr value) => Delegates.PyCell_Set(cell, value);

        //====================================================================
        // Python GC API
        //====================================================================

        internal const int _PyGC_REFS_SHIFT = 1;
        internal const long _PyGC_REFS_UNTRACKED = -2;
        internal const long _PyGC_REFS_REACHABLE = -3;
        internal const long _PyGC_REFS_TENTATIVELY_UNREACHABLE = -4;



        internal static IntPtr PyGC_Collect() => Delegates.PyGC_Collect();

        internal static IntPtr _Py_AS_GC(BorrowedReference ob)
        {
            // XXX: PyGC_Head has a force alignment depend on platform.
            // See PyGC_Head in objimpl.h for more details.
            return ob.DangerousGetAddress() - (Is32Bit ?  16 : 24);
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

        internal static IntPtr _PyGC_REFS(BorrowedReference ob)
        {
            return _PyGCHead_REFS(_Py_AS_GC(ob));
        }

        internal static bool _PyObject_GC_IS_TRACKED(BorrowedReference ob)
            => (long)_PyGC_REFS(ob) != _PyGC_REFS_UNTRACKED;

        internal static void Py_CLEAR(ref IntPtr ob)
        {
            XDecref(ob);
            ob = IntPtr.Zero;
        }

        //====================================================================
        // Python Capsules API
        //====================================================================


        internal static NewReference PyCapsule_New(IntPtr pointer, IntPtr name, IntPtr destructor)
            => Delegates.PyCapsule_New(pointer, name, destructor);

        internal static IntPtr PyCapsule_GetPointer(BorrowedReference capsule, IntPtr name)
        {
            return Delegates.PyCapsule_GetPointer(capsule, name);
        }

        internal static int PyCapsule_SetPointer(BorrowedReference capsule, IntPtr pointer) => Delegates.PyCapsule_SetPointer(capsule, pointer);

        //====================================================================
        // Miscellaneous
        //====================================================================


        internal static IntPtr PyMethod_Self(IntPtr ob) => Delegates.PyMethod_Self(ob);


        internal static IntPtr PyMethod_Function(IntPtr ob) => Delegates.PyMethod_Function(ob);


        internal static int Py_AddPendingCall(IntPtr func, IntPtr arg) => Delegates.Py_AddPendingCall(func, arg);


        internal static int PyThreadState_SetAsyncExcLLP64(uint id, IntPtr exc) => Delegates.PyThreadState_SetAsyncExcLLP64(id, exc);

        internal static int PyThreadState_SetAsyncExcLP64(ulong id, IntPtr exc) => Delegates.PyThreadState_SetAsyncExcLP64(id, exc);


        internal static int Py_MakePendingCalls() => Delegates.Py_MakePendingCalls();

        internal static void SetNoSiteFlag()
        {
            var loader = LibraryLoader.Instance;
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
            return PyImport_Import(PyIdentifier.builtins);
        }

        private static class Delegates
        {
            static readonly ILibraryLoader libraryLoader = LibraryLoader.Instance;

            static Delegates()
            {
                PyDictProxy_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyDictProxy_New), GetUnmanagedDll(_PythonDll));
                Py_IncRef = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_IncRef), GetUnmanagedDll(_PythonDll));
                Py_DecRef = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_DecRef), GetUnmanagedDll(_PythonDll));
                Py_Initialize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Initialize), GetUnmanagedDll(_PythonDll));
                Py_InitializeEx = (delegate* unmanaged[Cdecl]<int, void>)GetFunctionByName(nameof(Py_InitializeEx), GetUnmanagedDll(_PythonDll));
                Py_IsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(Py_IsInitialized), GetUnmanagedDll(_PythonDll));
                Py_Finalize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Finalize), GetUnmanagedDll(_PythonDll));
                Py_NewInterpreter = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_NewInterpreter), GetUnmanagedDll(_PythonDll));
                Py_EndInterpreter = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_EndInterpreter), GetUnmanagedDll(_PythonDll));
                PyThreadState_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyThreadState_New), GetUnmanagedDll(_PythonDll));
                PyThreadState_Get = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyThreadState_Get), GetUnmanagedDll(_PythonDll));
                _PyThreadState_UncheckedGet = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(_PyThreadState_UncheckedGet), GetUnmanagedDll(_PythonDll));
                PyThread_get_key_value = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyThread_get_key_value), GetUnmanagedDll(_PythonDll));
                PyThread_get_thread_ident = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyThread_get_thread_ident), GetUnmanagedDll(_PythonDll));
                PyThread_set_key_value = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyThread_set_key_value), GetUnmanagedDll(_PythonDll));
                PyThreadState_Swap = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyThreadState_Swap), GetUnmanagedDll(_PythonDll));
                PyGILState_Ensure = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyGILState_Ensure), GetUnmanagedDll(_PythonDll));
                PyGILState_Release = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyGILState_Release), GetUnmanagedDll(_PythonDll));
                PyGILState_GetThisThreadState = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyGILState_GetThisThreadState), GetUnmanagedDll(_PythonDll));
                Py_Main = (delegate* unmanaged[Cdecl]<int, IntPtr, int>)GetFunctionByName(nameof(Py_Main), GetUnmanagedDll(_PythonDll));
                PyEval_InitThreads = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_InitThreads), GetUnmanagedDll(_PythonDll));
                PyEval_ThreadsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyEval_ThreadsInitialized), GetUnmanagedDll(_PythonDll));
                PyEval_AcquireLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_AcquireLock), GetUnmanagedDll(_PythonDll));
                PyEval_ReleaseLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_ReleaseLock), GetUnmanagedDll(_PythonDll));
                PyEval_AcquireThread = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyEval_AcquireThread), GetUnmanagedDll(_PythonDll));
                PyEval_ReleaseThread = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyEval_ReleaseThread), GetUnmanagedDll(_PythonDll));
                PyEval_SaveThread = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyEval_SaveThread), GetUnmanagedDll(_PythonDll));
                PyEval_RestoreThread = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyEval_RestoreThread), GetUnmanagedDll(_PythonDll));
                PyEval_GetBuiltins = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetBuiltins), GetUnmanagedDll(_PythonDll));
                PyEval_GetGlobals = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetGlobals), GetUnmanagedDll(_PythonDll));
                PyEval_GetLocals = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyEval_GetLocals), GetUnmanagedDll(_PythonDll));
                Py_GetProgramName = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetProgramName), GetUnmanagedDll(_PythonDll));
                Py_SetProgramName = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetProgramName), GetUnmanagedDll(_PythonDll));
                Py_GetPythonHome = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPythonHome), GetUnmanagedDll(_PythonDll));
                Py_SetPythonHome = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetPythonHome), GetUnmanagedDll(_PythonDll));
                Py_GetPath = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPath), GetUnmanagedDll(_PythonDll));
                Py_SetPath = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(Py_SetPath), GetUnmanagedDll(_PythonDll));
                Py_GetVersion = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetVersion), GetUnmanagedDll(_PythonDll));
                Py_GetPlatform = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetPlatform), GetUnmanagedDll(_PythonDll));
                Py_GetCopyright = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetCopyright), GetUnmanagedDll(_PythonDll));
                Py_GetCompiler = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetCompiler), GetUnmanagedDll(_PythonDll));
                Py_GetBuildInfo = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(Py_GetBuildInfo), GetUnmanagedDll(_PythonDll));
                PyRun_SimpleStringFlags = (delegate* unmanaged[Cdecl]<StrPtr, in PyCompilerFlags, int>)GetFunctionByName(nameof(PyRun_SimpleStringFlags), GetUnmanagedDll(_PythonDll));
                PyRun_StringFlags = (delegate* unmanaged[Cdecl]<StrPtr, RunFlagType, BorrowedReference, BorrowedReference, in PyCompilerFlags, NewReference>)GetFunctionByName(nameof(PyRun_StringFlags), GetUnmanagedDll(_PythonDll));
                PyEval_EvalCode = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyEval_EvalCode), GetUnmanagedDll(_PythonDll));
                Py_CompileStringObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, IntPtr>)GetFunctionByName(nameof(Py_CompileStringObject), GetUnmanagedDll(_PythonDll));
                PyImport_ExecCodeModule = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyImport_ExecCodeModule), GetUnmanagedDll(_PythonDll));
                PyCFunction_NewEx = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyCFunction_NewEx), GetUnmanagedDll(_PythonDll));
                PyCFunction_Call = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyCFunction_Call), GetUnmanagedDll(_PythonDll));
                PyMethod_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyMethod_New), GetUnmanagedDll(_PythonDll));
                PyObject_HasAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyObject_HasAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_GetAttrString = (delegate* unmanaged[Cdecl]<IntPtr, StrPtr, IntPtr>)GetFunctionByName(nameof(PyObject_GetAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_SetAttrString = (delegate* unmanaged[Cdecl]<IntPtr, StrPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_SetAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_HasAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_HasAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_SetAttr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_SetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GetItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_GetItem), GetUnmanagedDll(_PythonDll));
                PyObject_SetItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_SetItem), GetUnmanagedDll(_PythonDll));
                PyObject_DelItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_DelItem), GetUnmanagedDll(_PythonDll));
                PyObject_GetIter = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_GetIter), GetUnmanagedDll(_PythonDll));
                PyObject_Call = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_Call), GetUnmanagedDll(_PythonDll));
                PyObject_CallObject = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_CallObject), GetUnmanagedDll(_PythonDll));
                PyObject_RichCompareBool = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int>)GetFunctionByName(nameof(PyObject_RichCompareBool), GetUnmanagedDll(_PythonDll));
                PyObject_IsInstance = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_IsInstance), GetUnmanagedDll(_PythonDll));
                PyObject_IsSubclass = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_IsSubclass), GetUnmanagedDll(_PythonDll));
                PyCallable_Check = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName(nameof(PyCallable_Check), GetUnmanagedDll(_PythonDll));
                PyObject_IsTrue = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsTrue), GetUnmanagedDll(_PythonDll));
                PyObject_Not = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName(nameof(PyObject_Not), GetUnmanagedDll(_PythonDll));
                _PyObject_Size = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyObject_Size", GetUnmanagedDll(_PythonDll));
                PyObject_Hash = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_Hash), GetUnmanagedDll(_PythonDll));
                PyObject_Repr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_Repr), GetUnmanagedDll(_PythonDll));
                PyObject_Str = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_Str), GetUnmanagedDll(_PythonDll));
                PyObject_Unicode = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyObject_Str", GetUnmanagedDll(_PythonDll));
                PyObject_Dir = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_Dir), GetUnmanagedDll(_PythonDll));
                PyObject_GetBuffer = (delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, int, int>)GetFunctionByName(nameof(PyObject_GetBuffer), GetUnmanagedDll(_PythonDll));
                PyBuffer_Release = (delegate* unmanaged[Cdecl]<ref Py_buffer, void>)GetFunctionByName(nameof(PyBuffer_Release), GetUnmanagedDll(_PythonDll));
                try
                {
                    PyBuffer_SizeFromFormat = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr>)GetFunctionByName(nameof(PyBuffer_SizeFromFormat), GetUnmanagedDll(_PythonDll));
                }
                catch (MissingMethodException)
                {
                    // only in 3.9+
                }
                PyBuffer_IsContiguous = (delegate* unmanaged[Cdecl]<ref Py_buffer, char, int>)GetFunctionByName(nameof(PyBuffer_IsContiguous), GetUnmanagedDll(_PythonDll));
                PyBuffer_GetPointer = (delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr[], IntPtr>)GetFunctionByName(nameof(PyBuffer_GetPointer), GetUnmanagedDll(_PythonDll));
                PyBuffer_FromContiguous = (delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, char, int>)GetFunctionByName(nameof(PyBuffer_FromContiguous), GetUnmanagedDll(_PythonDll));
                PyBuffer_ToContiguous = (delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, IntPtr, char, int>)GetFunctionByName(nameof(PyBuffer_ToContiguous), GetUnmanagedDll(_PythonDll));
                PyBuffer_FillContiguousStrides = (delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, int, char, void>)GetFunctionByName(nameof(PyBuffer_FillContiguousStrides), GetUnmanagedDll(_PythonDll));
                PyBuffer_FillInfo = (delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, IntPtr, int, int, int>)GetFunctionByName(nameof(PyBuffer_FillInfo), GetUnmanagedDll(_PythonDll));
                PyNumber_Int = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyNumber_Long", GetUnmanagedDll(_PythonDll));
                PyNumber_Long = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Long), GetUnmanagedDll(_PythonDll));
                PyNumber_Float = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Float), GetUnmanagedDll(_PythonDll));
                PyNumber_Check = (delegate* unmanaged[Cdecl]<IntPtr, bool>)GetFunctionByName(nameof(PyNumber_Check), GetUnmanagedDll(_PythonDll));
                PyInt_FromLong = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyLong_FromLong", GetUnmanagedDll(_PythonDll));
                PyInt_AsLong = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName("PyLong_AsLong", GetUnmanagedDll(_PythonDll));
                PyLong_FromLong = (delegate* unmanaged[Cdecl]<long, IntPtr>)GetFunctionByName(nameof(PyLong_FromLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromUnsignedLong32 = (delegate* unmanaged[Cdecl]<uint, IntPtr>)GetFunctionByName("PyLong_FromUnsignedLong", GetUnmanagedDll(_PythonDll));
                PyLong_FromUnsignedLong64 = (delegate* unmanaged[Cdecl]<ulong, IntPtr>)GetFunctionByName("PyLong_FromUnsignedLong", GetUnmanagedDll(_PythonDll));
                PyLong_FromDouble = (delegate* unmanaged[Cdecl]<double, IntPtr>)GetFunctionByName(nameof(PyLong_FromDouble), GetUnmanagedDll(_PythonDll));
                PyLong_FromLongLong = (delegate* unmanaged[Cdecl]<long, IntPtr>)GetFunctionByName(nameof(PyLong_FromLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromUnsignedLongLong = (delegate* unmanaged[Cdecl]<ulong, IntPtr>)GetFunctionByName(nameof(PyLong_FromUnsignedLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromString = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, IntPtr>)GetFunctionByName(nameof(PyLong_FromString), GetUnmanagedDll(_PythonDll));
                PyLong_AsLong = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName(nameof(PyLong_AsLong), GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedLong32 = (delegate* unmanaged[Cdecl]<IntPtr, uint>)GetFunctionByName("PyLong_AsUnsignedLong", GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedLong64 = (delegate* unmanaged[Cdecl]<IntPtr, ulong>)GetFunctionByName("PyLong_AsUnsignedLong", GetUnmanagedDll(_PythonDll));
                PyLong_AsLongLong = (delegate* unmanaged[Cdecl]<BorrowedReference, long>)GetFunctionByName(nameof(PyLong_AsLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedLongLong = (delegate* unmanaged[Cdecl]<IntPtr, ulong>)GetFunctionByName(nameof(PyLong_AsUnsignedLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromVoidPtr = (delegate* unmanaged[Cdecl]<IntPtr, NewReference>)GetFunctionByName(nameof(PyLong_FromVoidPtr), GetUnmanagedDll(_PythonDll));
                PyLong_AsVoidPtr = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyLong_AsVoidPtr), GetUnmanagedDll(_PythonDll));
                PyFloat_FromDouble = (delegate* unmanaged[Cdecl]<double, IntPtr>)GetFunctionByName(nameof(PyFloat_FromDouble), GetUnmanagedDll(_PythonDll));
                PyFloat_FromString = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyFloat_FromString), GetUnmanagedDll(_PythonDll));
                PyFloat_AsDouble = (delegate* unmanaged[Cdecl]<IntPtr, double>)GetFunctionByName(nameof(PyFloat_AsDouble), GetUnmanagedDll(_PythonDll));
                PyNumber_Add = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Add), GetUnmanagedDll(_PythonDll));
                PyNumber_Subtract = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Subtract), GetUnmanagedDll(_PythonDll));
                PyNumber_Multiply = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Multiply), GetUnmanagedDll(_PythonDll));
                PyNumber_TrueDivide = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_TrueDivide), GetUnmanagedDll(_PythonDll));
                PyNumber_And = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_And), GetUnmanagedDll(_PythonDll));
                PyNumber_Xor = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Xor), GetUnmanagedDll(_PythonDll));
                PyNumber_Or = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Or), GetUnmanagedDll(_PythonDll));
                PyNumber_Lshift = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Lshift), GetUnmanagedDll(_PythonDll));
                PyNumber_Rshift = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Rshift), GetUnmanagedDll(_PythonDll));
                PyNumber_Power = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Power), GetUnmanagedDll(_PythonDll));
                PyNumber_Remainder = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Remainder), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceAdd = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceAdd), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceSubtract = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceSubtract), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceMultiply = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceMultiply), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceTrueDivide = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceTrueDivide), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceAnd = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceAnd), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceXor = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceXor), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceOr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceOr), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceLshift = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceLshift), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceRshift = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceRshift), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlacePower = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlacePower), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceRemainder = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_InPlaceRemainder), GetUnmanagedDll(_PythonDll));
                PyNumber_Negative = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Negative), GetUnmanagedDll(_PythonDll));
                PyNumber_Positive = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Positive), GetUnmanagedDll(_PythonDll));
                PyNumber_Invert = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyNumber_Invert), GetUnmanagedDll(_PythonDll));
                PySequence_Check = (delegate* unmanaged[Cdecl]<IntPtr, bool>)GetFunctionByName(nameof(PySequence_Check), GetUnmanagedDll(_PythonDll));
                PySequence_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PySequence_GetItem), GetUnmanagedDll(_PythonDll));
                PySequence_SetItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_SetItem), GetUnmanagedDll(_PythonDll));
                PySequence_DelItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_DelItem), GetUnmanagedDll(_PythonDll));
                PySequence_GetSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PySequence_GetSlice), GetUnmanagedDll(_PythonDll));
                PySequence_SetSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_SetSlice), GetUnmanagedDll(_PythonDll));
                PySequence_DelSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_DelSlice), GetUnmanagedDll(_PythonDll));
                PySequence_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PySequence_Size", GetUnmanagedDll(_PythonDll));
                PySequence_Contains = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_Contains), GetUnmanagedDll(_PythonDll));
                PySequence_Concat = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PySequence_Concat), GetUnmanagedDll(_PythonDll));
                PySequence_Repeat = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PySequence_Repeat), GetUnmanagedDll(_PythonDll));
                PySequence_Index = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PySequence_Index), GetUnmanagedDll(_PythonDll));
                _PySequence_Count = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName("PySequence_Count", GetUnmanagedDll(_PythonDll));
                PySequence_Tuple = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PySequence_Tuple), GetUnmanagedDll(_PythonDll));
                PySequence_List = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PySequence_List), GetUnmanagedDll(_PythonDll));
                PyBytes_FromString = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyBytes_FromString), GetUnmanagedDll(_PythonDll));
                _PyBytes_Size = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyBytes_Size", GetUnmanagedDll(_PythonDll));
                PyUnicode_FromStringAndSize = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_FromStringAndSize), GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUTF8 = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_AsUTF8), GetUnmanagedDll(_PythonDll));
                PyUnicode_FromObject = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_FromObject), GetUnmanagedDll(_PythonDll));
                PyUnicode_FromEncodedObject = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_FromEncodedObject), GetUnmanagedDll(_PythonDll));
                PyUnicode_FromKindAndData = (delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_FromKindAndData), GetUnmanagedDll(_PythonDll));
                PyUnicode_GetMax = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyUnicode_GetMax), GetUnmanagedDll(_PythonDll));
                _PyUnicode_GetSize = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyUnicode_GetSize", GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUnicode = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_AsUnicode), GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUTF16String = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyUnicode_AsUTF16String), GetUnmanagedDll(_PythonDll));
                PyUnicode_FromOrdinal = (delegate* unmanaged[Cdecl]<int, IntPtr>)GetFunctionByName(nameof(PyUnicode_FromOrdinal), GetUnmanagedDll(_PythonDll));
                PyUnicode_InternFromString = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr>)GetFunctionByName(nameof(PyUnicode_InternFromString), GetUnmanagedDll(_PythonDll));
                PyUnicode_Compare = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyUnicode_Compare), GetUnmanagedDll(_PythonDll));
                PyDict_New = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyDict_New), GetUnmanagedDll(_PythonDll));
                PyDict_Next = (delegate* unmanaged[Cdecl]<IntPtr, out IntPtr, out IntPtr, out IntPtr, int>)GetFunctionByName(nameof(PyDict_Next), GetUnmanagedDll(_PythonDll));
                PyDict_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItem), GetUnmanagedDll(_PythonDll));
                PyDict_GetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemString), GetUnmanagedDll(_PythonDll));
                PyDict_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItem), GetUnmanagedDll(_PythonDll));
                PyDict_SetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItemString), GetUnmanagedDll(_PythonDll));
                PyDict_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_DelItem), GetUnmanagedDll(_PythonDll));
                PyDict_DelItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyDict_DelItemString), GetUnmanagedDll(_PythonDll));
                PyMapping_HasKey = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyMapping_HasKey), GetUnmanagedDll(_PythonDll));
                PyDict_Keys = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Keys), GetUnmanagedDll(_PythonDll));
                PyDict_Values = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyDict_Values), GetUnmanagedDll(_PythonDll));
                PyDict_Items = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Items), GetUnmanagedDll(_PythonDll));
                PyDict_Copy = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyDict_Copy), GetUnmanagedDll(_PythonDll));
                PyDict_Update = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_Update), GetUnmanagedDll(_PythonDll));
                PyDict_Clear = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyDict_Clear), GetUnmanagedDll(_PythonDll));
                _PyDict_Size = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName("PyDict_Size", GetUnmanagedDll(_PythonDll));
                PySet_New = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySet_New), GetUnmanagedDll(_PythonDll));
                PySet_Add = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Add), GetUnmanagedDll(_PythonDll));
                PySet_Contains = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Contains), GetUnmanagedDll(_PythonDll));
                PyList_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyList_New), GetUnmanagedDll(_PythonDll));
                PyList_AsTuple = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyList_AsTuple), GetUnmanagedDll(_PythonDll));
                PyList_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyList_GetItem), GetUnmanagedDll(_PythonDll));
                PyList_SetItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyList_SetItem), GetUnmanagedDll(_PythonDll));
                PyList_Insert = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyList_Insert), GetUnmanagedDll(_PythonDll));
                PyList_Append = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)GetFunctionByName(nameof(PyList_Append), GetUnmanagedDll(_PythonDll));
                PyList_Reverse = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Reverse), GetUnmanagedDll(_PythonDll));
                PyList_Sort = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Sort), GetUnmanagedDll(_PythonDll));
                PyList_GetSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyList_GetSlice), GetUnmanagedDll(_PythonDll));
                PyList_SetSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyList_SetSlice), GetUnmanagedDll(_PythonDll));
                PyList_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyList_Size), GetUnmanagedDll(_PythonDll));
                PyTuple_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyTuple_New), GetUnmanagedDll(_PythonDll));
                PyTuple_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyTuple_GetItem), GetUnmanagedDll(_PythonDll));
                PyTuple_SetItem = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyTuple_SetItem), GetUnmanagedDll(_PythonDll));
                PyTuple_GetSlice = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyTuple_GetSlice), GetUnmanagedDll(_PythonDll));
                PyTuple_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyTuple_Size), GetUnmanagedDll(_PythonDll));
                PyIter_Next = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyIter_Next), GetUnmanagedDll(_PythonDll));
                PyModule_New = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyModule_New), GetUnmanagedDll(_PythonDll));
                PyModule_GetName = (delegate* unmanaged[Cdecl]<IntPtr, StrPtr>)GetFunctionByName(nameof(PyModule_GetName), GetUnmanagedDll(_PythonDll));
                PyModule_GetDict = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyModule_GetDict), GetUnmanagedDll(_PythonDll));
                PyModule_GetFilename = (delegate* unmanaged[Cdecl]<IntPtr, StrPtr>)GetFunctionByName(nameof(PyModule_GetFilename), GetUnmanagedDll(_PythonDll));
                PyModule_Create2 = (delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr>)GetFunctionByName(nameof(PyModule_Create2), GetUnmanagedDll(_PythonDll));
                PyImport_Import = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyImport_Import), GetUnmanagedDll(_PythonDll));
                PyImport_ImportModule = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr>)GetFunctionByName(nameof(PyImport_ImportModule), GetUnmanagedDll(_PythonDll));
                PyImport_ReloadModule = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyImport_ReloadModule), GetUnmanagedDll(_PythonDll));
                PyImport_AddModule = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyImport_AddModule), GetUnmanagedDll(_PythonDll));
                PyImport_GetModuleDict = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyImport_GetModuleDict), GetUnmanagedDll(_PythonDll));
                PySys_SetArgvEx = (delegate* unmanaged[Cdecl]<int, IntPtr, int, void>)GetFunctionByName(nameof(PySys_SetArgvEx), GetUnmanagedDll(_PythonDll));
                PySys_GetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PySys_GetObject), GetUnmanagedDll(_PythonDll));
                PySys_SetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PySys_SetObject), GetUnmanagedDll(_PythonDll));
                PyType_Modified = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyType_Modified), GetUnmanagedDll(_PythonDll));
                PyType_IsSubtype = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool>)GetFunctionByName(nameof(PyType_IsSubtype), GetUnmanagedDll(_PythonDll));
                PyType_GenericNew = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyType_GenericNew), GetUnmanagedDll(_PythonDll));
                PyType_GenericAlloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyType_GenericAlloc), GetUnmanagedDll(_PythonDll));
                PyType_Ready = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName(nameof(PyType_Ready), GetUnmanagedDll(_PythonDll));
                _PyType_Lookup = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(_PyType_Lookup), GetUnmanagedDll(_PythonDll));
                PyObject_GenericGetAttr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyObject_GenericGetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GenericSetAttr = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyObject_GenericSetAttr), GetUnmanagedDll(_PythonDll));
                _PyObject_GetDictPtr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference*>)GetFunctionByName(nameof(_PyObject_GetDictPtr), GetUnmanagedDll(_PythonDll));
                PyObject_GC_Del = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyObject_GC_Del), GetUnmanagedDll(_PythonDll));
                PyObject_GC_Track = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyObject_GC_Track), GetUnmanagedDll(_PythonDll));
                PyObject_GC_UnTrack = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyObject_GC_UnTrack), GetUnmanagedDll(_PythonDll));
                _PyObject_Dump = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(_PyObject_Dump), GetUnmanagedDll(_PythonDll));
                PyMem_Malloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Malloc), GetUnmanagedDll(_PythonDll));
                PyMem_Realloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Realloc), GetUnmanagedDll(_PythonDll));
                PyMem_Free = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyMem_Free), GetUnmanagedDll(_PythonDll));
                PyErr_SetString = (delegate* unmanaged[Cdecl]<IntPtr, StrPtr, void>)GetFunctionByName(nameof(PyErr_SetString), GetUnmanagedDll(_PythonDll));
                PyErr_SetObject = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void>)GetFunctionByName(nameof(PyErr_SetObject), GetUnmanagedDll(_PythonDll));
                PyErr_SetFromErrno = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyErr_SetFromErrno), GetUnmanagedDll(_PythonDll));
                PyErr_SetNone = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyErr_SetNone), GetUnmanagedDll(_PythonDll));
                PyErr_ExceptionMatches = (delegate* unmanaged[Cdecl]<IntPtr, int>)GetFunctionByName(nameof(PyErr_ExceptionMatches), GetUnmanagedDll(_PythonDll));
                PyErr_GivenExceptionMatches = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(PyErr_GivenExceptionMatches), GetUnmanagedDll(_PythonDll));
                PyErr_NormalizeException = (delegate* unmanaged[Cdecl]<ref IntPtr, ref IntPtr, ref IntPtr, void>)GetFunctionByName(nameof(PyErr_NormalizeException), GetUnmanagedDll(_PythonDll));
                PyErr_Occurred = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyErr_Occurred), GetUnmanagedDll(_PythonDll));
                PyErr_Fetch = (delegate* unmanaged[Cdecl]<out IntPtr, out IntPtr, out IntPtr, void>)GetFunctionByName(nameof(PyErr_Fetch), GetUnmanagedDll(_PythonDll));
                PyErr_Restore = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)GetFunctionByName(nameof(PyErr_Restore), GetUnmanagedDll(_PythonDll));
                PyErr_Clear = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Clear), GetUnmanagedDll(_PythonDll));
                PyErr_Print = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Print), GetUnmanagedDll(_PythonDll));
                PyCell_Get = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyCell_Get), GetUnmanagedDll(_PythonDll));
                PyCell_Set = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)GetFunctionByName(nameof(PyCell_Set), GetUnmanagedDll(_PythonDll));
                PyGC_Collect = (delegate* unmanaged[Cdecl]<IntPtr>)GetFunctionByName(nameof(PyGC_Collect), GetUnmanagedDll(_PythonDll));
                PyCapsule_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference>)GetFunctionByName(nameof(PyCapsule_New), GetUnmanagedDll(_PythonDll));
                PyCapsule_GetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr>)GetFunctionByName(nameof(PyCapsule_GetPointer), GetUnmanagedDll(_PythonDll));
                PyCapsule_SetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)GetFunctionByName(nameof(PyCapsule_SetPointer), GetUnmanagedDll(_PythonDll));
                PyMethod_Self = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyMethod_Self), GetUnmanagedDll(_PythonDll));
                PyMethod_Function = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyMethod_Function), GetUnmanagedDll(_PythonDll));
                Py_AddPendingCall = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)GetFunctionByName(nameof(Py_AddPendingCall), GetUnmanagedDll(_PythonDll));
                Py_MakePendingCalls = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(Py_MakePendingCalls), GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedSize_t = (delegate* unmanaged[Cdecl]<IntPtr, nuint>)GetFunctionByName("PyLong_AsSize_t", GetUnmanagedDll(_PythonDll));
                PyLong_AsSignedSize_t = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PyLong_AsSsize_t", GetUnmanagedDll(_PythonDll));
                PyExplicitlyConvertToInt64 = (delegate* unmanaged[Cdecl]<IntPtr, long>)GetFunctionByName("PyLong_AsLongLong", GetUnmanagedDll(_PythonDll));
                PyDict_GetItemWithError = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemWithError), GetUnmanagedDll(_PythonDll));
                PyException_SetCause = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)GetFunctionByName(nameof(PyException_SetCause), GetUnmanagedDll(_PythonDll));
                PyThreadState_SetAsyncExcLLP64 = (delegate* unmanaged[Cdecl]<uint, IntPtr, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
                PyThreadState_SetAsyncExcLP64 = (delegate* unmanaged[Cdecl]<ulong, IntPtr, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
            }

            static global::System.IntPtr GetUnmanagedDll(string libraryName)
            {
                if (libraryName is null) return IntPtr.Zero;
                return libraryLoader.Load(libraryName);
            }

            static global::System.IntPtr GetFunctionByName(string functionName, global::System.IntPtr libraryHandle)
                => libraryLoader.GetFunction(libraryHandle, functionName);

            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyDictProxy_New { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_IncRef { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_DecRef { get; }
            internal static delegate* unmanaged[Cdecl]<void> Py_Initialize { get; }
            internal static delegate* unmanaged[Cdecl]<int, void> Py_InitializeEx { get; }
            internal static delegate* unmanaged[Cdecl]<int> Py_IsInitialized { get; }
            internal static delegate* unmanaged[Cdecl]<void> Py_Finalize { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_NewInterpreter { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_EndInterpreter { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyThreadState_New { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyThreadState_Get { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> _PyThreadState_UncheckedGet { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyThread_get_key_value { get; }
            internal static delegate* unmanaged[Cdecl]<int> PyThread_get_thread_ident { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyThread_set_key_value { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyThreadState_Swap { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyGILState_Ensure { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyGILState_Release { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyGILState_GetThisThreadState { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, int> Py_Main { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_InitThreads { get; }
            internal static delegate* unmanaged[Cdecl]<int> PyEval_ThreadsInitialized { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_AcquireLock { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_ReleaseLock { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyEval_AcquireThread { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyEval_ReleaseThread { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyEval_SaveThread { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyEval_RestoreThread { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetBuiltins { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetGlobals { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyEval_GetLocals { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetProgramName { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetProgramName { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPythonHome { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetPythonHome { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPath { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> Py_SetPath { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetVersion { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetPlatform { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetCopyright { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetCompiler { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> Py_GetBuildInfo { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, in PyCompilerFlags, int> PyRun_SimpleStringFlags { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, RunFlagType, BorrowedReference, BorrowedReference, in PyCompilerFlags, NewReference> PyRun_StringFlags { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyEval_EvalCode { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, IntPtr> Py_CompileStringObject { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr, IntPtr> PyImport_ExecCodeModule { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyCFunction_NewEx { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyCFunction_Call { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyMethod_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyObject_HasAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, StrPtr, IntPtr> PyObject_GetAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, StrPtr, IntPtr, int> PyObject_SetAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_HasAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PyObject_SetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyObject_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PyObject_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyObject_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_GetIter { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyObject_Call { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyObject_CallObject { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int> PyObject_RichCompareBool { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyObject_IsInstance { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyObject_IsSubclass { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyCallable_Check { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_IsTrue { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyObject_Not { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _PyObject_Size { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_Hash { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_Repr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_Str { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_Unicode { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyObject_Dir { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, int, int> PyObject_GetBuffer { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, void> PyBuffer_Release { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr> PyBuffer_SizeFromFormat { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, char, int> PyBuffer_IsContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr[], IntPtr> PyBuffer_GetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, char, int> PyBuffer_FromContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, IntPtr, char, int> PyBuffer_ToContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, int, char, void> PyBuffer_FillContiguousStrides { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, IntPtr, int, int, int> PyBuffer_FillInfo { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Int { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Long { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Float { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, bool> PyNumber_Check { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyInt_FromLong { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyInt_AsLong { get; }
            internal static delegate* unmanaged[Cdecl]<long, IntPtr> PyLong_FromLong { get; }
            internal static delegate* unmanaged[Cdecl]<uint, IntPtr> PyLong_FromUnsignedLong32 { get; }
            internal static delegate* unmanaged[Cdecl]<ulong, IntPtr> PyLong_FromUnsignedLong64 { get; }
            internal static delegate* unmanaged[Cdecl]<double, IntPtr> PyLong_FromDouble { get; }
            internal static delegate* unmanaged[Cdecl]<long, IntPtr> PyLong_FromLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<ulong, IntPtr> PyLong_FromUnsignedLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, IntPtr> PyLong_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyLong_AsLong { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, uint> PyLong_AsUnsignedLong32 { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, ulong> PyLong_AsUnsignedLong64 { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, long> PyLong_AsLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, ulong> PyLong_AsUnsignedLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, NewReference> PyLong_FromVoidPtr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyLong_AsVoidPtr { get; }
            internal static delegate* unmanaged[Cdecl]<double, IntPtr> PyFloat_FromDouble { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyFloat_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, double> PyFloat_AsDouble { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Add { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Subtract { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Multiply { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_TrueDivide { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_And { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Xor { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Or { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Lshift { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Rshift { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Power { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_Remainder { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceAdd { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceSubtract { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceMultiply { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceTrueDivide { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceAnd { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceXor { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceOr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceLshift { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceRshift { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlacePower { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyNumber_InPlaceRemainder { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Negative { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Positive { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyNumber_Invert { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, bool> PySequence_Check { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PySequence_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PySequence_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PySequence_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PySequence_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> PySequence_SetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PySequence_DelSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PySequence_Size { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PySequence_Contains { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PySequence_Concat { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PySequence_Repeat { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PySequence_Index { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> _PySequence_Count { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PySequence_Tuple { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PySequence_List { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyBytes_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _PyBytes_Size { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyUnicode_FromStringAndSize { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyUnicode_AsUTF8 { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyUnicode_FromObject { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyUnicode_FromEncodedObject { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, IntPtr> PyUnicode_FromKindAndData { get; }
            internal static delegate* unmanaged[Cdecl]<int> PyUnicode_GetMax { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _PyUnicode_GetSize { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyUnicode_AsUnicode { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyUnicode_AsUTF16String { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr> PyUnicode_FromOrdinal { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr> PyUnicode_InternFromString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyUnicode_Compare { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyDict_New { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, out IntPtr, out IntPtr, out IntPtr, int> PyDict_Next { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference> PyDict_GetItemString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyDict_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int> PyDict_SetItemString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyDict_DelItemString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyMapping_HasKey { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Keys { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyDict_Values { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Items { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyDict_Copy { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_Update { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyDict_Clear { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _PyDict_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySet_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Add { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Contains { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyList_New { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyList_AsTuple { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference> PyList_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PyList_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr, int> PyList_Insert { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int> PyList_Append { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Reverse { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Sort { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyList_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> PyList_SetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyList_Size { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyTuple_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference> PyTuple_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PyTuple_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyTuple_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyTuple_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyIter_Next { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyModule_New { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, StrPtr> PyModule_GetName { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference> PyModule_GetDict { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, StrPtr> PyModule_GetFilename { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> PyModule_Create2 { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyImport_Import { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr> PyImport_ImportModule { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyImport_ReloadModule { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PyImport_AddModule { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyImport_GetModuleDict { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, int, void> PySys_SetArgvEx { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PySys_GetObject { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int> PySys_SetObject { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyType_Modified { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool> PyType_IsSubtype { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> PyType_GenericNew { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyType_GenericAlloc { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyType_Ready { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> _PyType_Lookup { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyObject_GenericGetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> PyObject_GenericSetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference*> _PyObject_GetDictPtr { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyObject_GC_Del { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyObject_GC_Track { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyObject_GC_UnTrack { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> _PyObject_Dump { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyMem_Malloc { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PyMem_Realloc { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyMem_Free { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, StrPtr, void> PyErr_SetString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void> PyErr_SetObject { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyErr_SetFromErrno { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyErr_SetNone { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, int> PyErr_ExceptionMatches { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> PyErr_GivenExceptionMatches { get; }
            internal static delegate* unmanaged[Cdecl]<ref IntPtr, ref IntPtr, ref IntPtr, void> PyErr_NormalizeException { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyErr_Occurred { get; }
            internal static delegate* unmanaged[Cdecl]<out IntPtr, out IntPtr, out IntPtr, void> PyErr_Fetch { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> PyErr_Restore { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyErr_Clear { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyErr_Print { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyCell_Get { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int> PyCell_Set { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr> PyGC_Collect { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference> PyCapsule_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr> PyCapsule_GetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int> PyCapsule_SetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyMethod_Self { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> PyMethod_Function { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> Py_AddPendingCall { get; }
            internal static delegate* unmanaged[Cdecl]<int> Py_MakePendingCalls { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, nuint> PyLong_AsUnsignedSize_t { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyLong_AsSignedSize_t { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, long> PyExplicitlyConvertToInt64 { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItemWithError { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> PyException_SetCause { get; }
            internal static delegate* unmanaged[Cdecl]<uint, IntPtr, int> PyThreadState_SetAsyncExcLLP64 { get; }
            internal static delegate* unmanaged[Cdecl]<ulong, IntPtr, int> PyThreadState_SetAsyncExcLP64 { get; }
        }
    }


    public enum ShutdownMode
    {
        Default,
        Normal,
        Soft,
        Reload,
        Extension,
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
