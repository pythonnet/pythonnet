using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{
    /// <summary>
    /// This class provides the public interface of the Python runtime.
    /// </summary>
    public class PythonEngine : IDisposable
    {
        private static DelegateManager? delegateManager;
        private static bool initialized;
        private static IntPtr _pythonHome = IntPtr.Zero;
        private static IntPtr _programName = IntPtr.Zero;
        private static IntPtr _pythonPath = IntPtr.Zero;
        private static InteropConfiguration interopConfiguration = InteropConfiguration.MakeDefault();

        public PythonEngine()
        {
            Initialize();
        }

        public PythonEngine(params string[] args)
        {
            Initialize(args);
        }

        public PythonEngine(IEnumerable<string> args)
        {
            Initialize(args);
        }

        public void Dispose()
        {
            Shutdown();
        }

        public static bool IsInitialized
        {
            get { return initialized; }
        }

        private static void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Python must be initialized for this operation"
                );
        }

        /// <summary>Set to <c>true</c> to enable GIL debugging assistance.</summary>
        public static bool DebugGIL { get; set; } = false;

        internal static DelegateManager DelegateManager
        {
            get
            {
                if (delegateManager == null)
                {
                    throw new InvalidOperationException(
                        "DelegateManager has not yet been initialized using Python.Runtime.PythonEngine.Initialize().");
                }
                return delegateManager;
            }
        }

        public static InteropConfiguration InteropConfiguration
        {
            get => interopConfiguration;
            set
            {
                if (IsInitialized)
                    throw new NotSupportedException("Changing interop configuration when engine is running is not supported");

                interopConfiguration = value ?? throw new ArgumentNullException(nameof(InteropConfiguration));
            }
        }

        public static string ProgramName
        {
            get
            {
                IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetProgramName());
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                Marshal.FreeHGlobal(_programName);
                _programName = Runtime.TryUsingDll(
                    () => UcsMarshaler.Py3UnicodePy2StringtoPtr(value)
                );
                Runtime.Py_SetProgramName(_programName);
            }
        }

        public static string PythonHome
        {
            get
            {
                EnsureInitialized();
                IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetPythonHome());
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                // this value is null in the beginning
                Marshal.FreeHGlobal(_pythonHome);
                _pythonHome = UcsMarshaler.Py3UnicodePy2StringtoPtr(value);
                Runtime.TryUsingDll(() => Runtime.Py_SetPythonHome(_pythonHome));
            }
        }

        public static string PythonPath
        {
            get
            {
                IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetPath());
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                Marshal.FreeHGlobal(_pythonPath);
                _pythonPath = Runtime.TryUsingDll(
                    () => UcsMarshaler.Py3UnicodePy2StringtoPtr(value)
                );
                Runtime.Py_SetPath(_pythonPath);
            }
        }

        public static Version MinSupportedVersion => new(3, 7);
        public static Version MaxSupportedVersion => new(3, 11, int.MaxValue, int.MaxValue);
        public static bool IsSupportedVersion(Version version) => version >= MinSupportedVersion && version <= MaxSupportedVersion;

        public static string Version
        {
            get { return Marshal.PtrToStringAnsi(Runtime.Py_GetVersion()); }
        }

        public static string BuildInfo
        {
            get { return Marshal.PtrToStringAnsi(Runtime.Py_GetBuildInfo()); }
        }

        public static string Platform
        {
            get { return Marshal.PtrToStringAnsi(Runtime.Py_GetPlatform()); }
        }

        public static string Copyright
        {
            get { return Marshal.PtrToStringAnsi(Runtime.Py_GetCopyright()); }
        }

        public static string Compiler
        {
            get { return Marshal.PtrToStringAnsi(Runtime.Py_GetCompiler()); }
        }

        /// <summary>
        /// Set the NoSiteFlag to disable loading the site module.
        /// Must be called before Initialize.
        /// https://docs.python.org/3/c-api/init.html#c.Py_NoSiteFlag
        /// </summary>
        public static void SetNoSiteFlag()
        {
            Runtime.SetNoSiteFlag();
        }

        public static int RunSimpleString(string code)
        {
            return Runtime.PyRun_SimpleString(code);
        }

        public static void Initialize()
        {
            Initialize(setSysArgv: true);
        }

        public static void Initialize(bool setSysArgv = true, bool initSigs = false)
        {
            Initialize(Enumerable.Empty<string>(), setSysArgv: setSysArgv, initSigs: initSigs);
        }

        /// <summary>
        /// Initialize Method
        /// </summary>
        /// <remarks>
        /// Initialize the Python runtime. It is safe to call this method
        /// more than once, though initialization will only happen on the
        /// first call. It is *not* necessary to hold the Python global
        /// interpreter lock (GIL) to call this method.
        /// initSigs can be set to 1 to do default python signal configuration. This will override the way signals are handled by the application.
        /// </remarks>
        public static void Initialize(IEnumerable<string> args, bool setSysArgv = true, bool initSigs = false)
        {
            if (initialized)
            {
                return;
            }
            // Creating the delegateManager MUST happen before Runtime.Initialize
            // is called. If it happens afterwards, DelegateManager's CodeGenerator
            // throws an exception in its ctor.  This exception is eaten somehow
            // during an initial "import clr", and the world ends shortly thereafter.
            // This is probably masking some bad mojo happening somewhere in Runtime.Initialize().
            delegateManager = new DelegateManager();
            Runtime.Initialize(initSigs);
            initialized = true;
            Exceptions.Clear();

            // Make sure we clean up properly on app domain unload.
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            if (setSysArgv)
            {
                Py.SetArgv(args);
            }

            // Load the clr.py resource into the clr module
            BorrowedReference clr_dict = Runtime.PyModule_GetDict(ImportHook.ClrModuleReference);

            var locals = new PyDict();
            try
            {
                BorrowedReference module = DefineModule("clr._extras");
                BorrowedReference module_globals = Runtime.PyModule_GetDict(module);

                Assembly assembly = Assembly.GetExecutingAssembly();
                // add the contents of clr.py to the module
                string clr_py = assembly.ReadStringResource("clr.py");

                Exec(clr_py, module_globals, locals.Reference);

                LoadSubmodule(module_globals, "clr.interop", "interop.py");

                LoadMixins(module_globals);

                // add the imported module to the clr module, and copy the API functions
                // and decorators into the main clr module.
                Runtime.PyDict_SetItemString(clr_dict, "_extras", module);

                // append version
                var version = typeof(PythonEngine)
                    .Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion;
                using var versionObj = Runtime.PyString_FromString(version);
                Runtime.PyDict_SetItemString(clr_dict, "__version__", versionObj.Borrow());

                using var keys = locals.Keys();
                foreach (PyObject key in keys)
                {
                    if (!key.ToString()!.StartsWith("_"))
                    {
                        using PyObject value = locals[key];
                        Runtime.PyDict_SetItem(clr_dict, key.Reference, value.Reference);
                    }
                    key.Dispose();
                }
            }
            finally
            {
                locals.Dispose();
            }

            ImportHook.UpdateCLRModuleDict();
        }

        static BorrowedReference DefineModule(string name)
        {
            var module = Runtime.PyImport_AddModule(name);
            var module_globals = Runtime.PyModule_GetDict(module);
            var builtins = Runtime.PyEval_GetBuiltins();
            Runtime.PyDict_SetItemString(module_globals, "__builtins__", builtins);
            return module;
        }

        static void LoadSubmodule(BorrowedReference targetModuleDict, string fullName, string resourceName)
        {
            string? memberName = fullName.AfterLast('.');
            Debug.Assert(memberName != null);

            var module = DefineModule(fullName);
            var module_globals = Runtime.PyModule_GetDict(module);

            Assembly assembly = Assembly.GetExecutingAssembly();
            string pyCode = assembly.ReadStringResource(resourceName);
            Exec(pyCode, module_globals, module_globals);

            Runtime.PyDict_SetItemString(targetModuleDict, memberName!, module);
        }

        static void LoadMixins(BorrowedReference targetModuleDict)
        {
            foreach (string nested in new[] { "collections" })
            {
                LoadSubmodule(targetModuleDict,
                    fullName: "clr._extras." + nested,
                    resourceName: typeof(PythonEngine).Namespace + ".Mixins." + nested + ".py");
            }
        }

        static void OnDomainUnload(object _, EventArgs __)
        {
            Shutdown();
        }

        static void OnProcessExit(object _, EventArgs __)
        {
            Runtime.ProcessIsTerminating = true;
            Shutdown();
        }

        /// <summary>
        /// A helper to perform initialization from the context of an active
        /// CPython interpreter process - this bootstraps the managed runtime
        /// when it is imported by the CLR extension module.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete(Util.InternalUseOnly)]
        public static IntPtr InitExt()
        {
            try
            {
                if (Runtime.IsInitialized)
                {
                    var builtins = Runtime.PyEval_GetBuiltins();
                    var runtimeError = Runtime.PyDict_GetItemString(builtins, "RuntimeError");
                    Exceptions.SetError(runtimeError, "Python.NET runtime is already initialized");
                    return IntPtr.Zero;
                }
                Runtime.HostedInPython = true;

                Initialize(setSysArgv: false);

                Finalizer.Instance.ErrorHandler += AllowLeaksDuringShutdown;
            }
            catch (PythonException e)
            {
                e.Restore();
                return IntPtr.Zero;
            }

            return Python.Runtime.ImportHook.GetCLRModule()
                   .DangerousMoveToPointerOrNull();
        }

        private static void AllowLeaksDuringShutdown(object sender, Finalizer.ErrorArgs e)
        {
            if (e.Error is RuntimeShutdownException)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Shutdown and release resources held by the Python runtime. The
        /// Python runtime can no longer be used in the current process
        /// after calling the Shutdown method.
        /// </summary>
        public static void Shutdown()
        {
            if (!initialized)
            {
                return;
            }

            using (Py.GIL())
            {
                if (Exceptions.ErrorOccurred())
                {
                    throw new InvalidOperationException(
                        "Python error indicator is set",
                        innerException: PythonException.PeekCurrentOrNull(out _));
                }
            }

            // If the shutdown handlers trigger a domain unload,
            // don't call shutdown again.
            AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

            ExecuteShutdownHandlers();
            // Remember to shut down the runtime.
            Runtime.Shutdown();

            initialized = false;

            InteropConfiguration = InteropConfiguration.MakeDefault();
        }

        /// <summary>
        /// Called when the engine is shut down.
        ///
        /// Shutdown handlers are run in reverse order they were added, so that
        /// resources available when running a shutdown handler are the same as
        /// what was available when it was added.
        /// </summary>
        public delegate void ShutdownHandler();

        static readonly List<ShutdownHandler> ShutdownHandlers = new();

        /// <summary>
        /// Add a function to be called when the engine is shut down.
        ///
        /// Shutdown handlers are executed in the opposite order they were
        /// added, so that you can be sure that everything that was initialized
        /// when you added the handler is still initialized when you need to shut
        /// down.
        ///
        /// If the same shutdown handler is added several times, it will be run
        /// several times.
        ///
        /// Don't add shutdown handlers while running a shutdown handler.
        /// </summary>
        public static void AddShutdownHandler(ShutdownHandler handler)
        {
            ShutdownHandlers.Add(handler);
        }

        /// <summary>
        /// Remove a shutdown handler.
        ///
        /// If the same shutdown handler is added several times, only the last
        /// one is removed.
        ///
        /// Don't remove shutdown handlers while running a shutdown handler.
        /// </summary>
        public static void RemoveShutdownHandler(ShutdownHandler handler)
        {
            for (int index = ShutdownHandlers.Count - 1; index >= 0; --index)
            {
                if (ShutdownHandlers[index] == handler)
                {
                    ShutdownHandlers.RemoveAt(index);
                    break;
                }
            }
        }

        /// <summary>
        /// Run all the shutdown handlers.
        ///
        /// They're run in opposite order they were added.
        /// </summary>
        static void ExecuteShutdownHandlers()
        {
            while(ShutdownHandlers.Count > 0)
            {
                var handler = ShutdownHandlers[ShutdownHandlers.Count - 1];
                ShutdownHandlers.RemoveAt(ShutdownHandlers.Count - 1);
                handler();
            }
        }

        /// <summary>
        /// AcquireLock Method
        /// </summary>
        /// <remarks>
        /// Acquire the Python global interpreter lock (GIL). Managed code
        /// *must* call this method before using any objects or calling any
        /// methods on objects in the Python.Runtime namespace. The only
        /// exception is PythonEngine.Initialize, which may be called without
        /// first calling AcquireLock.
        /// Each call to AcquireLock must be matched by a corresponding call
        /// to ReleaseLock, passing the token obtained from AcquireLock.
        /// For more information, see the "Extending and Embedding" section
        /// of the Python documentation on www.python.org.
        /// </remarks>
        internal static PyGILState AcquireLock()
        {
            return Runtime.PyGILState_Ensure();
        }


        /// <summary>
        /// ReleaseLock Method
        /// </summary>
        /// <remarks>
        /// Release the Python global interpreter lock using a token obtained
        /// from a previous call to AcquireLock.
        /// For more information, see the "Extending and Embedding" section
        /// of the Python documentation on www.python.org.
        /// </remarks>
        internal static void ReleaseLock(PyGILState gs)
        {
            Runtime.PyGILState_Release(gs);
        }


        /// <summary>
        /// BeginAllowThreads Method
        /// </summary>
        /// <remarks>
        /// Release the Python global interpreter lock to allow other threads
        /// to run. This is equivalent to the Py_BEGIN_ALLOW_THREADS macro
        /// provided by the C Python API.
        /// For more information, see the "Extending and Embedding" section
        /// of the Python documentation on www.python.org.
        /// </remarks>
        public static unsafe IntPtr BeginAllowThreads()
        {
            return (IntPtr)Runtime.PyEval_SaveThread();
        }


        /// <summary>
        /// EndAllowThreads Method
        /// </summary>
        /// <remarks>
        /// Re-aquire the Python global interpreter lock for the current
        /// thread. This is equivalent to the Py_END_ALLOW_THREADS macro
        /// provided by the C Python API.
        /// For more information, see the "Extending and Embedding" section
        /// of the Python documentation on www.python.org.
        /// </remarks>
        public static unsafe void EndAllowThreads(IntPtr ts)
        {
            Runtime.PyEval_RestoreThread((PyThreadState*)ts);
        }

        public static PyObject Compile(string code, string filename = "", RunFlagType mode = RunFlagType.File)
        {
            var flag = (int)mode;
            NewReference ptr = Runtime.Py_CompileString(code, filename, flag);
            PythonException.ThrowIfIsNull(ptr);
            return ptr.MoveToPyObject();
        }


        /// <summary>
        /// Eval Method
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression and returns the result.
        /// It's a subset of Python eval function.
        /// </remarks>
        public static PyObject Eval(string code, PyDict? globals = null, PyObject? locals = null)
        {
            PyObject result = RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.Eval);
            return result;
        }


        /// <summary>
        /// Exec Method
        /// </summary>
        /// <remarks>
        /// Run a string containing Python code.
        /// It's a subset of Python exec function.
        /// </remarks>
        public static void Exec(string code, PyDict? globals = null, PyObject? locals = null)
        {
            using PyObject result = RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.File);
            if (result.obj != Runtime.PyNone)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }
        /// <summary>
        /// Exec Method
        /// </summary>
        /// <remarks>
        /// Run a string containing Python code.
        /// It's a subset of Python exec function.
        /// </remarks>
        internal static void Exec(string code, BorrowedReference globals, BorrowedReference locals = default)
        {
            using PyObject result = RunString(code, globals: globals, locals: locals, RunFlagType.File);
            if (result.obj != Runtime.PyNone)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <summary>
        /// Gets the Python thread ID.
        /// </summary>
        /// <returns>The Python thread ID.</returns>
        public static ulong GetPythonThreadID()
        {
            using PyObject threading = Py.Import("threading");
            using PyObject id = threading.InvokeMethod("get_ident");
            return id.As<ulong>();
        }

        /// <summary>
        /// Interrupts the execution of a thread.
        /// </summary>
        /// <param name="pythonThreadID">The Python thread ID.</param>
        /// <returns>The number of thread states modified; this is normally one, but will be zero if the thread id is not found.</returns>
        public static int Interrupt(ulong pythonThreadID)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Runtime.PyThreadState_SetAsyncExcLLP64((uint)pythonThreadID, Exceptions.KeyboardInterrupt);
            }

            return Runtime.PyThreadState_SetAsyncExcLP64(pythonThreadID, Exceptions.KeyboardInterrupt);
        }

        /// <summary>
        /// RunString Method. Function has been deprecated and will be removed.
        /// Use Exec/Eval/RunSimpleString instead.
        /// </summary>
        [Obsolete("RunString is deprecated and will be removed. Use Exec/Eval/RunSimpleString instead.")]
        public static PyObject RunString(string code, PyDict? globals = null, PyObject? locals = null)
        {
            return RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.File);
        }

        /// <summary>
        /// Internal RunString Method.
        /// </summary>
        /// <remarks>
        /// Run a string containing Python code. Returns the result of
        /// executing the code string as a PyObject instance, or null if
        /// an exception was raised.
        /// </remarks>
        internal static PyObject RunString(string code, BorrowedReference globals, BorrowedReference locals, RunFlagType flag)
        {
            if (code is null) throw new ArgumentNullException(nameof(code));

            NewReference tempGlobals = default;
            if (globals.IsNull)
            {
                globals = Runtime.PyEval_GetGlobals();
                if (globals.IsNull)
                {
                    tempGlobals = Runtime.PyDict_New();
                    globals = tempGlobals.BorrowOrThrow();
                    Runtime.PyDict_SetItem(
                        globals, PyIdentifier.__builtins__,
                        Runtime.PyEval_GetBuiltins()
                    );
                }
            }

            if (locals == null)
            {
                locals = globals;
            }

            try
            {
                NewReference result = Runtime.PyRun_String(
                    code, flag, globals, locals
                );
                PythonException.ThrowIfIsNull(result);
                return result.MoveToPyObject();
            }
            finally
            {
                tempGlobals.Dispose();
            }
        }
    }

    public enum RunFlagType : int
    {
        Single = 256,
        File = 257, /* Py_file_input */
        Eval = 258
    }
}
