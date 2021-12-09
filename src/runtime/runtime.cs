using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;
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
        public static string? PythonDLL
        {
            get => _PythonDll;
            set
            {
                if (_isInitialized)
                    throw new InvalidOperationException("This property must be set before runtime is initialized");
                _PythonDll = value;
            }
        }

        static string? _PythonDll = GetDefaultDllName();
        private static string? GetDefaultDllName()
        {
            string dll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            if (dll is not null) return dll;

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

        private static bool _isInitialized = false;

        internal static readonly bool Is32Bit = IntPtr.Size == 4;

        // .NET core: System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        internal static bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        internal static Version InteropVersion { get; }
            = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        public static int MainManagedThreadId { get; private set; }

        public static ShutdownMode ShutdownMode { get; internal set; }
        private static readonly List<PyObject> _pyRefs = new ();

        internal static Version PyVersion
        {
            get
            {
                var versionTuple = PySys_GetObject("version_info");
                var major = Converter.ToInt32(PyTuple_GetItem(versionTuple, 0));
                var minor = Converter.ToInt32(PyTuple_GetItem(versionTuple, 1));
                var micro = Converter.ToInt32(PyTuple_GetItem(versionTuple, 2));
                return new Version(major, minor, micro);
            }
        }

        const string RunSysPropName = "__pythonnet_run__";
        static int run = 0;

        internal static int GetRun()
        {
            int runNumber = run;
            Debug.Assert(runNumber > 0, "This must only be called after Runtime is initialized at least once");
            return runNumber;
        }

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

            bool interpreterAlreadyInitialized = TryUsingDll(
                () => Py_IsInitialized() != 0
            );
            if (!interpreterAlreadyInitialized)
            {
                Py_InitializeEx(initSigs ? 1 : 0);

                NewRun();

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
                if (mode != ShutdownMode.Extension)
                {
                    PyGILState_Ensure();
                }

                BorrowedReference pyRun = PySys_GetObject(RunSysPropName);
                if (pyRun != null)
                {
                    run = checked((int)PyLong_AsSignedSize_t(pyRun));
                }
                else
                {
                    NewRun();
                }
            }
            MainManagedThreadId = Thread.CurrentThread.ManagedThreadId;

            Finalizer.Initialize();

            InitPyMembers();

            ABI.Initialize(PyVersion);

            InternString.Initialize();

            GenericUtil.Reset();
            ClassManager.Reset();
            ClassDerivedObject.Reset();
            TypeManager.Initialize();

            // Initialize modules that depend on the runtime class.
            AssemblyManager.Initialize();
            OperatorMethod.Initialize();
            if (mode == ShutdownMode.Reload && RuntimeData.HasStashData())
            {
                RuntimeData.RestoreRuntimeData();
            }
            else
            {
                PyCLRMetaType = MetaType.Initialize();
                ImportHook.Initialize();
            }
            Exceptions.Initialize();

            // Need to add the runtime directory to sys.path so that we
            // can find built-in assemblies like System.Data, et. al.
            string rtdir = RuntimeEnvironment.GetRuntimeDirectory();
            BorrowedReference path = PySys_GetObject("path");
            using var item = PyString_FromString(rtdir);
            if (PySequence_Contains(path, item.Borrow()) == 0)
            {
                PyList_Append(path, item.Borrow());
            }
            AssemblyManager.UpdatePath();

            clrInterop = GetModuleLazy("clr.interop");
            inspect = GetModuleLazy("inspect");
        }

        static void NewRun()
        {
            run++;
            using var pyRun = PyLong_FromLongLong(run);
            PySys_SetObject(RunSysPropName, pyRun.BorrowOrThrow());
        }

        private static void InitPyMembers()
        {
            using (var builtinsOwned = PyImport_ImportModule("builtins"))
            {
                var builtins = builtinsOwned.Borrow();
                SetPyMember(out PyNotImplemented, PyObject_GetAttrString(builtins, "NotImplemented").StealNullable());

                SetPyMember(out PyBaseObjectType, PyObject_GetAttrString(builtins, "object").StealNullable());

                SetPyMember(out _PyNone, PyObject_GetAttrString(builtins, "None").StealNullable());
                SetPyMember(out _PyTrue, PyObject_GetAttrString(builtins, "True").StealNullable());
                SetPyMember(out _PyFalse, PyObject_GetAttrString(builtins, "False").StealNullable());

                SetPyMemberTypeOf(out PyBoolType, _PyTrue!);
                SetPyMemberTypeOf(out PyNoneType, _PyNone!);

                SetPyMemberTypeOf(out PyMethodType, PyObject_GetAttrString(builtins, "len").StealNullable());

                // For some arcane reason, builtins.__dict__.__setitem__ is *not*
                // a wrapper_descriptor, even though dict.__setitem__ is.
                //
                // object.__init__ seems safe, though.
                SetPyMemberTypeOf(out PyWrapperDescriptorType, PyObject_GetAttrString(PyBaseObjectType, "__init__").StealNullable());

                SetPyMember(out PySuper_Type, PyObject_GetAttrString(builtins, "super").StealNullable());
            }

            SetPyMemberTypeOf(out PyStringType, PyString_FromString("string").StealNullable());

            SetPyMemberTypeOf(out PyUnicodeType, PyString_FromString("unicode").StealNullable());

            SetPyMemberTypeOf(out PyBytesType, EmptyPyBytes().StealNullable());

            SetPyMemberTypeOf(out PyTupleType, PyTuple_New(0).StealNullable());

            SetPyMemberTypeOf(out PyListType, PyList_New(0).StealNullable());

            SetPyMemberTypeOf(out PyDictType, PyDict_New().StealNullable());

            SetPyMemberTypeOf(out PyLongType, PyInt_FromInt32(0).StealNullable());

            SetPyMemberTypeOf(out PyFloatType, PyFloat_FromDouble(0).StealNullable());

            _PyObject_NextNotImplemented = Get_PyObject_NextNotImplemented();
            {
                using var sys = PyImport_ImportModule("sys");
                SetPyMemberTypeOf(out PyModuleType, sys.StealNullable());
            }
        }

        private static NativeFunc* Get_PyObject_NextNotImplemented()
        {
            using var pyType = SlotHelper.CreateObjectType();
            return Util.ReadPtr<NativeFunc>(pyType.Borrow(), TypeOffset.tp_iternext);
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

            NullGCHandles(ExtensionType.loadedExtensions);
            ClassManager.RemoveClasses();
            TypeManager.RemoveTypes(mode);

            MetaType.Release();
            PyCLRMetaType.Dispose();
            PyCLRMetaType = null!;

            Exceptions.Shutdown();
            PythonEngine.InteropConfiguration.Dispose();
            DisposeLazyModule(clrInterop);
            DisposeLazyModule(inspect);
            PyObjectConversions.Reset();

            PyGC_Collect();
            bool everythingSeemsCollected = TryCollectingGarbage();
            Debug.Assert(everythingSeemsCollected);

            Finalizer.Shutdown();
            InternString.Shutdown();

            if (mode != ShutdownMode.Normal && mode != ShutdownMode.Extension)
            {
                if (mode == ShutdownMode.Soft)
                {
                    RuntimeState.Restore();
                }
                ResetPyMembers();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                PyGILState_Release(state);
                // Then release the GIL for good, if there is somehting to release
                // Use the unchecked version as the checked version calls `abort()`
                // if the current state is NULL.
                if (_PyThreadState_UncheckedGet() != (PyThreadState*)0)
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
                else
                {
                    PyGILState_Release(state);
                }
            }
        }

        const int MaxCollectRetriesOnShutdown = 20;
        internal static int _collected;
        static bool TryCollectingGarbage()
        {
            for (int attempt = 0; attempt < MaxCollectRetriesOnShutdown; attempt++)
            {
                Interlocked.Exchange(ref _collected, 0);
                nint pyCollected = 0;
                for (int i = 0; i < 2; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    pyCollected += PyGC_Collect();
                    pyCollected += Finalizer.Instance.DisposeAll();
                }
                if (Volatile.Read(ref _collected) == 0 && pyCollected == 0)
                {
                    return true;
                }
                else
                {
                    NullGCHandles(CLRObject.reflectedObjects);
                }
            }
            return false;
        }

        internal static void Shutdown()
        {
            var mode = ShutdownMode;
            Shutdown(mode);
        }

        static void DisposeLazyModule(Lazy<PyObject> module)
        {
            if (module.IsValueCreated)
            {
                module.Value.Dispose();
            }
        }

        private static Lazy<PyObject> GetModuleLazy(string moduleName)
            => moduleName is null
                ? throw new ArgumentNullException(nameof(moduleName))
                : new Lazy<PyObject>(() => PyModule.Import(moduleName), isThreadSafe: false);

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
            catch (PythonException e) when (e.Is(Exceptions.ImportError))
            {
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
                }
            }
        }

        private static void SetPyMember(out PyObject obj, StolenReference value)
        {
            // XXX: For current usages, value should not be null.
            if (value == null)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            obj = new PyObject(value);
            _pyRefs.Add(obj);
        }

        private static void SetPyMemberTypeOf(out PyType obj, PyObject value)
        {
            var type = PyObject_Type(value);
            obj = new PyType(type.StealOrThrow(), prevalidated: true);
            _pyRefs.Add(obj);
        }

        private static void SetPyMemberTypeOf(out PyObject obj, StolenReference value)
        {
            if (value == null)
            {
                throw PythonException.ThrowLastAsClrException();
            }
            var @ref = new BorrowedReference(value.Pointer);
            var type = PyObject_Type(@ref);
            XDecref(value.AnalyzerWorkaround());
            SetPyMember(out obj, type.StealNullable());
        }

        private static void ResetPyMembers()
        {
            foreach (var pyObj in _pyRefs)
                pyObj.Dispose();
            _pyRefs.Clear();
        }

        private static void ClearClrModules()
        {
            var modules = PyImport_GetModuleDict();
            using var items = PyDict_Items(modules);
            nint length = PyList_Size(items.BorrowOrThrow());
            if (length < 0) throw PythonException.ThrowLastAsClrException();
            for (nint i = 0; i < length; i++)
            {
                var item = PyList_GetItem(items.Borrow(), i);
                var name = PyTuple_GetItem(item, 0);
                var module = PyTuple_GetItem(item, 1);
                if (ManagedType.IsInstanceOfManagedType(module))
                {
                    PyDict_DelItem(modules, name);
                }
            }
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
            if (!PythonException.CurrentMatches(Exceptions.KeyError))
            {
                throw PythonException.ThrowLastAsClrException();
            }
            PyErr_Clear();
        }

        private static void NullGCHandles(IEnumerable<IntPtr> objects)
        {
            foreach (IntPtr objWithGcHandle in objects.ToArray())
            {
                var @ref = new BorrowedReference(objWithGcHandle);
                ManagedType.TryFreeGCHandle(@ref);
            }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // these objects are initialized in Initialize rather than in constructor
        internal static PyObject PyBaseObjectType;
        internal static PyObject PyModuleType;
        internal static PyObject PySuper_Type;
        internal static PyType PyCLRMetaType;
        internal static PyObject PyMethodType;
        internal static PyObject PyWrapperDescriptorType;

        internal static PyObject PyUnicodeType;
        internal static PyObject PyStringType;
        internal static PyObject PyTupleType;
        internal static PyObject PyListType;
        internal static PyObject PyDictType;
        internal static PyObject PyLongType;
        internal static PyObject PyFloatType;
        internal static PyType PyBoolType;
        internal static PyType PyNoneType;
        internal static BorrowedReference PyTypeType => new(Delegates.PyType_Type);

        internal static int* Py_NoSiteFlag;

        internal static PyObject PyBytesType;
        internal static NativeFunc* _PyObject_NextNotImplemented;

        internal static PyObject PyNotImplemented;
        internal const int Py_LT = 0;
        internal const int Py_LE = 1;
        internal const int Py_EQ = 2;
        internal const int Py_NE = 3;
        internal const int Py_GT = 4;
        internal const int Py_GE = 5;

        internal static BorrowedReference PyTrue => _PyTrue;
        static PyObject _PyTrue;
        internal static BorrowedReference PyFalse => _PyFalse;
        static PyObject _PyFalse;
        internal static BorrowedReference PyNone => _PyNone;
        private static PyObject _PyNone;

        private static Lazy<PyObject> inspect;
        internal static PyObject InspectModule => inspect.Value;
        private static Lazy<PyObject> clrInterop;
        internal static PyObject InteropModule => clrInterop.Value;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        internal static BorrowedReference CLRMetaType => PyCLRMetaType;

        public static PyObject None => new(_PyNone);

        /// <summary>
        /// Check if any Python Exceptions occurred.
        /// If any exist throw new PythonException.
        /// </summary>
        /// <remarks>
        /// Can be used instead of `obj == IntPtr.Zero` for example.
        /// </remarks>
        internal static void CheckExceptionOccurred()
        {
            if (PyErr_Occurred() != null)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        internal static NewReference ExtendTuple(BorrowedReference t, params PyObject[] args)
        {
            var size = PyTuple_Size(t);
            int add = args.Length;

            NewReference items = PyTuple_New(size + add);
            for (var i = 0; i < size; i++)
            {
                var item = PyTuple_GetItem(t, i);
                PyTuple_SetItem(items.Borrow(), i, item);
            }

            for (var n = 0; n < add; n++)
            {
                PyTuple_SetItem(items.Borrow(), size + n, args[n]);
            }

            return items;
        }

        internal static Type[]? PythonArgsToTypeArray(BorrowedReference arg)
        {
            return PythonArgsToTypeArray(arg, false);
        }

        internal static Type[]? PythonArgsToTypeArray(BorrowedReference arg, bool mangleObjects)
        {
            // Given a PyObject * that is either a single type object or a
            // tuple of (managed or unmanaged) type objects, return a Type[]
            // containing the CLR Type objects that map to those types.
            BorrowedReference args = arg;
            NewReference newArgs = default;

            if (!PyTuple_Check(arg))
            {
                newArgs = PyTuple_New(1);
                args = newArgs.Borrow();
                PyTuple_SetItem(args, 0, arg);
            }

            var n = PyTuple_Size(args);
            var types = new Type[n];
            Type? t = null;

            for (var i = 0; i < n; i++)
            {
                BorrowedReference op = PyTuple_GetItem(args, i);
                if (mangleObjects && (!PyType_Check(op)))
                {
                    op = PyObject_TYPE(op);
                }
                ManagedType? mt = ManagedType.GetManagedObject(op);

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
            newArgs.Dispose();
            return types;
        }

        /// <summary>
        /// Managed exports of the Python C API. Where appropriate, we do
        /// some optimization to avoid managed &lt;--&gt; unmanaged transitions
        /// (mostly for heavily used methods).
        /// </summary>
        [Obsolete("Use NewReference or PyObject constructor instead")]
        internal static unsafe void XIncref(BorrowedReference op)
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

        internal static unsafe void XDecref(StolenReference op)
        {
#if DEBUG
            Debug.Assert(op == null || Refcount(new BorrowedReference(op.Pointer)) > 0);
            Debug.Assert(_isInitialized || Py_IsInitialized() != 0 || _Py_IsFinalizing() != false);
#endif
#if !CUSTOM_INCDEC_REF
            if (op == null) return;
            Py_DecRef(op.AnalyzerWorkaround());
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
        internal static unsafe nint Refcount(BorrowedReference op)
        {
            if (op == null)
            {
                return 0;
            }
            var p = (nint*)(op.DangerousGetAddress() + ABI.RefCountOffset);
            return *p;
        }
        [Pure]
        internal static int Refcount32(BorrowedReference op) => checked((int)Refcount(op));

        /// <summary>
        /// Call specified function, and handle PythonDLL-related failures.
        /// </summary>
        internal static T TryUsingDll<T>(Func<T> op)
        {
            try
            {
                return op();
            }
            catch (TypeInitializationException loadFailure)
            {
                var delegatesLoadFailure = loadFailure;
                // failure to load Delegates type might have been the cause
                // of failure to load some higher-level type
                while (delegatesLoadFailure.InnerException is TypeInitializationException nested)
                {
                    delegatesLoadFailure = nested;
                }

                if (delegatesLoadFailure.InnerException is BadPythonDllException badDll)
                {
                    throw badDll;
                }

                throw;
            }
        }

        /// <summary>
        /// Export of Macro Py_XIncRef. Use XIncref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>

        internal static void Py_IncRef(BorrowedReference ob) => Delegates.Py_IncRef(ob);

        /// <summary>
        /// Export of Macro Py_XDecRef. Use XDecref instead.
        /// Limit this function usage for Testing and Py_Debug builds
        /// </summary>
        /// <param name="ob">PyObject Ptr</param>

        internal static void Py_DecRef(StolenReference ob) => Delegates.Py_DecRef(ob);


        internal static void Py_Initialize() => Delegates.Py_Initialize();


        internal static void Py_InitializeEx(int initsigs) => Delegates.Py_InitializeEx(initsigs);


        internal static int Py_IsInitialized() => Delegates.Py_IsInitialized();


        internal static void Py_Finalize() => Delegates.Py_Finalize();


        internal static PyThreadState* Py_NewInterpreter() => Delegates.Py_NewInterpreter();


        internal static void Py_EndInterpreter(PyThreadState* threadState) => Delegates.Py_EndInterpreter(threadState);


        internal static PyThreadState* PyThreadState_New(PyInterpreterState* istate) => Delegates.PyThreadState_New(istate);


        internal static PyThreadState* PyThreadState_Get() => Delegates.PyThreadState_Get();


        internal static PyThreadState* _PyThreadState_UncheckedGet() => Delegates._PyThreadState_UncheckedGet();


        internal static int PyGILState_Check() => Delegates.PyGILState_Check();
        internal static PyGILState PyGILState_Ensure() => Delegates.PyGILState_Ensure();


        internal static void PyGILState_Release(PyGILState gs) => Delegates.PyGILState_Release(gs);



        internal static PyThreadState* PyGILState_GetThisThreadState() => Delegates.PyGILState_GetThisThreadState();


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


        internal static void PyEval_AcquireThread(PyThreadState* tstate) => Delegates.PyEval_AcquireThread(tstate);


        internal static void PyEval_ReleaseThread(PyThreadState* tstate) => Delegates.PyEval_ReleaseThread(tstate);


        internal static PyThreadState* PyEval_SaveThread() => Delegates.PyEval_SaveThread();


        internal static void PyEval_RestoreThread(PyThreadState* tstate) => Delegates.PyEval_RestoreThread(tstate);


        internal static BorrowedReference PyEval_GetBuiltins() => Delegates.PyEval_GetBuiltins();


        internal static BorrowedReference PyEval_GetGlobals() => Delegates.PyEval_GetGlobals();


        internal static BorrowedReference PyEval_GetLocals() => Delegates.PyEval_GetLocals();


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

        internal static NewReference PyEval_EvalCode(BorrowedReference co, BorrowedReference globals, BorrowedReference locals) => Delegates.PyEval_EvalCode(co, globals, locals);

        /// <summary>
        /// Return value: New reference.
        /// This is a simplified interface to Py_CompileStringFlags() below, leaving flags set to NULL.
        /// </summary>
        internal static NewReference Py_CompileString(string str, string file, int start)
        {
            using var strPtr = new StrPtr(str, Encoding.UTF8);
            using var fileObj = new PyString(file);
            return Delegates.Py_CompileStringObject(strPtr, fileObj, start, Utf8String, -1);
        }

        internal static NewReference PyImport_ExecCodeModule(string name, BorrowedReference code)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyImport_ExecCodeModule(namePtr, code);
        }

        //====================================================================
        // Python abstract object API
        //====================================================================

        /// <summary>
        /// A macro-like method to get the type of a Python object. This is
        /// designed to be lean and mean in IL &amp; avoid managed &lt;-&gt; unmanaged
        /// transitions. Note that this does not incref the type object.
        /// </summary>
        internal static unsafe BorrowedReference PyObject_TYPE(BorrowedReference op)
        {
            IntPtr address = op.DangerousGetAddressOrNull();
            if (address == IntPtr.Zero)
            {
                return BorrowedReference.Null;
            }
            Debug.Assert(TypeOffset.ob_type > 0);
            BorrowedReference* typePtr = (BorrowedReference*)(address + TypeOffset.ob_type);
            return *typePtr;
        }
        internal static NewReference PyObject_Type(BorrowedReference o)
            => Delegates.PyObject_Type(o);

        internal static string PyObject_GetTypeName(BorrowedReference op)
        {
            Debug.Assert(TypeOffset.tp_name > 0);
            Debug.Assert(op != null);
            BorrowedReference pyType = PyObject_TYPE(op);
            IntPtr ppName = Util.ReadIntPtr(pyType, TypeOffset.tp_name);
            return Marshal.PtrToStringAnsi(ppName);
        }

        /// <summary>
        /// Test whether the Python object is an iterable.
        /// </summary>
        internal static bool PyObject_IsIterable(BorrowedReference ob)
        {
            var ob_type = PyObject_TYPE(ob);
            return Util.ReadIntPtr(ob_type, TypeOffset.tp_iter) != IntPtr.Zero;
        }

        internal static int PyObject_HasAttrString(BorrowedReference pointer, string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_HasAttrString(pointer, namePtr);
        }

        internal static NewReference PyObject_GetAttrString(BorrowedReference pointer, string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_GetAttrString(pointer, namePtr);
        }

        internal static NewReference PyObject_GetAttrString(BorrowedReference pointer, StrPtr name)
            => Delegates.PyObject_GetAttrString(pointer, name);


        internal static int PyObject_DelAttr(BorrowedReference @object, BorrowedReference name) => Delegates.PyObject_SetAttr(@object, name, null);
        internal static int PyObject_DelAttrString(BorrowedReference @object, string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_SetAttrString(@object, namePtr, null);
        }
        internal static int PyObject_SetAttrString(BorrowedReference @object, string name, BorrowedReference value)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyObject_SetAttrString(@object, namePtr, value);
        }

        internal static int PyObject_HasAttr(BorrowedReference pointer, BorrowedReference name) => Delegates.PyObject_HasAttr(pointer, name);


        internal static NewReference PyObject_GetAttr(BorrowedReference pointer, IntPtr name)
            => Delegates.PyObject_GetAttr(pointer, new BorrowedReference(name));
        internal static NewReference PyObject_GetAttr(BorrowedReference o, BorrowedReference name) => Delegates.PyObject_GetAttr(o, name);


        internal static int PyObject_SetAttr(BorrowedReference o, BorrowedReference name, BorrowedReference value) => Delegates.PyObject_SetAttr(o, name, value);


        internal static NewReference PyObject_GetItem(BorrowedReference o, BorrowedReference key) => Delegates.PyObject_GetItem(o, key);


        internal static int PyObject_SetItem(BorrowedReference o, BorrowedReference key, BorrowedReference value) => Delegates.PyObject_SetItem(o, key, value);


        internal static int PyObject_DelItem(BorrowedReference o, BorrowedReference key) => Delegates.PyObject_DelItem(o, key);


        internal static NewReference PyObject_GetIter(BorrowedReference op) => Delegates.PyObject_GetIter(op);


        internal static NewReference PyObject_Call(BorrowedReference pointer, BorrowedReference args, BorrowedReference kw) => Delegates.PyObject_Call(pointer, args, kw);

        internal static NewReference PyObject_CallObject(BorrowedReference callable, BorrowedReference args) => Delegates.PyObject_CallObject(callable, args);
        internal static IntPtr PyObject_CallObject(IntPtr pointer, IntPtr args)
            => Delegates.PyObject_CallObject(new BorrowedReference(pointer), new BorrowedReference(args))
                .DangerousMoveToPointerOrNull();


        internal static int PyObject_RichCompareBool(BorrowedReference value1, BorrowedReference value2, int opid) => Delegates.PyObject_RichCompareBool(value1, value2, opid);

        internal static int PyObject_Compare(BorrowedReference value1, BorrowedReference value2)
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


        internal static int PyObject_IsInstance(BorrowedReference ob, BorrowedReference type) => Delegates.PyObject_IsInstance(ob, type);


        internal static int PyObject_IsSubclass(BorrowedReference ob, BorrowedReference type) => Delegates.PyObject_IsSubclass(ob, type);


        internal static int PyCallable_Check(BorrowedReference o) => Delegates.PyCallable_Check(o);


        internal static int PyObject_IsTrue(IntPtr pointer) => PyObject_IsTrue(new BorrowedReference(pointer));
        internal static int PyObject_IsTrue(BorrowedReference pointer) => Delegates.PyObject_IsTrue(pointer);


        internal static int PyObject_Not(BorrowedReference o) => Delegates.PyObject_Not(o);

        internal static nint PyObject_Size(BorrowedReference pointer) => Delegates.PyObject_Size(pointer);


        internal static nint PyObject_Hash(BorrowedReference op) => Delegates.PyObject_Hash(op);


        internal static NewReference PyObject_Repr(BorrowedReference pointer)
        {
            AssertNoErorSet();

            return Delegates.PyObject_Repr(pointer);
        }


        internal static NewReference PyObject_Str(BorrowedReference pointer)
        {
            AssertNoErorSet();

            return Delegates.PyObject_Str(pointer);
        }

        [Conditional("DEBUG")]
        internal static void AssertNoErorSet()
        {
            if (Exceptions.ErrorOccurred())
                throw new InvalidOperationException(
                    "Can't call with exception set",
                    PythonException.FetchCurrent());
        }


        internal static NewReference PyObject_Dir(BorrowedReference pointer) => Delegates.PyObject_Dir(pointer);

        internal static void _Py_NewReference(BorrowedReference ob)
        {
            if (Delegates._Py_NewReference != null)
                Delegates._Py_NewReference(ob);
        }

        internal static bool? _Py_IsFinalizing()
        {
            if (Delegates._Py_IsFinalizing != null)
                return Delegates._Py_IsFinalizing() != 0;
            else
                return null; ;
        }

        //====================================================================
        // Python buffer API
        //====================================================================


        internal static int PyObject_GetBuffer(BorrowedReference exporter, out Py_buffer view, int flags) => Delegates.PyObject_GetBuffer(exporter, out view, flags);


        internal static void PyBuffer_Release(ref Py_buffer view) => Delegates.PyBuffer_Release(ref view);


        internal static nint PyBuffer_SizeFromFormat(string format)
        {
            using var formatPtr = new StrPtr(format, Encoding.ASCII);
            return Delegates.PyBuffer_SizeFromFormat(formatPtr);
        }

        internal static int PyBuffer_IsContiguous(ref Py_buffer view, char order) => Delegates.PyBuffer_IsContiguous(ref view, order);


        internal static IntPtr PyBuffer_GetPointer(ref Py_buffer view, IntPtr[] indices) => Delegates.PyBuffer_GetPointer(ref view, indices);


        internal static int PyBuffer_FromContiguous(ref Py_buffer view, IntPtr buf, IntPtr len, char fort) => Delegates.PyBuffer_FromContiguous(ref view, buf, len, fort);


        internal static int PyBuffer_ToContiguous(IntPtr buf, ref Py_buffer src, IntPtr len, char order) => Delegates.PyBuffer_ToContiguous(buf, ref src, len, order);


        internal static void PyBuffer_FillContiguousStrides(int ndims, IntPtr shape, IntPtr strides, int itemsize, char order) => Delegates.PyBuffer_FillContiguousStrides(ndims, shape, strides, itemsize, order);


        internal static int PyBuffer_FillInfo(ref Py_buffer view, BorrowedReference exporter, IntPtr buf, IntPtr len, int _readonly, int flags) => Delegates.PyBuffer_FillInfo(ref view, exporter, buf, len, _readonly, flags);

        //====================================================================
        // Python number API
        //====================================================================


        internal static NewReference PyNumber_Long(BorrowedReference ob) => Delegates.PyNumber_Long(ob);


        internal static NewReference PyNumber_Float(BorrowedReference ob) => Delegates.PyNumber_Float(ob);


        internal static bool PyNumber_Check(BorrowedReference ob) => Delegates.PyNumber_Check(ob);

        internal static bool PyInt_Check(BorrowedReference ob)
            => PyObject_TypeCheck(ob, PyLongType);

        internal static bool PyBool_Check(BorrowedReference ob)
            => PyObject_TypeCheck(ob, PyBoolType);

        internal static NewReference PyInt_FromInt32(int value) => PyLong_FromLongLong(value);

        internal static NewReference PyInt_FromInt64(long value) => PyLong_FromLongLong(value);

        internal static bool PyLong_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == PyLongType;
        }

        internal static NewReference PyLong_FromLongLong(long value) => Delegates.PyLong_FromLongLong(value);


        internal static NewReference PyLong_FromUnsignedLongLong(ulong value) => Delegates.PyLong_FromUnsignedLongLong(value);


        internal static NewReference PyLong_FromString(string value, int radix)
        {
            using var valPtr = new StrPtr(value, Encoding.UTF8);
            return Delegates.PyLong_FromString(valPtr, IntPtr.Zero, radix);
        }



        internal static nuint PyLong_AsUnsignedSize_t(BorrowedReference value) => Delegates.PyLong_AsUnsignedSize_t(value);

        internal static nint PyLong_AsSignedSize_t(BorrowedReference value) => Delegates.PyLong_AsSignedSize_t(value);

        internal static long? PyLong_AsLongLong(BorrowedReference value)
        {
            long result = Delegates.PyLong_AsLongLong(value);
            if (result == -1 && Exceptions.ErrorOccurred())
            {
                return null;
            }
            return result;
        }

        internal static ulong? PyLong_AsUnsignedLongLong(BorrowedReference value)
        {
            ulong result = Delegates.PyLong_AsUnsignedLongLong(value);
            if (result == unchecked((ulong)-1) && Exceptions.ErrorOccurred())
            {
                return null;
            }
            return result;
        }

        internal static bool PyFloat_Check(BorrowedReference ob)
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


        internal static NewReference PyFloat_FromDouble(double value) => Delegates.PyFloat_FromDouble(value);


        internal static NewReference PyFloat_FromString(BorrowedReference value) => Delegates.PyFloat_FromString(value);


        internal static double PyFloat_AsDouble(BorrowedReference ob) => Delegates.PyFloat_AsDouble(ob);


        internal static NewReference PyNumber_Add(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Add(o1, o2);


        internal static NewReference PyNumber_Subtract(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Subtract(o1, o2);


        internal static NewReference PyNumber_Multiply(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Multiply(o1, o2);


        internal static NewReference PyNumber_TrueDivide(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_TrueDivide(o1, o2);


        internal static NewReference PyNumber_And(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_And(o1, o2);


        internal static NewReference PyNumber_Xor(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Xor(o1, o2);


        internal static NewReference PyNumber_Or(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Or(o1, o2);


        internal static NewReference PyNumber_Lshift(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Lshift(o1, o2);


        internal static NewReference PyNumber_Rshift(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Rshift(o1, o2);


        internal static NewReference PyNumber_Power(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Power(o1, o2);


        internal static NewReference PyNumber_Remainder(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_Remainder(o1, o2);


        internal static NewReference PyNumber_InPlaceAdd(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceAdd(o1, o2);


        internal static NewReference PyNumber_InPlaceSubtract(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceSubtract(o1, o2);


        internal static NewReference PyNumber_InPlaceMultiply(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceMultiply(o1, o2);


        internal static NewReference PyNumber_InPlaceTrueDivide(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceTrueDivide(o1, o2);


        internal static NewReference PyNumber_InPlaceAnd(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceAnd(o1, o2);


        internal static NewReference PyNumber_InPlaceXor(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceXor(o1, o2);


        internal static NewReference PyNumber_InPlaceOr(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceOr(o1, o2);


        internal static NewReference PyNumber_InPlaceLshift(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceLshift(o1, o2);


        internal static NewReference PyNumber_InPlaceRshift(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceRshift(o1, o2);


        internal static NewReference PyNumber_InPlacePower(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlacePower(o1, o2);


        internal static NewReference PyNumber_InPlaceRemainder(BorrowedReference o1, BorrowedReference o2) => Delegates.PyNumber_InPlaceRemainder(o1, o2);


        internal static NewReference PyNumber_Negative(BorrowedReference o1) => Delegates.PyNumber_Negative(o1);


        internal static NewReference PyNumber_Positive(BorrowedReference o1) => Delegates.PyNumber_Positive(o1);


        internal static NewReference PyNumber_Invert(BorrowedReference o1) => Delegates.PyNumber_Invert(o1);


        //====================================================================
        // Python sequence API
        //====================================================================


        internal static bool PySequence_Check(BorrowedReference pointer) => Delegates.PySequence_Check(pointer);

        internal static NewReference PySequence_GetItem(BorrowedReference pointer, nint index) => Delegates.PySequence_GetItem(pointer, index);
        internal static int PySequence_SetItem(BorrowedReference pointer, nint index, BorrowedReference value) => Delegates.PySequence_SetItem(pointer, index, value);

        internal static int PySequence_DelItem(BorrowedReference pointer, nint index) => Delegates.PySequence_DelItem(pointer, index);

        internal static NewReference PySequence_GetSlice(BorrowedReference pointer, nint i1, nint i2) => Delegates.PySequence_GetSlice(pointer, i1, i2);

        internal static int PySequence_SetSlice(BorrowedReference pointer, nint i1, nint i2, BorrowedReference v) => Delegates.PySequence_SetSlice(pointer, i1, i2, v);

        internal static int PySequence_DelSlice(BorrowedReference pointer, nint i1, nint i2) => Delegates.PySequence_DelSlice(pointer, i1, i2);

        internal static nint PySequence_Size(BorrowedReference pointer) => Delegates.PySequence_Size(pointer);

        internal static int PySequence_Contains(BorrowedReference pointer, BorrowedReference item) => Delegates.PySequence_Contains(pointer, item);


        internal static NewReference PySequence_Concat(BorrowedReference pointer, BorrowedReference other) => Delegates.PySequence_Concat(pointer, other);

        internal static NewReference PySequence_Repeat(BorrowedReference pointer, nint count) => Delegates.PySequence_Repeat(pointer, count);


        internal static nint PySequence_Index(BorrowedReference pointer, BorrowedReference item) => Delegates.PySequence_Index(pointer, item);

        private static nint PySequence_Count(BorrowedReference pointer, BorrowedReference value) => Delegates.PySequence_Count(pointer, value);


        internal static NewReference PySequence_Tuple(BorrowedReference pointer) => Delegates.PySequence_Tuple(pointer);


        internal static NewReference PySequence_List(BorrowedReference pointer) => Delegates.PySequence_List(pointer);


        //====================================================================
        // Python string API
        //====================================================================
        internal static bool IsStringType(BorrowedReference op)
        {
            BorrowedReference t = PyObject_TYPE(op);
            return (t == PyStringType)
                || (t == PyUnicodeType);
        }

        internal static bool PyString_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == PyStringType;
        }

        internal static NewReference PyString_FromString(string value)
        {
            fixed(char* ptr = value)
                return Delegates.PyUnicode_DecodeUTF16(
                    (IntPtr)ptr,
                    value.Length * sizeof(Char),
                    IntPtr.Zero,
                    IntPtr.Zero
                );
        }


        internal static NewReference EmptyPyBytes()
        {
            byte* bytes = stackalloc byte[1];
            bytes[0] = 0;
            return Delegates.PyBytes_FromString((IntPtr)bytes);
        }

        internal static IntPtr PyBytes_AsString(BorrowedReference ob)
        {
            Debug.Assert(ob != null);
            return Delegates.PyBytes_AsString(ob);
        }

        internal static nint PyBytes_Size(BorrowedReference op) => Delegates.PyBytes_Size(op);

        internal static IntPtr PyUnicode_AsUTF8(BorrowedReference unicode) => Delegates.PyUnicode_AsUTF8(unicode);

        /// <summary>Length in code points</summary>
        internal static nint PyUnicode_GetLength(BorrowedReference ob) => Delegates.PyUnicode_GetLength(ob);


        internal static IntPtr PyUnicode_AsUnicode(BorrowedReference ob) => Delegates.PyUnicode_AsUnicode(ob);
        internal static NewReference PyUnicode_AsUTF16String(BorrowedReference ob) => Delegates.PyUnicode_AsUTF16String(ob);



        internal static NewReference PyUnicode_FromOrdinal(int c) => Delegates.PyUnicode_FromOrdinal(c);

        internal static NewReference PyUnicode_InternFromString(string s)
        {
            using var ptr = new StrPtr(s, Encoding.UTF8);
            return Delegates.PyUnicode_InternFromString(ptr);
        }

        internal static int PyUnicode_Compare(BorrowedReference left, BorrowedReference right) => Delegates.PyUnicode_Compare(left, right);

        internal static string ToString(BorrowedReference op)
        {
            using var strval = PyObject_Str(op);
            return GetManagedStringFromUnicodeObject(strval.BorrowOrThrow())!;
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
        internal static string? GetManagedString(in BorrowedReference op)
        {
            var type = PyObject_TYPE(op);

            if (type == PyUnicodeType)
            {
                return GetManagedStringFromUnicodeObject(op);
            }

            return null;
        }

        static string GetManagedStringFromUnicodeObject(BorrowedReference op)
        {
#if DEBUG
            var type = PyObject_TYPE(op);
            Debug.Assert(type == PyUnicodeType);
#endif
            using var bytes = PyUnicode_AsUTF16String(op);
            if (bytes.IsNull())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            int bytesLength = checked((int)PyBytes_Size(bytes.Borrow()));
            char* codePoints = (char*)PyBytes_AsString(bytes.Borrow());
            return new string(codePoints,
                              startIndex: 1, // skip BOM
                              length: bytesLength / 2 - 1); // utf16 - BOM
        }


        //====================================================================
        // Python dictionary API
        //====================================================================

        internal static bool PyDict_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == PyDictType;
        }


        internal static NewReference PyDict_New() => Delegates.PyDict_New();

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
        internal static int PyDict_SetItem(BorrowedReference dict, BorrowedReference key, BorrowedReference value) => Delegates.PyDict_SetItem(dict, key, value);

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

        internal static int PyMapping_HasKey(BorrowedReference pointer, BorrowedReference key) => Delegates.PyMapping_HasKey(pointer, key);


        internal static NewReference PyDict_Keys(BorrowedReference pointer) => Delegates.PyDict_Keys(pointer);

        internal static NewReference PyDict_Values(BorrowedReference pointer) => Delegates.PyDict_Values(pointer);

        internal static NewReference PyDict_Items(BorrowedReference pointer) => Delegates.PyDict_Items(pointer);


        internal static NewReference PyDict_Copy(BorrowedReference pointer) => Delegates.PyDict_Copy(pointer);


        internal static int PyDict_Update(BorrowedReference pointer, BorrowedReference other) => Delegates.PyDict_Update(pointer, other);


        internal static void PyDict_Clear(BorrowedReference pointer) => Delegates.PyDict_Clear(pointer);

        internal static nint PyDict_Size(BorrowedReference pointer) => Delegates.PyDict_Size(pointer);


        internal static NewReference PySet_New(BorrowedReference iterable) => Delegates.PySet_New(iterable);


        internal static int PySet_Add(BorrowedReference set, BorrowedReference key) => Delegates.PySet_Add(set, key);

        /// <summary>
        /// Return 1 if found, 0 if not found, and -1 if an error is encountered.
        /// </summary>

        internal static int PySet_Contains(BorrowedReference anyset, BorrowedReference key) => Delegates.PySet_Contains(anyset, key);

        //====================================================================
        // Python list API
        //====================================================================

        internal static bool PyList_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == PyListType;
        }

        internal static NewReference PyList_New(nint size) => Delegates.PyList_New(size);

        internal static BorrowedReference PyList_GetItem(BorrowedReference pointer, nint index) => Delegates.PyList_GetItem(pointer, index);

        internal static int PyList_SetItem(BorrowedReference pointer, nint index, StolenReference value) => Delegates.PyList_SetItem(pointer, index, value);

        internal static int PyList_Insert(BorrowedReference pointer, nint index, BorrowedReference value) => Delegates.PyList_Insert(pointer, index, value);


        internal static int PyList_Append(BorrowedReference pointer, BorrowedReference value) => Delegates.PyList_Append(pointer, value);


        internal static int PyList_Reverse(BorrowedReference pointer) => Delegates.PyList_Reverse(pointer);


        internal static int PyList_Sort(BorrowedReference pointer) => Delegates.PyList_Sort(pointer);

        private static NewReference PyList_GetSlice(BorrowedReference pointer, nint start, nint end) => Delegates.PyList_GetSlice(pointer, start, end);

        private static int PyList_SetSlice(BorrowedReference pointer, nint start, nint end, BorrowedReference value) => Delegates.PyList_SetSlice(pointer, start, end, value);


        internal static nint PyList_Size(BorrowedReference pointer) => Delegates.PyList_Size(pointer);

        //====================================================================
        // Python tuple API
        //====================================================================

        internal static bool PyTuple_Check(BorrowedReference ob)
        {
            return PyObject_TYPE(ob) == PyTupleType;
        }
        internal static NewReference PyTuple_New(nint size) => Delegates.PyTuple_New(size);

        internal static BorrowedReference PyTuple_GetItem(BorrowedReference pointer, nint index) => Delegates.PyTuple_GetItem(pointer, index);

        internal static int PyTuple_SetItem(BorrowedReference pointer, nint index, BorrowedReference value)
        {
            var newRef = new NewReference(value);
            return PyTuple_SetItem(pointer, index, newRef.Steal());
        }

        internal static int PyTuple_SetItem(BorrowedReference pointer, nint index, StolenReference value) => Delegates.PyTuple_SetItem(pointer, index, value);

        internal static NewReference PyTuple_GetSlice(BorrowedReference pointer, nint start, nint end) => Delegates.PyTuple_GetSlice(pointer, start, end);

        internal static nint PyTuple_Size(BorrowedReference pointer) => Delegates.PyTuple_Size(pointer);


        //====================================================================
        // Python iterator API
        //====================================================================
        internal static bool PyIter_Check(BorrowedReference ob)
        {
            if (Delegates.PyIter_Check != null)
                return Delegates.PyIter_Check(ob) != 0;
            var ob_type = PyObject_TYPE(ob);
            var tp_iternext = (NativeFunc*)Util.ReadIntPtr(ob_type, TypeOffset.tp_iternext);
            return tp_iternext != (NativeFunc*)0 && tp_iternext != _PyObject_NextNotImplemented;
        }
        internal static NewReference PyIter_Next(BorrowedReference pointer) => Delegates.PyIter_Next(pointer);


        //====================================================================
        // Python module API
        //====================================================================


        internal static NewReference PyModule_New(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyModule_New(namePtr);
        }

        internal static BorrowedReference PyModule_GetDict(BorrowedReference module) => Delegates.PyModule_GetDict(module);

        internal static NewReference PyImport_Import(BorrowedReference name) => Delegates.PyImport_Import(name);

        /// <param name="module">The module to add the object to.</param>
        /// <param name="name">The key that will refer to the object.</param>
        /// <param name="value">The object to add to the module.</param>
        /// <returns>Return -1 on error, 0 on success.</returns>
        internal static int PyModule_AddObject(BorrowedReference module, string name, StolenReference value)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            IntPtr valueAddr = value.DangerousGetAddressOrNull();
            int res = Delegates.PyModule_AddObject(module, namePtr, valueAddr);
            // We can't just exit here because the reference is stolen only on success.
            if (res != 0)
            {
                XDecref(StolenReference.TakeNullable(ref valueAddr));
            }
            return res;

        }

        /// <summary>
        /// Return value: New reference.
        /// </summary>

        internal static NewReference PyImport_ImportModule(string name)
        {
            using var namePtr = new StrPtr(name, Encoding.UTF8);
            return Delegates.PyImport_ImportModule(namePtr);
        }

        internal static NewReference PyImport_ReloadModule(BorrowedReference module) => Delegates.PyImport_ReloadModule(module);


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
        internal static bool PyType_Check(BorrowedReference ob) => PyObject_TypeCheck(ob, PyTypeType);


        internal static void PyType_Modified(BorrowedReference type) => Delegates.PyType_Modified(type);
        internal static bool PyType_IsSubtype(BorrowedReference t1, BorrowedReference t2)
        {
            Debug.Assert(t1 != null && t2 != null);
            return Delegates.PyType_IsSubtype(t1, t2);
        }

        internal static bool PyObject_TypeCheck(BorrowedReference ob, BorrowedReference tp)
        {
            BorrowedReference t = PyObject_TYPE(ob);
            return (t == tp) || PyType_IsSubtype(t, tp);
        }

        internal static bool PyType_IsSameAsOrSubtype(BorrowedReference type, BorrowedReference ofType)
        {
            return (type == ofType) || PyType_IsSubtype(type, ofType);
        }


        internal static NewReference PyType_GenericNew(BorrowedReference type, BorrowedReference args, BorrowedReference kw) => Delegates.PyType_GenericNew(type, args, kw);

        internal static NewReference PyType_GenericAlloc(BorrowedReference type, nint n) => Delegates.PyType_GenericAlloc(type, n);

        internal static IntPtr PyType_GetSlot(BorrowedReference type, TypeSlotID slot) => Delegates.PyType_GetSlot(type, slot);
        internal static NewReference PyType_FromSpecWithBases(in NativeTypeSpec spec, BorrowedReference bases) => Delegates.PyType_FromSpecWithBases(in spec, bases);

        /// <summary>
        /// Finalize a type object. This should be called on all type objects to finish their initialization. This function is responsible for adding inherited slots from a type�s base class. Return 0 on success, or return -1 and sets an exception on error.
        /// </summary>

        internal static int PyType_Ready(BorrowedReference type) => Delegates.PyType_Ready(type);


        internal static BorrowedReference _PyType_Lookup(BorrowedReference type, BorrowedReference name) => Delegates._PyType_Lookup(type, name);


        internal static NewReference PyObject_GenericGetAttr(BorrowedReference obj, BorrowedReference name) => Delegates.PyObject_GenericGetAttr(obj, name);


        internal static int PyObject_GenericSetAttr(BorrowedReference obj, BorrowedReference name, BorrowedReference value) => Delegates.PyObject_GenericSetAttr(obj, name, value);

        internal static NewReference PyObject_GenericGetDict(BorrowedReference o) => PyObject_GenericGetDict(o, IntPtr.Zero);
        internal static NewReference PyObject_GenericGetDict(BorrowedReference o, IntPtr context) => Delegates.PyObject_GenericGetDict(o, context);

        internal static void PyObject_GC_Del(StolenReference ob) => Delegates.PyObject_GC_Del(ob);


        internal static bool PyObject_GC_IsTracked(BorrowedReference ob)
        {
            if (PyVersion >= new Version(3, 9))
                return Delegates.PyObject_GC_IsTracked(ob) != 0;

            throw new NotSupportedException("Requires Python 3.9");
        }

        internal static void PyObject_GC_Track(BorrowedReference ob) => Delegates.PyObject_GC_Track(ob);

        internal static void PyObject_GC_UnTrack(BorrowedReference ob) => Delegates.PyObject_GC_UnTrack(ob);

        internal static void _PyObject_Dump(BorrowedReference ob) => Delegates._PyObject_Dump(ob);

        //====================================================================
        // Python memory API
        //====================================================================

        internal static IntPtr PyMem_Malloc(long size)
        {
            return PyMem_Malloc(new IntPtr(size));
        }


        private static IntPtr PyMem_Malloc(nint size) => Delegates.PyMem_Malloc(size);

        private static IntPtr PyMem_Realloc(IntPtr ptr, nint size) => Delegates.PyMem_Realloc(ptr, size);


        internal static void PyMem_Free(IntPtr ptr) => Delegates.PyMem_Free(ptr);


        //====================================================================
        // Python exception API
        //====================================================================


        internal static void PyErr_SetString(BorrowedReference ob, string message)
        {
            using var msgPtr = new StrPtr(message, Encoding.UTF8);
            Delegates.PyErr_SetString(ob, msgPtr);
        }

        internal static void PyErr_SetObject(BorrowedReference type, BorrowedReference exceptionObject) => Delegates.PyErr_SetObject(type, exceptionObject);

        internal static int PyErr_ExceptionMatches(BorrowedReference exception) => Delegates.PyErr_ExceptionMatches(exception);


        internal static int PyErr_GivenExceptionMatches(BorrowedReference given, BorrowedReference typeOrTypes) => Delegates.PyErr_GivenExceptionMatches(given, typeOrTypes);


        internal static void PyErr_NormalizeException(ref NewReference type, ref NewReference val, ref NewReference tb) => Delegates.PyErr_NormalizeException(ref type, ref val, ref tb);


        internal static BorrowedReference PyErr_Occurred() => Delegates.PyErr_Occurred();


        internal static void PyErr_Fetch(out NewReference type, out NewReference val, out NewReference tb) => Delegates.PyErr_Fetch(out type, out val, out tb);


        internal static void PyErr_Restore(StolenReference type, StolenReference val, StolenReference tb) => Delegates.PyErr_Restore(type, val, tb);


        internal static void PyErr_Clear() => Delegates.PyErr_Clear();


        internal static void PyErr_Print() => Delegates.PyErr_Print();


        internal static NewReference PyException_GetCause(BorrowedReference ex)
            => Delegates.PyException_GetCause(ex);
        internal static NewReference PyException_GetTraceback(BorrowedReference ex)
            => Delegates.PyException_GetTraceback(ex);

        /// <summary>
        /// Set the cause associated with the exception to cause. Use NULL to clear it. There is no type check to make sure that cause is either an exception instance or None. This steals a reference to cause.
        /// </summary>
        internal static void PyException_SetCause(BorrowedReference ex, StolenReference cause)
            => Delegates.PyException_SetCause(ex, cause);
        internal static int PyException_SetTraceback(BorrowedReference ex, BorrowedReference tb)
            => Delegates.PyException_SetTraceback(ex, tb);

        //====================================================================
        // Cell API
        //====================================================================


        internal static NewReference PyCell_Get(BorrowedReference cell) => Delegates.PyCell_Get(cell);


        internal static int PyCell_Set(BorrowedReference cell, BorrowedReference value) => Delegates.PyCell_Set(cell, value);

        //====================================================================
        // Python GC API
        //====================================================================

        internal const int _PyGC_REFS_SHIFT = 1;
        internal const long _PyGC_REFS_UNTRACKED = -2;
        internal const long _PyGC_REFS_REACHABLE = -3;
        internal const long _PyGC_REFS_TENTATIVELY_UNREACHABLE = -4;



        internal static nint PyGC_Collect() => Delegates.PyGC_Collect();
        internal static void Py_CLEAR(BorrowedReference ob, int offset) => ReplaceReference(ob, offset, default);
        internal static void Py_CLEAR<T>(ref T? ob)
            where T: PyObject
        {
            ob?.Dispose();
            ob = null;
        }

        internal static void ReplaceReference(BorrowedReference ob, int offset, StolenReference newValue)
        {
            IntPtr raw = Util.ReadIntPtr(ob, offset);
            Util.WriteNullableRef(ob, offset, newValue);
            XDecref(StolenReference.TakeNullable(ref raw));
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


        internal static int PyThreadState_SetAsyncExcLLP64(uint id, BorrowedReference exc) => Delegates.PyThreadState_SetAsyncExcLLP64(id, exc);

        internal static int PyThreadState_SetAsyncExcLP64(ulong id, BorrowedReference exc) => Delegates.PyThreadState_SetAsyncExcLP64(id, exc);


        internal static void SetNoSiteFlag()
        {
            var loader = LibraryLoader.Instance;
            IntPtr dllLocal = IntPtr.Zero;
            if (_PythonDll != "__Internal")
            {
                dllLocal = loader.Load(_PythonDll);
            }
            try
            {
                Py_NoSiteFlag = (int*)loader.GetFunction(dllLocal, "Py_NoSiteFlag");
                *Py_NoSiteFlag = 1;
            }
            finally
            {
                if (dllLocal != IntPtr.Zero)
                {
                    loader.Free(dllLocal);
                }
            }
        }

        internal static class Delegates
        {
            static readonly ILibraryLoader libraryLoader = LibraryLoader.Instance;

            static Delegates()
            {
                Py_IncRef = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(Py_IncRef), GetUnmanagedDll(_PythonDll));
                Py_DecRef = (delegate* unmanaged[Cdecl]<StolenReference, void>)GetFunctionByName(nameof(Py_DecRef), GetUnmanagedDll(_PythonDll));
                Py_Initialize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Initialize), GetUnmanagedDll(_PythonDll));
                Py_InitializeEx = (delegate* unmanaged[Cdecl]<int, void>)GetFunctionByName(nameof(Py_InitializeEx), GetUnmanagedDll(_PythonDll));
                Py_IsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(Py_IsInitialized), GetUnmanagedDll(_PythonDll));
                Py_Finalize = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(Py_Finalize), GetUnmanagedDll(_PythonDll));
                Py_NewInterpreter = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(Py_NewInterpreter), GetUnmanagedDll(_PythonDll));
                Py_EndInterpreter = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(Py_EndInterpreter), GetUnmanagedDll(_PythonDll));
                PyThreadState_New = (delegate* unmanaged[Cdecl]<PyInterpreterState*, PyThreadState*>)GetFunctionByName(nameof(PyThreadState_New), GetUnmanagedDll(_PythonDll));
                PyThreadState_Get = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyThreadState_Get), GetUnmanagedDll(_PythonDll));
                _PyThreadState_UncheckedGet = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(_PyThreadState_UncheckedGet), GetUnmanagedDll(_PythonDll));
                try
                {
                    PyGILState_Check = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyGILState_Check), GetUnmanagedDll(_PythonDll));
                }
                catch (MissingMethodException e)
                {
                    throw new NotSupportedException(Util.MinimalPythonVersionRequired, innerException: e);
                }
                PyGILState_Ensure = (delegate* unmanaged[Cdecl]<PyGILState>)GetFunctionByName(nameof(PyGILState_Ensure), GetUnmanagedDll(_PythonDll));
                PyGILState_Release = (delegate* unmanaged[Cdecl]<PyGILState, void>)GetFunctionByName(nameof(PyGILState_Release), GetUnmanagedDll(_PythonDll));
                PyGILState_GetThisThreadState = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyGILState_GetThisThreadState), GetUnmanagedDll(_PythonDll));
                Py_Main = (delegate* unmanaged[Cdecl]<int, IntPtr, int>)GetFunctionByName(nameof(Py_Main), GetUnmanagedDll(_PythonDll));
                PyEval_InitThreads = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_InitThreads), GetUnmanagedDll(_PythonDll));
                PyEval_ThreadsInitialized = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(PyEval_ThreadsInitialized), GetUnmanagedDll(_PythonDll));
                PyEval_AcquireLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_AcquireLock), GetUnmanagedDll(_PythonDll));
                PyEval_ReleaseLock = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyEval_ReleaseLock), GetUnmanagedDll(_PythonDll));
                PyEval_AcquireThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_AcquireThread), GetUnmanagedDll(_PythonDll));
                PyEval_ReleaseThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_ReleaseThread), GetUnmanagedDll(_PythonDll));
                PyEval_SaveThread = (delegate* unmanaged[Cdecl]<PyThreadState*>)GetFunctionByName(nameof(PyEval_SaveThread), GetUnmanagedDll(_PythonDll));
                PyEval_RestoreThread = (delegate* unmanaged[Cdecl]<PyThreadState*, void>)GetFunctionByName(nameof(PyEval_RestoreThread), GetUnmanagedDll(_PythonDll));
                PyEval_GetBuiltins = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetBuiltins), GetUnmanagedDll(_PythonDll));
                PyEval_GetGlobals = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetGlobals), GetUnmanagedDll(_PythonDll));
                PyEval_GetLocals = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyEval_GetLocals), GetUnmanagedDll(_PythonDll));
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
                PyEval_EvalCode = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyEval_EvalCode), GetUnmanagedDll(_PythonDll));
                Py_CompileStringObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, NewReference>)GetFunctionByName(nameof(Py_CompileStringObject), GetUnmanagedDll(_PythonDll));
                PyImport_ExecCodeModule = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_ExecCodeModule), GetUnmanagedDll(_PythonDll));
                PyObject_HasAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyObject_HasAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_GetAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, NewReference>)GetFunctionByName(nameof(PyObject_GetAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_SetAttrString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetAttrString), GetUnmanagedDll(_PythonDll));
                PyObject_HasAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_HasAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_SetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetItem), GetUnmanagedDll(_PythonDll));
                PyObject_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_SetItem), GetUnmanagedDll(_PythonDll));
                PyObject_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_DelItem), GetUnmanagedDll(_PythonDll));
                PyObject_GetIter = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GetIter), GetUnmanagedDll(_PythonDll));
                PyObject_Call = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Call), GetUnmanagedDll(_PythonDll));
                PyObject_CallObject = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_CallObject), GetUnmanagedDll(_PythonDll));
                PyObject_RichCompareBool = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int, int>)GetFunctionByName(nameof(PyObject_RichCompareBool), GetUnmanagedDll(_PythonDll));
                PyObject_IsInstance = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsInstance), GetUnmanagedDll(_PythonDll));
                PyObject_IsSubclass = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsSubclass), GetUnmanagedDll(_PythonDll));
                PyCallable_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyCallable_Check), GetUnmanagedDll(_PythonDll));
                PyObject_IsTrue = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_IsTrue), GetUnmanagedDll(_PythonDll));
                PyObject_Not = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_Not), GetUnmanagedDll(_PythonDll));
                PyObject_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PyObject_Size", GetUnmanagedDll(_PythonDll));
                PyObject_Hash = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyObject_Hash), GetUnmanagedDll(_PythonDll));
                PyObject_Repr = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Repr), GetUnmanagedDll(_PythonDll));
                PyObject_Str = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Str), GetUnmanagedDll(_PythonDll));
                PyObject_Type = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Type), GetUnmanagedDll(_PythonDll));
                PyObject_Dir = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_Dir), GetUnmanagedDll(_PythonDll));
                PyObject_GetBuffer = (delegate* unmanaged[Cdecl]<BorrowedReference, out Py_buffer, int, int>)GetFunctionByName(nameof(PyObject_GetBuffer), GetUnmanagedDll(_PythonDll));
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
                PyBuffer_FillInfo = (delegate* unmanaged[Cdecl]<ref Py_buffer, BorrowedReference, IntPtr, IntPtr, int, int, int>)GetFunctionByName(nameof(PyBuffer_FillInfo), GetUnmanagedDll(_PythonDll));
                PyNumber_Long = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Long), GetUnmanagedDll(_PythonDll));
                PyNumber_Float = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Float), GetUnmanagedDll(_PythonDll));
                PyNumber_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, bool>)GetFunctionByName(nameof(PyNumber_Check), GetUnmanagedDll(_PythonDll));
                PyLong_FromLongLong = (delegate* unmanaged[Cdecl]<long, NewReference>)GetFunctionByName(nameof(PyLong_FromLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromUnsignedLongLong = (delegate* unmanaged[Cdecl]<ulong, NewReference>)GetFunctionByName(nameof(PyLong_FromUnsignedLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromString = (delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, NewReference>)GetFunctionByName(nameof(PyLong_FromString), GetUnmanagedDll(_PythonDll));
                PyLong_AsLongLong = (delegate* unmanaged[Cdecl]<BorrowedReference, long>)GetFunctionByName(nameof(PyLong_AsLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedLongLong = (delegate* unmanaged[Cdecl]<BorrowedReference, ulong>)GetFunctionByName(nameof(PyLong_AsUnsignedLongLong), GetUnmanagedDll(_PythonDll));
                PyLong_FromVoidPtr = (delegate* unmanaged[Cdecl]<IntPtr, NewReference>)GetFunctionByName(nameof(PyLong_FromVoidPtr), GetUnmanagedDll(_PythonDll));
                PyLong_AsVoidPtr = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyLong_AsVoidPtr), GetUnmanagedDll(_PythonDll));
                PyFloat_FromDouble = (delegate* unmanaged[Cdecl]<double, NewReference>)GetFunctionByName(nameof(PyFloat_FromDouble), GetUnmanagedDll(_PythonDll));
                PyFloat_FromString = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyFloat_FromString), GetUnmanagedDll(_PythonDll));
                PyFloat_AsDouble = (delegate* unmanaged[Cdecl]<BorrowedReference, double>)GetFunctionByName(nameof(PyFloat_AsDouble), GetUnmanagedDll(_PythonDll));
                PyNumber_Add = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Add), GetUnmanagedDll(_PythonDll));
                PyNumber_Subtract = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Subtract), GetUnmanagedDll(_PythonDll));
                PyNumber_Multiply = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Multiply), GetUnmanagedDll(_PythonDll));
                PyNumber_TrueDivide = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_TrueDivide), GetUnmanagedDll(_PythonDll));
                PyNumber_And = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_And), GetUnmanagedDll(_PythonDll));
                PyNumber_Xor = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Xor), GetUnmanagedDll(_PythonDll));
                PyNumber_Or = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Or), GetUnmanagedDll(_PythonDll));
                PyNumber_Lshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Lshift), GetUnmanagedDll(_PythonDll));
                PyNumber_Rshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Rshift), GetUnmanagedDll(_PythonDll));
                PyNumber_Power = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Power), GetUnmanagedDll(_PythonDll));
                PyNumber_Remainder = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Remainder), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceAdd = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceAdd), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceSubtract = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceSubtract), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceMultiply = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceMultiply), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceTrueDivide = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceTrueDivide), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceAnd = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceAnd), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceXor = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceXor), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceOr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceOr), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceLshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceLshift), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceRshift = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceRshift), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlacePower = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlacePower), GetUnmanagedDll(_PythonDll));
                PyNumber_InPlaceRemainder = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_InPlaceRemainder), GetUnmanagedDll(_PythonDll));
                PyNumber_Negative = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Negative), GetUnmanagedDll(_PythonDll));
                PyNumber_Positive = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Positive), GetUnmanagedDll(_PythonDll));
                PyNumber_Invert = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyNumber_Invert), GetUnmanagedDll(_PythonDll));
                PySequence_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, bool>)GetFunctionByName(nameof(PySequence_Check), GetUnmanagedDll(_PythonDll));
                PySequence_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PySequence_GetItem), GetUnmanagedDll(_PythonDll));
                PySequence_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_SetItem), GetUnmanagedDll(_PythonDll));
                PySequence_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, int>)GetFunctionByName(nameof(PySequence_DelItem), GetUnmanagedDll(_PythonDll));
                PySequence_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PySequence_GetSlice), GetUnmanagedDll(_PythonDll));
                PySequence_SetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_SetSlice), GetUnmanagedDll(_PythonDll));
                PySequence_DelSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, int>)GetFunctionByName(nameof(PySequence_DelSlice), GetUnmanagedDll(_PythonDll));
                PySequence_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Size), GetUnmanagedDll(_PythonDll));
                PySequence_Contains = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySequence_Contains), GetUnmanagedDll(_PythonDll));
                PySequence_Concat = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_Concat), GetUnmanagedDll(_PythonDll));
                PySequence_Repeat = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PySequence_Repeat), GetUnmanagedDll(_PythonDll));
                PySequence_Index = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Index), GetUnmanagedDll(_PythonDll));
                PySequence_Count = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint>)GetFunctionByName(nameof(PySequence_Count), GetUnmanagedDll(_PythonDll));
                PySequence_Tuple = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_Tuple), GetUnmanagedDll(_PythonDll));
                PySequence_List = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySequence_List), GetUnmanagedDll(_PythonDll));
                PyBytes_AsString = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyBytes_AsString), GetUnmanagedDll(_PythonDll));
                PyBytes_FromString = (delegate* unmanaged[Cdecl]<IntPtr, NewReference>)GetFunctionByName(nameof(PyBytes_FromString), GetUnmanagedDll(_PythonDll));
                PyBytes_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyBytes_Size), GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUTF8 = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyUnicode_AsUTF8), GetUnmanagedDll(_PythonDll));
                PyUnicode_DecodeUTF16 = (delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr, IntPtr, NewReference>)GetFunctionByName(nameof(PyUnicode_DecodeUTF16), GetUnmanagedDll(_PythonDll));
                PyUnicode_GetLength = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyUnicode_GetLength), GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUnicode = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyUnicode_AsUnicode), GetUnmanagedDll(_PythonDll));
                PyUnicode_AsUTF16String = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyUnicode_AsUTF16String), GetUnmanagedDll(_PythonDll));
                PyUnicode_FromOrdinal = (delegate* unmanaged[Cdecl]<int, NewReference>)GetFunctionByName(nameof(PyUnicode_FromOrdinal), GetUnmanagedDll(_PythonDll));
                PyUnicode_InternFromString = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyUnicode_InternFromString), GetUnmanagedDll(_PythonDll));
                PyUnicode_Compare = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyUnicode_Compare), GetUnmanagedDll(_PythonDll));
                PyDict_New = (delegate* unmanaged[Cdecl]<NewReference>)GetFunctionByName(nameof(PyDict_New), GetUnmanagedDll(_PythonDll));
                PyDict_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItem), GetUnmanagedDll(_PythonDll));
                PyDict_GetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemString), GetUnmanagedDll(_PythonDll));
                PyDict_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItem), GetUnmanagedDll(_PythonDll));
                PyDict_SetItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_SetItemString), GetUnmanagedDll(_PythonDll));
                PyDict_DelItem = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_DelItem), GetUnmanagedDll(_PythonDll));
                PyDict_DelItemString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int>)GetFunctionByName(nameof(PyDict_DelItemString), GetUnmanagedDll(_PythonDll));
                PyMapping_HasKey = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyMapping_HasKey), GetUnmanagedDll(_PythonDll));
                PyDict_Keys = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Keys), GetUnmanagedDll(_PythonDll));
                PyDict_Values = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Values), GetUnmanagedDll(_PythonDll));
                PyDict_Items = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Items), GetUnmanagedDll(_PythonDll));
                PyDict_Copy = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyDict_Copy), GetUnmanagedDll(_PythonDll));
                PyDict_Update = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyDict_Update), GetUnmanagedDll(_PythonDll));
                PyDict_Clear = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyDict_Clear), GetUnmanagedDll(_PythonDll));
                PyDict_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyDict_Size), GetUnmanagedDll(_PythonDll));
                PySet_New = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PySet_New), GetUnmanagedDll(_PythonDll));
                PySet_Add = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Add), GetUnmanagedDll(_PythonDll));
                PySet_Contains = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PySet_Contains), GetUnmanagedDll(_PythonDll));
                PyList_New = (delegate* unmanaged[Cdecl]<nint, NewReference>)GetFunctionByName(nameof(PyList_New), GetUnmanagedDll(_PythonDll));
                PyList_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyList_GetItem), GetUnmanagedDll(_PythonDll));
                PyList_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int>)GetFunctionByName(nameof(PyList_SetItem), GetUnmanagedDll(_PythonDll));
                PyList_Insert = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int>)GetFunctionByName(nameof(PyList_Insert), GetUnmanagedDll(_PythonDll));
                PyList_Append = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyList_Append), GetUnmanagedDll(_PythonDll));
                PyList_Reverse = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Reverse), GetUnmanagedDll(_PythonDll));
                PyList_Sort = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyList_Sort), GetUnmanagedDll(_PythonDll));
                PyList_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PyList_GetSlice), GetUnmanagedDll(_PythonDll));
                PyList_SetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int>)GetFunctionByName(nameof(PyList_SetSlice), GetUnmanagedDll(_PythonDll));
                PyList_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName(nameof(PyList_Size), GetUnmanagedDll(_PythonDll));
                PyTuple_New = (delegate* unmanaged[Cdecl]<nint, NewReference>)GetFunctionByName(nameof(PyTuple_New), GetUnmanagedDll(_PythonDll));
                PyTuple_GetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, BorrowedReference>)GetFunctionByName(nameof(PyTuple_GetItem), GetUnmanagedDll(_PythonDll));
                PyTuple_SetItem = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int>)GetFunctionByName(nameof(PyTuple_SetItem), GetUnmanagedDll(_PythonDll));
                PyTuple_GetSlice = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference>)GetFunctionByName(nameof(PyTuple_GetSlice), GetUnmanagedDll(_PythonDll));
                PyTuple_Size = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr>)GetFunctionByName(nameof(PyTuple_Size), GetUnmanagedDll(_PythonDll));
                try
                {
                    PyIter_Check = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyIter_Check), GetUnmanagedDll(_PythonDll));
                } catch (MissingMethodException) { }
                PyIter_Next = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyIter_Next), GetUnmanagedDll(_PythonDll));
                PyModule_New = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyModule_New), GetUnmanagedDll(_PythonDll));
                PyModule_GetDict = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyModule_GetDict), GetUnmanagedDll(_PythonDll));
                PyModule_AddObject = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, IntPtr, int>)GetFunctionByName(nameof(PyModule_AddObject), GetUnmanagedDll(_PythonDll));
                PyImport_Import = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_Import), GetUnmanagedDll(_PythonDll));
                PyImport_ImportModule = (delegate* unmanaged[Cdecl]<StrPtr, NewReference>)GetFunctionByName(nameof(PyImport_ImportModule), GetUnmanagedDll(_PythonDll));
                PyImport_ReloadModule = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyImport_ReloadModule), GetUnmanagedDll(_PythonDll));
                PyImport_AddModule = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PyImport_AddModule), GetUnmanagedDll(_PythonDll));
                PyImport_GetModuleDict = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyImport_GetModuleDict), GetUnmanagedDll(_PythonDll));
                PySys_SetArgvEx = (delegate* unmanaged[Cdecl]<int, IntPtr, int, void>)GetFunctionByName(nameof(PySys_SetArgvEx), GetUnmanagedDll(_PythonDll));
                PySys_GetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference>)GetFunctionByName(nameof(PySys_GetObject), GetUnmanagedDll(_PythonDll));
                PySys_SetObject = (delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int>)GetFunctionByName(nameof(PySys_SetObject), GetUnmanagedDll(_PythonDll));
                PyType_Modified = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyType_Modified), GetUnmanagedDll(_PythonDll));
                PyType_IsSubtype = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool>)GetFunctionByName(nameof(PyType_IsSubtype), GetUnmanagedDll(_PythonDll));
                PyType_GenericNew = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyType_GenericNew), GetUnmanagedDll(_PythonDll));
                PyType_GenericAlloc = (delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference>)GetFunctionByName(nameof(PyType_GenericAlloc), GetUnmanagedDll(_PythonDll));
                PyType_Ready = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyType_Ready), GetUnmanagedDll(_PythonDll));
                _PyType_Lookup = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(_PyType_Lookup), GetUnmanagedDll(_PythonDll));
                PyObject_GenericGetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyObject_GenericGetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GenericGetDict = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, NewReference>)GetFunctionByName(nameof(PyObject_GenericGetDict), GetUnmanagedDll(PythonDLL));
                PyObject_GenericSetAttr = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyObject_GenericSetAttr), GetUnmanagedDll(_PythonDll));
                PyObject_GC_Del = (delegate* unmanaged[Cdecl]<StolenReference, void>)GetFunctionByName(nameof(PyObject_GC_Del), GetUnmanagedDll(_PythonDll));
                try
                {
                    PyObject_GC_IsTracked = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyObject_GC_IsTracked), GetUnmanagedDll(_PythonDll));
                } catch (MissingMethodException) { }
                PyObject_GC_Track = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyObject_GC_Track), GetUnmanagedDll(_PythonDll));
                PyObject_GC_UnTrack = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(PyObject_GC_UnTrack), GetUnmanagedDll(_PythonDll));
                _PyObject_Dump = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(_PyObject_Dump), GetUnmanagedDll(_PythonDll));
                PyMem_Malloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Malloc), GetUnmanagedDll(_PythonDll));
                PyMem_Realloc = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)GetFunctionByName(nameof(PyMem_Realloc), GetUnmanagedDll(_PythonDll));
                PyMem_Free = (delegate* unmanaged[Cdecl]<IntPtr, void>)GetFunctionByName(nameof(PyMem_Free), GetUnmanagedDll(_PythonDll));
                PyErr_SetString = (delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, void>)GetFunctionByName(nameof(PyErr_SetString), GetUnmanagedDll(_PythonDll));
                PyErr_SetObject = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void>)GetFunctionByName(nameof(PyErr_SetObject), GetUnmanagedDll(_PythonDll));
                PyErr_ExceptionMatches = (delegate* unmanaged[Cdecl]<BorrowedReference, int>)GetFunctionByName(nameof(PyErr_ExceptionMatches), GetUnmanagedDll(_PythonDll));
                PyErr_GivenExceptionMatches = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyErr_GivenExceptionMatches), GetUnmanagedDll(_PythonDll));
                PyErr_NormalizeException = (delegate* unmanaged[Cdecl]<ref NewReference, ref NewReference, ref NewReference, void>)GetFunctionByName(nameof(PyErr_NormalizeException), GetUnmanagedDll(_PythonDll));
                PyErr_Occurred = (delegate* unmanaged[Cdecl]<BorrowedReference>)GetFunctionByName(nameof(PyErr_Occurred), GetUnmanagedDll(_PythonDll));
                PyErr_Fetch = (delegate* unmanaged[Cdecl]<out NewReference, out NewReference, out NewReference, void>)GetFunctionByName(nameof(PyErr_Fetch), GetUnmanagedDll(_PythonDll));
                PyErr_Restore = (delegate* unmanaged[Cdecl]<StolenReference, StolenReference, StolenReference, void>)GetFunctionByName(nameof(PyErr_Restore), GetUnmanagedDll(_PythonDll));
                PyErr_Clear = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Clear), GetUnmanagedDll(_PythonDll));
                PyErr_Print = (delegate* unmanaged[Cdecl]<void>)GetFunctionByName(nameof(PyErr_Print), GetUnmanagedDll(_PythonDll));
                PyCell_Get = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyCell_Get), GetUnmanagedDll(_PythonDll));
                PyCell_Set = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyCell_Set), GetUnmanagedDll(_PythonDll));
                PyGC_Collect = (delegate* unmanaged[Cdecl]<nint>)GetFunctionByName(nameof(PyGC_Collect), GetUnmanagedDll(_PythonDll));
                PyCapsule_New = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference>)GetFunctionByName(nameof(PyCapsule_New), GetUnmanagedDll(_PythonDll));
                PyCapsule_GetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr>)GetFunctionByName(nameof(PyCapsule_GetPointer), GetUnmanagedDll(_PythonDll));
                PyCapsule_SetPointer = (delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int>)GetFunctionByName(nameof(PyCapsule_SetPointer), GetUnmanagedDll(_PythonDll));
                PyLong_AsUnsignedSize_t = (delegate* unmanaged[Cdecl]<BorrowedReference, nuint>)GetFunctionByName("PyLong_AsSize_t", GetUnmanagedDll(_PythonDll));
                PyLong_AsSignedSize_t = (delegate* unmanaged[Cdecl]<BorrowedReference, nint>)GetFunctionByName("PyLong_AsSsize_t", GetUnmanagedDll(_PythonDll));
                PyDict_GetItemWithError = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference>)GetFunctionByName(nameof(PyDict_GetItemWithError), GetUnmanagedDll(_PythonDll));
                PyException_GetCause = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyException_GetCause), GetUnmanagedDll(_PythonDll));
                PyException_GetTraceback = (delegate* unmanaged[Cdecl]<BorrowedReference, NewReference>)GetFunctionByName(nameof(PyException_GetTraceback), GetUnmanagedDll(_PythonDll));
                PyException_SetCause = (delegate* unmanaged[Cdecl]<BorrowedReference, StolenReference, void>)GetFunctionByName(nameof(PyException_SetCause), GetUnmanagedDll(_PythonDll));
                PyException_SetTraceback = (delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int>)GetFunctionByName(nameof(PyException_SetTraceback), GetUnmanagedDll(_PythonDll));
                PyThreadState_SetAsyncExcLLP64 = (delegate* unmanaged[Cdecl]<uint, BorrowedReference, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
                PyThreadState_SetAsyncExcLP64 = (delegate* unmanaged[Cdecl]<ulong, BorrowedReference, int>)GetFunctionByName("PyThreadState_SetAsyncExc", GetUnmanagedDll(_PythonDll));
                PyType_GetSlot = (delegate* unmanaged[Cdecl]<BorrowedReference, TypeSlotID, IntPtr>)GetFunctionByName(nameof(PyType_GetSlot), GetUnmanagedDll(_PythonDll));
                PyType_FromSpecWithBases = (delegate* unmanaged[Cdecl]<in NativeTypeSpec, BorrowedReference, NewReference>)GetFunctionByName(nameof(PyType_FromSpecWithBases), GetUnmanagedDll(PythonDLL));

                try
                {
                    _Py_NewReference = (delegate* unmanaged[Cdecl]<BorrowedReference, void>)GetFunctionByName(nameof(_Py_NewReference), GetUnmanagedDll(_PythonDll));
                }
                catch (MissingMethodException) { }
                try
                {
                    _Py_IsFinalizing = (delegate* unmanaged[Cdecl]<int>)GetFunctionByName(nameof(_Py_IsFinalizing), GetUnmanagedDll(_PythonDll));
                }
                catch (MissingMethodException) { }

                PyType_Type = GetFunctionByName(nameof(PyType_Type), GetUnmanagedDll(_PythonDll));
            }

            static global::System.IntPtr GetUnmanagedDll(string? libraryName)
            {
                if (libraryName is null) return IntPtr.Zero;
                return libraryLoader.Load(libraryName);
            }

            static global::System.IntPtr GetFunctionByName(string functionName, global::System.IntPtr libraryHandle)
            {
                try
                {
                    return libraryLoader.GetFunction(libraryHandle, functionName);
                }
                catch (MissingMethodException e) when (libraryHandle == IntPtr.Zero)
                {
                    throw new BadPythonDllException(
                        "Runtime.PythonDLL was not set or does not point to a supported Python runtime DLL." +
                        " See https://github.com/pythonnet/pythonnet#embedding-python-in-net",
                        e);
                }
            }

            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> Py_IncRef { get; }
            internal static delegate* unmanaged[Cdecl]<StolenReference, void> Py_DecRef { get; }
            internal static delegate* unmanaged[Cdecl]<void> Py_Initialize { get; }
            internal static delegate* unmanaged[Cdecl]<int, void> Py_InitializeEx { get; }
            internal static delegate* unmanaged[Cdecl]<int> Py_IsInitialized { get; }
            internal static delegate* unmanaged[Cdecl]<void> Py_Finalize { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*> Py_NewInterpreter { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> Py_EndInterpreter { get; }
            internal static delegate* unmanaged[Cdecl]<PyInterpreterState*, PyThreadState*> PyThreadState_New { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyThreadState_Get { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*> _PyThreadState_UncheckedGet { get; }
            internal static delegate* unmanaged[Cdecl]<int> PyGILState_Check { get; }
            internal static delegate* unmanaged[Cdecl]<PyGILState> PyGILState_Ensure { get; }
            internal static delegate* unmanaged[Cdecl]<PyGILState, void> PyGILState_Release { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyGILState_GetThisThreadState { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, int> Py_Main { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_InitThreads { get; }
            internal static delegate* unmanaged[Cdecl]<int> PyEval_ThreadsInitialized { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_AcquireLock { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyEval_ReleaseLock { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_AcquireThread { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_ReleaseThread { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*> PyEval_SaveThread { get; }
            internal static delegate* unmanaged[Cdecl]<PyThreadState*, void> PyEval_RestoreThread { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetBuiltins { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetGlobals { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyEval_GetLocals { get; }
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
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyEval_EvalCode { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int, in PyCompilerFlags, int, NewReference> Py_CompileStringObject { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, NewReference> PyImport_ExecCodeModule { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyObject_HasAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, NewReference> PyObject_GetAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int> PyObject_SetAttrString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_HasAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_SetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_GetIter { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyObject_Call { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_CallObject { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int, int> PyObject_RichCompareBool { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_IsInstance { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyObject_IsSubclass { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyCallable_Check { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_IsTrue { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_Not { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyObject_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyObject_Hash { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Repr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Str { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Type { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyObject_Dir { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, out Py_buffer, int, int> PyObject_GetBuffer { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, void> PyBuffer_Release { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, nint> PyBuffer_SizeFromFormat { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, char, int> PyBuffer_IsContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr[], IntPtr> PyBuffer_GetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, IntPtr, IntPtr, char, int> PyBuffer_FromContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, ref Py_buffer, IntPtr, char, int> PyBuffer_ToContiguous { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, IntPtr, int, char, void> PyBuffer_FillContiguousStrides { get; }
            internal static delegate* unmanaged[Cdecl]<ref Py_buffer, BorrowedReference, IntPtr, IntPtr, int, int, int> PyBuffer_FillInfo { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Long { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Float { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, bool> PyNumber_Check { get; }
            internal static delegate* unmanaged[Cdecl]<long, NewReference> PyLong_FromLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<ulong, NewReference> PyLong_FromUnsignedLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, IntPtr, int, NewReference> PyLong_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, long> PyLong_AsLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, ulong> PyLong_AsUnsignedLongLong { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, NewReference> PyLong_FromVoidPtr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyLong_AsVoidPtr { get; }
            internal static delegate* unmanaged[Cdecl]<double, NewReference> PyFloat_FromDouble { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyFloat_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, double> PyFloat_AsDouble { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Add { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Subtract { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Multiply { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_TrueDivide { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_And { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Xor { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Or { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Lshift { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Rshift { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Power { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_Remainder { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceAdd { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceSubtract { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceMultiply { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceTrueDivide { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceAnd { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceXor { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceOr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceLshift { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceRshift { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlacePower { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyNumber_InPlaceRemainder { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Negative { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Positive { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyNumber_Invert { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, bool> PySequence_Check { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PySequence_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int> PySequence_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, int> PySequence_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PySequence_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int> PySequence_SetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, int> PySequence_DelSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PySequence_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySequence_Contains { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PySequence_Concat { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PySequence_Repeat { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint> PySequence_Index { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, nint> PySequence_Count { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySequence_Tuple { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySequence_List { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyBytes_AsString { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, NewReference> PyBytes_FromString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyBytes_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyUnicode_AsUTF8 { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr, IntPtr, NewReference> PyUnicode_DecodeUTF16 { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyUnicode_GetLength { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr> PyUnicode_AsUnicode { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyUnicode_AsUTF16String { get; }
            internal static delegate* unmanaged[Cdecl]<int, NewReference> PyUnicode_FromOrdinal { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyUnicode_InternFromString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyUnicode_Compare { get; }
            internal static delegate* unmanaged[Cdecl]<NewReference> PyDict_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference> PyDict_GetItemString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyDict_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, BorrowedReference, int> PyDict_SetItemString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_DelItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, int> PyDict_DelItemString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyMapping_HasKey { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Keys { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Values { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Items { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyDict_Copy { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyDict_Update { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyDict_Clear { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyDict_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PySet_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Add { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PySet_Contains { get; }
            internal static delegate* unmanaged[Cdecl]<nint, NewReference> PyList_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference> PyList_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int> PyList_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference, int> PyList_Insert { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyList_Append { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Reverse { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyList_Sort { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PyList_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, BorrowedReference, int> PyList_SetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyList_Size { get; }
            internal static delegate* unmanaged[Cdecl]<nint, NewReference> PyTuple_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, BorrowedReference> PyTuple_GetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, StolenReference, int> PyTuple_SetItem { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, nint, NewReference> PyTuple_GetSlice { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyTuple_Size { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyIter_Check { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyIter_Next { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyModule_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference> PyModule_GetDict { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, IntPtr, int> PyModule_AddObject { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyImport_Import { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, NewReference> PyImport_ImportModule { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyImport_ReloadModule { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PyImport_AddModule { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyImport_GetModuleDict { get; }
            internal static delegate* unmanaged[Cdecl]<int, IntPtr, int, void> PySys_SetArgvEx { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference> PySys_GetObject { get; }
            internal static delegate* unmanaged[Cdecl]<StrPtr, BorrowedReference, int> PySys_SetObject { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyType_Modified { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, bool> PyType_IsSubtype { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, NewReference> PyType_GenericNew { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint, NewReference> PyType_GenericAlloc { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyType_Ready { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> _PyType_Lookup { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, NewReference> PyObject_GenericGetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference, int> PyObject_GenericSetAttr { get; }
            internal static delegate* unmanaged[Cdecl]<StolenReference, void> PyObject_GC_Del { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyObject_GC_IsTracked { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyObject_GC_Track { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> PyObject_GC_UnTrack { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> _PyObject_Dump { get; }
            internal static delegate* unmanaged[Cdecl]<nint, IntPtr> PyMem_Malloc { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, nint, IntPtr> PyMem_Realloc { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, void> PyMem_Free { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StrPtr, void> PyErr_SetString { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, void> PyErr_SetObject { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, int> PyErr_ExceptionMatches { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyErr_GivenExceptionMatches { get; }
            internal static delegate* unmanaged[Cdecl]<ref NewReference, ref NewReference, ref NewReference, void> PyErr_NormalizeException { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference> PyErr_Occurred { get; }
            internal static delegate* unmanaged[Cdecl]<out NewReference, out NewReference, out NewReference, void> PyErr_Fetch { get; }
            internal static delegate* unmanaged[Cdecl]<StolenReference, StolenReference, StolenReference, void> PyErr_Restore { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyErr_Clear { get; }
            internal static delegate* unmanaged[Cdecl]<void> PyErr_Print { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyCell_Get { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyCell_Set { get; }
            internal static delegate* unmanaged[Cdecl]<nint> PyGC_Collect { get; }
            internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, NewReference> PyCapsule_New { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, IntPtr> PyCapsule_GetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, int> PyCapsule_SetPointer { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nuint> PyLong_AsUnsignedSize_t { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, nint> PyLong_AsSignedSize_t { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, BorrowedReference> PyDict_GetItemWithError { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyException_GetCause { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, NewReference> PyException_GetTraceback { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, StolenReference, void> PyException_SetCause { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, BorrowedReference, int> PyException_SetTraceback { get; }
            internal static delegate* unmanaged[Cdecl]<uint, BorrowedReference, int> PyThreadState_SetAsyncExcLLP64 { get; }
            internal static delegate* unmanaged[Cdecl]<ulong, BorrowedReference, int> PyThreadState_SetAsyncExcLP64 { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, IntPtr, NewReference> PyObject_GenericGetDict { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, TypeSlotID, IntPtr> PyType_GetSlot { get; }
            internal static delegate* unmanaged[Cdecl]<in NativeTypeSpec, BorrowedReference, NewReference> PyType_FromSpecWithBases { get; }
            internal static delegate* unmanaged[Cdecl]<BorrowedReference, void> _Py_NewReference { get; }
            internal static delegate* unmanaged[Cdecl]<int> _Py_IsFinalizing { get; }
            internal static IntPtr PyType_Type { get; }
        }
    }

    internal class BadPythonDllException : MissingMethodException
    {
        public BadPythonDllException(string message, Exception innerException)
            : base(message, innerException) { }
    }


    public enum ShutdownMode
    {
        Default,
        Normal,
        Soft,
        Reload,
        Extension,
    }
}
