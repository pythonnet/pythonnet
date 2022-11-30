using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Python.Runtime.Native;
using System.Linq;
using static System.FormattableString;

namespace Python.Runtime
{
    /// <summary>
    /// Encapsulates the low-level Python C API. Note that it is
    /// the responsibility of the caller to have acquired the GIL
    /// before calling any of these methods.
    /// </summary>
    public unsafe partial class Runtime
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
        internal static bool IsInitialized => _isInitialized;
        private static bool _typesInitialized = false;
        internal static bool TypeManagerInitialized => _typesInitialized;
        internal static readonly bool Is32Bit = IntPtr.Size == 4;

        // Available in newer .NET Core versions (>= 5) as IntPtr.MaxValue etc.
        internal static readonly long IntPtrMaxValue = Is32Bit ? Int32.MaxValue : Int64.MaxValue;
        internal static readonly long IntPtrMinValue = Is32Bit ? Int32.MinValue : Int64.MinValue;
        internal static readonly ulong UIntPtrMaxValue = Is32Bit ? UInt32.MaxValue : UInt64.MaxValue;

        // .NET core: System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        internal static bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        internal static Version InteropVersion { get; }
            = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        public static int MainManagedThreadId { get; private set; }

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

        internal static bool HostedInPython;
        internal static bool ProcessIsTerminating;

