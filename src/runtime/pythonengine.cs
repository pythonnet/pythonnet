using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime
{
    /// <summary>
    /// This class provides the public interface of the Python runtime.
    /// </summary>
    public class PythonEngine : IDisposable
    {
        private static DelegateManager delegateManager;
        private static bool initialized;
        private static IntPtr _pythonHome = IntPtr.Zero;
        private static IntPtr _programName = IntPtr.Zero;
        private static IntPtr _pythonPath = IntPtr.Zero;

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

        public static string ProgramName
        {
            get
            {
                IntPtr p = Runtime.Py_GetProgramName();
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                Marshal.FreeHGlobal(_programName);
                _programName = UcsMarshaler.Py3UnicodePy2StringtoPtr(value);
                Runtime.Py_SetProgramName(_programName);
            }
        }

        public static string PythonHome
        {
            get
            {
                IntPtr p = Runtime.Py_GetPythonHome();
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                Marshal.FreeHGlobal(_pythonHome);
                _pythonHome = UcsMarshaler.Py3UnicodePy2StringtoPtr(value);
                Runtime.Py_SetPythonHome(_pythonHome);
            }
        }

        public static string PythonPath
        {
            get
            {
                IntPtr p = Runtime.Py_GetPath();
                return UcsMarshaler.PtrToPy3UnicodePy2String(p) ?? "";
            }
            set
            {
                if (Runtime.IsPython2)
                {
                    throw new NotSupportedException("Set PythonPath not supported on Python 2");
                }
                Marshal.FreeHGlobal(_pythonPath);
                _pythonPath = UcsMarshaler.Py3UnicodePy2StringtoPtr(value);
                Runtime.Py_SetPath(_pythonPath);
            }
        }

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

        public static int RunSimpleString(string code)
        {
            return Runtime.PyRun_SimpleString(code);
        }

        public static void Initialize()
        {
            Initialize(setSysArgv: true);
        }

        public static void Initialize(bool setSysArgv = true)
        {
            Initialize(Enumerable.Empty<string>(), setSysArgv: setSysArgv);
        }

        /// <summary>
        /// Initialize Method
        /// </summary>
        /// <remarks>
        /// Initialize the Python runtime. It is safe to call this method
        /// more than once, though initialization will only happen on the
        /// first call. It is *not* necessary to hold the Python global
        /// interpreter lock (GIL) to call this method.
        /// </remarks>
        public static void Initialize(IEnumerable<string> args, bool setSysArgv = true)
        {
            if (!initialized)
            {
                // Creating the delegateManager MUST happen before Runtime.Initialize
                // is called. If it happens afterwards, DelegateManager's CodeGenerator
                // throws an exception in its ctor.  This exception is eaten somehow
                // during an initial "import clr", and the world ends shortly thereafter.
                // This is probably masking some bad mojo happening somewhere in Runtime.Initialize().
                delegateManager = new DelegateManager();
                Runtime.Initialize();
                initialized = true;
                Exceptions.Clear();

                if (setSysArgv)
                {
                    Py.SetArgv(args);
                }

                // register the atexit callback (this doesn't use Py_AtExit as the C atexit
                // callbacks are called after python is fully finalized but the python ones
                // are called while the python engine is still running).
                string code =
                    "import atexit, clr\n" +
                    "atexit.register(clr._AtExit)\n";
                PythonEngine.Exec(code);

                // Load the clr.py resource into the clr module
                IntPtr clr = Python.Runtime.ImportHook.GetCLRModule();
                IntPtr clr_dict = Runtime.PyModule_GetDict(clr);

                var locals = new PyDict();
                try
                {
                    IntPtr module = Runtime.PyImport_AddModule("clr._extras");
                    IntPtr module_globals = Runtime.PyModule_GetDict(module);
                    IntPtr builtins = Runtime.PyEval_GetBuiltins();
                    Runtime.PyDict_SetItemString(module_globals, "__builtins__", builtins);

                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("clr.py"))
                    using (var reader = new StreamReader(stream))
                    {
                        // add the contents of clr.py to the module
                        string clr_py = reader.ReadToEnd();
                        Exec(clr_py, module_globals, locals.Handle);
                    }

                    // add the imported module to the clr module, and copy the API functions
                    // and decorators into the main clr module.
                    Runtime.PyDict_SetItemString(clr_dict, "_extras", module);
                    foreach (PyObject key in locals.Keys())
                    {
                        if (!key.ToString().StartsWith("_") || key.ToString().Equals("__version__"))
                        {
                            PyObject value = locals[key];
                            Runtime.PyDict_SetItem(clr_dict, key.Handle, value.Handle);
                            value.Dispose();
                        }
                        key.Dispose();
                    }
                }
                finally
                {
                    locals.Dispose();
                }
            }
        }

        /// <summary>
        /// A helper to perform initialization from the context of an active
        /// CPython interpreter process - this bootstraps the managed runtime
        /// when it is imported by the CLR extension module.
        /// </summary>
#if PYTHON3
        public static IntPtr InitExt()
#elif PYTHON2
        public static void InitExt()
#endif
        {
            try
            {
                Initialize(setSysArgv: false);

                // Trickery - when the import hook is installed into an already
                // running Python, the standard import machinery is still in
                // control for the duration of the import that caused bootstrap.
                //
                // That is problematic because the std machinery tries to get
                // sub-names directly from the module __dict__ rather than going
                // through our module object's getattr hook. This workaround is
                // evil ;) We essentially climb up the stack looking for the
                // import that caused the bootstrap to happen, then re-execute
                // the import explicitly after our hook has been installed. By
                // doing this, the original outer import should work correctly.
                //
                // Note that this is only needed during the execution of the
                // first import that installs the CLR import hook. This hack
                // still doesn't work if you use the interactive interpreter,
                // since there is no line info to get the import line ;(

                string code =
                    "import traceback\n" +
                    "for item in traceback.extract_stack():\n" +
                    "    line = item[3]\n" +
                    "    if line is not None:\n" +
                    "        if line.startswith('import CLR') or \\\n" +
                    "           line.startswith('import clr') or \\\n" +
                    "           line.startswith('from clr') or \\\n" +
                    "           line.startswith('from CLR'):\n" +
                    "            exec(line)\n" +
                    "            break\n";

                PythonEngine.Exec(code);
            }
            catch (PythonException e)
            {
                e.Restore();
#if PYTHON3
                return IntPtr.Zero;
#endif
            }

#if PYTHON3
            return Python.Runtime.ImportHook.GetCLRModule();
#endif
        }

        /// <summary>
        /// Shutdown Method
        /// </summary>
        /// <remarks>
        /// Shutdown and release resources held by the Python runtime. The
        /// Python runtime can no longer be used in the current process
        /// after calling the Shutdown method.
        /// </remarks>
        public static void Shutdown()
        {
            if (initialized)
            {
                Marshal.FreeHGlobal(_pythonHome);
                _pythonHome = IntPtr.Zero;
                Marshal.FreeHGlobal(_programName);
                _programName = IntPtr.Zero;
                Marshal.FreeHGlobal(_pythonPath);
                _pythonPath = IntPtr.Zero;

                Runtime.Shutdown();
                initialized = false;
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
        public static IntPtr AcquireLock()
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
        public static void ReleaseLock(IntPtr gs)
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
        public static IntPtr BeginAllowThreads()
        {
            return Runtime.PyEval_SaveThread();
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
        public static void EndAllowThreads(IntPtr ts)
        {
            Runtime.PyEval_RestoreThread(ts);
        }


        /// <summary>
        /// ImportModule Method
        /// </summary>
        /// <remarks>
        /// Given a fully-qualified module or package name, import the
        /// module and return the resulting module object as a PyObject
        /// or null if an exception is raised.
        /// </remarks>
        public static PyObject ImportModule(string name)
        {
            IntPtr op = Runtime.PyImport_ImportModule(name);
            Runtime.CheckExceptionOccurred();
            return new PyObject(op);
        }


        /// <summary>
        /// ReloadModule Method
        /// </summary>
        /// <remarks>
        /// Given a PyObject representing a previously loaded module, reload
        /// the module.
        /// </remarks>
        public static PyObject ReloadModule(PyObject module)
        {
            IntPtr op = Runtime.PyImport_ReloadModule(module.Handle);
            Runtime.CheckExceptionOccurred();
            return new PyObject(op);
        }


        /// <summary>
        /// ModuleFromString Method
        /// </summary>
        /// <remarks>
        /// Given a string module name and a string containing Python code,
        /// execute the code in and return a module of the given name.
        /// </remarks>
        public static PyObject ModuleFromString(string name, string code)
        {
            IntPtr c = Runtime.Py_CompileString(code, "none", (IntPtr)257);
            Runtime.CheckExceptionOccurred();
            IntPtr m = Runtime.PyImport_ExecCodeModule(name, c);
            Runtime.CheckExceptionOccurred();
            return new PyObject(m);
        }


        /// <summary>
        /// Eval Method
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression and returns the result.
        /// It's a subset of Python eval function.
        /// </remarks>
        public static PyObject Eval(string code, IntPtr? globals = null, IntPtr? locals = null)
        {
            PyObject result = RunString(code, globals, locals, RunFlagType.Eval);
            return result;
        }


        /// <summary>
        /// Exec Method
        /// </summary>
        /// <remarks>
        /// Run a string containing Python code.
        /// It's a subset of Python exec function.
        /// </remarks>
        public static void Exec(string code, IntPtr? globals = null, IntPtr? locals = null)
        {
            PyObject result = RunString(code, globals, locals, RunFlagType.File);
            if (result.obj != Runtime.PyNone)
            {
                throw new PythonException();
            }
            result.Dispose();
        }


        /// <summary>
        /// RunString Method. Function has been deprecated and will be removed.
        /// Use Exec/Eval/RunSimpleString instead.
        /// </summary>
        [Obsolete("RunString is deprecated and will be removed. Use Exec/Eval/RunSimpleString instead.")]
        public static PyObject RunString(string code, IntPtr? globals = null, IntPtr? locals = null)
        {
            return RunString(code, globals, locals, RunFlagType.File);
        }

        /// <summary>
        /// Internal RunString Method.
        /// </summary>
        /// <remarks>
        /// Run a string containing Python code. Returns the result of
        /// executing the code string as a PyObject instance, or null if
        /// an exception was raised.
        /// </remarks>
        internal static PyObject RunString(string code, IntPtr? globals, IntPtr? locals, RunFlagType flag)
        {
            var borrowedGlobals = true;
            if (globals == null)
            {
                globals = Runtime.PyEval_GetGlobals();
                if (globals == IntPtr.Zero)
                {
                    globals = Runtime.PyDict_New();
                    Runtime.PyDict_SetItemString(
                        globals.Value, "__builtins__",
                        Runtime.PyEval_GetBuiltins()
                    );
                    borrowedGlobals = false;
                }
            }

            var borrowedLocals = true;
            if (locals == null)
            {
                locals = Runtime.PyDict_New();
                borrowedLocals = false;
            }

            try
            {
                IntPtr result = Runtime.PyRun_String(
                    code, (IntPtr)flag, globals.Value, locals.Value
                );

                Runtime.CheckExceptionOccurred();

                return new PyObject(result);
            }
            finally
            {
                if (!borrowedLocals)
                {
                    Runtime.XDecref(locals.Value);
                }
                if (!borrowedGlobals)
                {
                    Runtime.XDecref(globals.Value);
                }
            }
        }
    }

    public enum RunFlagType
    {
        Single = 256,
        File = 257, /* Py_file_input */
        Eval = 258
    }

    public class PySessionDisposedException: Exception
    {

    }

    public enum CompileMode
    {
        Single = 256,
        File = 257,
        Eval = 258
    }

    public class PyScope : IDisposable
    {
        public class GILState : IDisposable
        {
            private bool isGetGIL;
            private IntPtr state;

            internal GILState()
            {
            }

            public void AcquireLock()
            {
                if (isGetGIL)
                {
                    return;
                }
                state = PythonEngine.AcquireLock();
                isGetGIL = true;
            }

            public void ReleaseLock()
            {
                if (isGetGIL)
                {
                    PythonEngine.ReleaseLock(state);
                    isGetGIL = false;
                }
            }

            public void Dispose()
            {
                this.ReleaseLock();
                GC.SuppressFinalize(this);
            }

            ~GILState()
            {
                Dispose();
            }
        }

        private string name;

        private PyScope parent;

        private GILState state;

        private bool isDisposed;

        private PyScope(GILState state)
        {
            this.isDisposed = false;
            this.state = state;
            globals = Runtime.PyDict_New();
            if (globals == IntPtr.Zero)
            {
                throw new PythonException();
            }
            locals = Runtime.PyDict_New();
            if (locals == IntPtr.Zero)
            {
                throw new PythonException();
            }
        }

        internal PyScope(string name, GILState state)
            :this(state)
        {
            this.state = state;
            this.name = name;
            Runtime.PyDict_SetItemString(
                    globals, "__builtins__",
                    Runtime.PyEval_GetBuiltins()
                );
        }

        internal PyScope(PyScope parent, GILState state)
            : this(state)
        {
            this.state = state;
            this.parent = parent;
            globals = Runtime.PyDict_New();
            if (globals == IntPtr.Zero)
            {
                throw new PythonException();
            }
            locals = Runtime.PyDict_New();
            if (locals == IntPtr.Zero)
            {
                throw new PythonException();
            }
            int result = Runtime.PyDict_Update(globals, parent.globals);
            if (result < 0)
            {
                throw new PythonException();
            }
            result = Runtime.PyDict_Update(globals, parent.locals);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// the dict for global variables
        /// </summary>
        public IntPtr globals
        {
            get;
            private set;
        }

        /// <summary>
        /// the dict for local variables
        /// </summary>
        public IntPtr locals
        {
            get;
            private set;
        }

        public PyScope SubScope()
        {
            return new PyScope(this, this.state);
        }

        public void Suspend()
        {
            this.state.ReleaseLock();
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the local variable dict and return the resulting module object as a PyObject.
        /// </remarks>
        public PyObject Import(string name)
        {
            return ImportAs(name, name);
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the local variable dict and return the resulting module object as a PyObject.
        /// </remarks>
        public PyObject ImportAs(string name, string asname)
        {
            this.AcquireLock();
            PyObject module = PythonEngine.ImportModule(name);
            if (asname == null)
            {
                asname = name;
            }
            SetVariable(asname, module);
            return module;
        }

        public PyObject Execute(PyObject script)
        {
            IntPtr ptr = Runtime.PyEval_EvalCode(script.Handle, globals, locals);
            Py.Throw();
            if(ptr == Runtime.PyNone)
            {
                Runtime.XDecref(ptr);
                return null;
            }
            return new PyObject(ptr);
        }

        public T Execute<T>(PyObject script)
        {
            var pyObj = this.Execute(script);
            if(pyObj == null)
            {
                return default(T);
            }
            T obj = (T)pyObj.AsManagedObject(typeof(T));
            return obj;
        }

        /// <summary>
        /// Compile Method
        /// </summary>
        /// <remarks>
        /// Compile Python expression/statements into ast.
        /// </remarks>
        public PyObject Compile(string code, string filename = "", CompileMode mode = CompileMode.File)
        {
            IntPtr flag = (IntPtr)mode;
            IntPtr ptr = Runtime.Py_CompileString(code, filename, flag);
            Py.Throw();
            return new PyObject(ptr);
        }

        /// <summary>
        /// Evaluate a Python expression
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression and return the result as a PyObject
        /// or null if an exception is raised.
        /// </remarks>
        public PyObject Eval(string code)
        {
            this.AcquireLock();
            var flag = (IntPtr)Runtime.Py_eval_input;
            IntPtr ptr = Runtime.PyRun_String(
                code, flag, globals, locals
            );
            Py.Throw();
            return new PyObject(ptr);
        }

        /// <summary>
        /// Evaluate a Python expression
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression and convert the result to Managed Object.
        /// </remarks>
        public T Eval<T>(string code)
        {
            PyObject pyObj = Eval(code);
            T obj = (T)pyObj.AsManagedObject(typeof(T));
            return obj;
        }

        /// <summary>
        /// Exec Method
        /// </summary>
        /// <remarks>
        /// Evaluate a Python script and save its local variables in the current local variable dict.
        /// </remarks>
        public void Exec(string code)
        {
            this.AcquireLock();
            Exec(code, this.globals, this.locals);
        }
        
        private void Exec(string code, IntPtr _globals, IntPtr _locals)
        {
            var flag = (IntPtr)Runtime.Py_file_input;
            IntPtr ptr = Runtime.PyRun_String(
                code, flag, _globals, _locals
            );
            Py.Throw();
            if (ptr != Runtime.PyNone)
            {
                throw new PythonException();
            }
            Runtime.XDecref(ptr);
        }

        /// <summary>
        /// SetGlobalVariable Method
        /// </summary>
        /// <remarks>
        /// Add a new variable to global variable dict if it not exists
        /// or set the value of the global variable if it exists.
        /// </remarks>
        internal void SetGlobalVariable(string name, object value)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                IntPtr _value = GetInstHandle(value);
                int r = Runtime.PyObject_SetItem(globals, pyKey.obj, _value);
                if (r < 0)
                {
                    throw new PythonException();
                }
                Runtime.XDecref(_value);
            }
        }

        /// <summary>
        /// RemoveGlobalVariable Method
        /// </summary>
        /// <remarks>
        /// Remove a variable from the global variable dict.
        /// </remarks>
        internal void RemoveGlobalVariable(string name)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_DelItem(globals, pyKey.obj);
                if (r < 0)
                {
                    throw new PythonException();
                }
            }
        }

        /// <summary>
        /// SetLocal Method
        /// </summary>
        /// <remarks>
        /// Add a new variable to local variable dict if it not exists
        /// or set the value of the local variable if it exists.
        /// </remarks>
        public void SetVariable(string name, object value)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                IntPtr _value = GetInstHandle(value);
                int r = Runtime.PyObject_SetItem(locals, pyKey.obj, _value);
                if (r < 0)
                {
                    throw new PythonException();
                }
                Runtime.XDecref(_value);
            }
        }

        /// <summary>
        /// DelLocal Method
        /// </summary>
        /// <remarks>
        /// Remove a variable from the local variable dict.
        /// </remarks>
        public void RemoveVariable(string name)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_DelItem(locals, pyKey.obj);
                if (r < 0)
                {
                    throw new PythonException();
                }
            }
        }

        /// <summary>
        /// Exists Method
        /// </summary>
        /// <remarks>
        /// Returns true if the variable appears in the local variable dict or the global variable dict.
        /// </remarks>
        public bool ContainsVariable(string name)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                if (Runtime.PyMapping_HasKey(locals, pyKey.obj) != 0)
                {
                    return true;
                }
                return Runtime.PyMapping_HasKey(globals, pyKey.obj) != 0;
            }
        }

        /// <summary>
        /// Get Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable is not exists, return null.
        /// </remarks>
        public PyObject GetVariable(string name)
        {
            this.AcquireLock();
            using (var pyKey = new PyString(name))
            {
                IntPtr op;
                if (Runtime.PyMapping_HasKey(locals, pyKey.obj) != 0)
                {
                    op = Runtime.PyObject_GetItem(locals, pyKey.obj);
                }
                else if (Runtime.PyMapping_HasKey(globals, pyKey.obj) != 0)
                {
                    op = Runtime.PyObject_GetItem(globals, pyKey.obj);
                }
                else
                {
                    return null; //name not exists
                }
                if (op == IntPtr.Zero)
                {
                    throw new PythonException();
                }
                return new PyObject(op);
            }
        }

        public T GetVariable<T>(string name)
        {
            PyObject obj = this.GetVariable(name);
            return (T)obj.AsManagedObject(typeof(T));
        }

        private static IntPtr GetInstHandle(object value)
        {
            if (value == null)
            {
                Runtime.XIncref(Runtime.PyNone);
                return Runtime.PyNone;
            }
            else
            {
                var ptr = Converter.ToPython(value, value.GetType());
                return ptr;
            }
        }

        private void AcquireLock()
        {
            if(isDisposed)
            {
                throw new PySessionDisposedException();
            }
            this.state.AcquireLock();
        }

        public virtual void Dispose()
        {
            if(isDisposed)
            {
                return;
            }
            Runtime.XDecref(globals);
            Runtime.XDecref(locals);
            if (this.parent == null)
            {
                Py.RemoveSession(name);
            }
            isDisposed = true;
        }

        ~PyScope()
        {
            Dispose();
        }
    }

    public static class Py
    {
        public static GILState GIL()
        {
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }

            return new GILState();
        }

        private static PyScope.GILState gil = new PyScope.GILState();

        /// <summary>
        /// Sessions should be cleared after shut down.
        /// Currently, the seperation of static methods into Py and PythonEngine makes the code ugly.
        /// </summary>
        private static Dictionary<string, PyScope> Sessions = new Dictionary<string, PyScope>();

        public static PyScope Session(string name)
        {
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }
            if(Sessions.ContainsKey(name))
            {
                return Sessions[name];
            }
            var session = new PyScope(name, gil);
            Sessions[name] = session;
            return session;
        }

        internal static void RemoveSession(string name)
        {
            Sessions.Remove(name);
        }

        public class GILState : IDisposable
        {
            private IntPtr state;

            internal GILState()
            {
                state = PythonEngine.AcquireLock();
            }

            public void Dispose()
            {
                PythonEngine.ReleaseLock(state);
                GC.SuppressFinalize(this);
            }

            ~GILState()
            {
                Dispose();
            }
        }

        public class KeywordArguments : PyDict
        {
        }

        public static KeywordArguments kw(params object[] kv)
        {
            var dict = new KeywordArguments();
            if (kv.Length % 2 != 0)
            {
                throw new ArgumentException("Must have an equal number of keys and values");
            }
            for (var i = 0; i < kv.Length; i += 2)
            {
                IntPtr value;
                if (kv[i + 1] is PyObject)
                {
                    value = ((PyObject)kv[i + 1]).Handle;
                }
                else
                {
                    value = Converter.ToPython(kv[i + 1], kv[i + 1]?.GetType());
                }
                if (Runtime.PyDict_SetItemString(dict.Handle, (string)kv[i], value) != 0)
                {
                    throw new ArgumentException(string.Format("Cannot add key '{0}' to dictionary.", (string)kv[i]));
                }
                if (!(kv[i + 1] is PyObject))
                {
                    Runtime.XDecref(value);
                }
            }
            return dict;
        }

        public static PyObject Import(string name)
        {
            return PythonEngine.ImportModule(name);
        }

        public static void SetArgv()
        {
            IEnumerable<string> args;
            try
            {
                args = Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                args = Enumerable.Empty<string>();
            }

            SetArgv(
                new[] { "" }.Concat(
                    Environment.GetCommandLineArgs().Skip(1)
                )
            );
        }

        public static void SetArgv(params string[] argv)
        {
            SetArgv(argv as IEnumerable<string>);
        }

        public static void SetArgv(IEnumerable<string> argv)
        {
            using (GIL())
            {
                string[] arr = argv.ToArray();
                Runtime.PySys_SetArgvEx(arr.Length, arr, 0);
                Runtime.CheckExceptionOccurred();
            }
        }
    }
}