        /// <summary>
        /// Initialize the runtime...
        /// </summary>
        /// <remarks>Always call this method from the Main thread.  After the
        /// first call to this method, the main thread has acquired the GIL.</remarks>
        internal static void Initialize(bool initSigs = false)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

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
                RuntimeState.Save();
            }
            else
            {
                if (!HostedInPython)
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
            _typesInitialized = true;

            // Initialize modules that depend on the runtime class.
            AssemblyManager.Initialize();
            OperatorMethod.Initialize();
            if (RuntimeData.HasStashData())
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
            hexCallable = new(() => new PyString("%x").GetAttr("__mod__"));
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

        internal static void Shutdown()
        {
            if (Py_IsInitialized() == 0 || !_isInitialized)
            {
                return;
            }
            _isInitialized = false;

            var state = PyGILState_Ensure();

            if (!HostedInPython && !ProcessIsTerminating)
            {
                // avoid saving dead objects
                TryCollectingGarbage(runs: 3);

                RuntimeData.Stash();
            }

            AssemblyManager.Shutdown();
            OperatorMethod.Shutdown();
            ImportHook.Shutdown();

            ClearClrModules();
            RemoveClrRootModule();

            NullGCHandles(ExtensionType.loadedExtensions);
            ClassManager.RemoveClasses();
            TypeManager.RemoveTypes();
            _typesInitialized = false;

            MetaType.Release();
            PyCLRMetaType.Dispose();
            PyCLRMetaType = null!;

            Exceptions.Shutdown();
            PythonEngine.InteropConfiguration.Dispose();
            DisposeLazyObject(clrInterop);
            DisposeLazyObject(inspect);
            DisposeLazyObject(hexCallable);
            PyObjectConversions.Reset();

            PyGC_Collect();
            bool everythingSeemsCollected = TryCollectingGarbage(MaxCollectRetriesOnShutdown,
                                                                 forceBreakLoops: true);
            Debug.Assert(everythingSeemsCollected);

            Finalizer.Shutdown();
            InternString.Shutdown();

            ResetPyMembers();

            if (!HostedInPython)
            {
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

                ExtensionType.loadedExtensions.Clear();
                CLRObject.reflectedObjects.Clear();
            }
            else
            {
                PyGILState_Release(state);
            }
        }

        const int MaxCollectRetriesOnShutdown = 20;
        internal static int _collected;
        static bool TryCollectingGarbage(int runs, bool forceBreakLoops)
        {
            if (runs <= 0) throw new ArgumentOutOfRangeException(nameof(runs));

            for (int attempt = 0; attempt < runs; attempt++)
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
                    if (attempt + 1 == runs) return true;
                }
                else if (forceBreakLoops)
                {
                    NullGCHandles(CLRObject.reflectedObjects);
                    CLRObject.reflectedObjects.Clear();
                }
            }
            return false;
        }
        /// <summary>
        /// Alternates .NET and Python GC runs in an attempt to collect all garbage
        /// </summary>
        /// <param name="runs">Total number of GC loops to run</param>
        /// <returns><c>true</c> if a steady state was reached upon the requested number of tries (e.g. on the last try no objects were collected).</returns>
        [ForbidPythonThreads]
        public static bool TryCollectingGarbage(int runs)
            => TryCollectingGarbage(runs, forceBreakLoops: false);

        static void DisposeLazyObject(Lazy<PyObject> pyObject)
        {
            if (pyObject.IsValueCreated)
            {
                pyObject.Value.Dispose();
            }
        }

        private static Lazy<PyObject> GetModuleLazy(string moduleName)
            => moduleName is null
                ? throw new ArgumentNullException(nameof(moduleName))
                : new Lazy<PyObject>(() => PyModule.Import(moduleName), isThreadSafe: false);

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

        private static Lazy<PyObject> hexCallable;
        internal static PyObject HexCallable => hexCallable.Value;
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

                if (mt is ClassBase b)
                {
                    var _type = b.type;
                    t = _type.Valid ?  _type.Value : null;
                }
                else if (mt is CLRObject ob)
                {
                    var inst = ob.inst;
                    if (inst is Type ty)
                    {
                        t = ty;
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

        internal static void TryUsingDll(Action op) =>
            TryUsingDll(() => { op(); return 0; });

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


        internal static int PyObject_IsInstance(BorrowedReference ob, BorrowedReference type) => Delegates.PyObject_IsInstance(ob, type);


        internal static int PyObject_IsSubclass(BorrowedReference ob, BorrowedReference type) => Delegates.PyObject_IsSubclass(ob, type);

        internal static void PyObject_ClearWeakRefs(BorrowedReference ob) => Delegates.PyObject_ClearWeakRefs(ob);

        internal static BorrowedReference PyObject_GetWeakRefList(BorrowedReference ob)
        {
            Debug.Assert(ob != null);
            var type = PyObject_TYPE(ob);
            int offset = Util.ReadInt32(type, TypeOffset.tp_weaklistoffset);
            if (offset == 0) return BorrowedReference.Null;
            Debug.Assert(offset > 0);
            return Util.ReadRef(ob, offset);
        }


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


        internal static IntPtr PyBuffer_GetPointer(ref Py_buffer view, nint[] indices) => Delegates.PyBuffer_GetPointer(ref view, indices);


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

        internal static bool PyInt_CheckExact(BorrowedReference ob)
            => PyObject_TypeCheckExact(ob, PyLongType);

        internal static bool PyBool_Check(BorrowedReference ob)
            => PyObject_TypeCheck(ob, PyBoolType);
        internal static bool PyBool_CheckExact(BorrowedReference ob)
            => PyObject_TypeCheckExact(ob, PyBoolType);

        internal static NewReference PyInt_FromInt32(int value) => PyLong_FromLongLong(value);

        internal static NewReference PyInt_FromInt64(long value) => PyLong_FromLongLong(value);

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
            => PyObject_TypeCheck(ob, PyFloatType);
        internal static bool PyFloat_CheckExact(BorrowedReference ob)
            => PyObject_TypeCheckExact(ob, PyFloatType);

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
        internal static bool PyString_Check(BorrowedReference ob)
            => PyObject_TypeCheck(ob, PyStringType);
        internal static bool PyString_CheckExact(BorrowedReference ob)
            => PyObject_TypeCheckExact(ob, PyStringType);

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

        internal static NewReference PyByteArray_FromStringAndSize(IntPtr strPtr, nint len) => Delegates.PyByteArray_FromStringAndSize(strPtr, len);
        internal static NewReference PyByteArray_FromStringAndSize(string s)
        {
            using var ptr = new StrPtr(s, Encoding.UTF8);
            return PyByteArray_FromStringAndSize(ptr.RawPointer, checked((nint)ptr.ByteCount));
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

        internal static bool PyObject_TypeCheckExact(BorrowedReference ob, BorrowedReference tp)
            => PyObject_TYPE(ob) == tp;
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
            TryUsingDll(() =>
            {
                *Delegates.Py_NoSiteFlag = 1;
                return *Delegates.Py_NoSiteFlag;
            });
        }
    }

    internal class BadPythonDllException : MissingMethodException
    {
        public BadPythonDllException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
