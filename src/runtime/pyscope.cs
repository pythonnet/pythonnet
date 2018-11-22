using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;

namespace Python.Runtime
{
    public class PyScopeException : Exception
    {
        public PyScopeException(string message)
            : base(message)
        {

        }
    }

    /// <summary>
    /// Classes/methods have this attribute must be used with GIL obtained.
    /// </summary>
    public class PyGILAttribute : Attribute
    {
    }

    [PyGIL]
    public class PyScope : DynamicObject, IPyDisposable
    {
        public readonly string Name;

        /// <summary>
        /// the python Module object the scope associated with.
        /// </summary>
        internal readonly IntPtr obj;

        /// <summary>
        /// the variable dict of the scope.
        /// </summary>
        internal readonly IntPtr variables;

        private bool _isDisposed;
        private bool _finalized = false;

        /// <summary>
        /// The Manager this scope associated with.
        /// It provides scopes this scope can import.
        /// </summary>
        internal readonly PyScopeManager Manager;

        /// <summary>
        /// event which will be triggered after the scope disposed.
        /// </summary>
        public event Action<PyScope> OnDispose;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// Create a scope based on a Python Module.
        /// </remarks>
        internal PyScope(IntPtr ptr, PyScopeManager manager)
        {
            if (!Runtime.PyType_IsSubtype(Runtime.PyObject_TYPE(ptr), Runtime.PyModuleType))
            {
                throw new PyScopeException("object is not a module");
            }
            Manager = manager ?? PyScopeManager.Global;
            obj = ptr;
            //Refcount of the variables not increase
            variables = Runtime.PyModule_GetDict(obj);
            Runtime.CheckExceptionOccurred();

            Runtime.PyDict_SetItemString(
                variables, "__builtins__",
                Runtime.PyEval_GetBuiltins()
            );
            this.Name = this.Get<string>("__name__");
        }

        /// <summary>
        /// return the variable dict of the scope.
        /// </summary>
        /// <returns></returns>
        public PyDict Variables()
        {
            Runtime.XIncref(variables);
            return new PyDict(variables);
        }

        /// <summary>
        /// Create a scope, and import all from this scope
        /// </summary>
        /// <returns></returns>
        public PyScope NewScope()
        {
            var scope = Manager.Create();
            scope.ImportAll(this);
            return scope;
        }

        /// <summary>
        /// Import method
        /// </summary>
        /// <remarks>
        /// Import a scope or a module of given name,
        /// scope will be looked up first.
        /// </remarks>
        public dynamic Import(string name, string asname = null)
        {
            Check();
            if (String.IsNullOrEmpty(asname))
            {
                asname = name;
            }
            PyScope scope;
            Manager.TryGet(name, out scope);
            if (scope != null)
            {
                Import(scope, asname);
                return scope;
            }
            else
            {
                PyObject module = PythonEngine.ImportModule(name);
                Import(module, asname);
                return module;
            }
        }

        /// <summary>
        /// Import method
        /// </summary>
        /// <remarks>
        /// Import a scope as a variable of given name.
        /// </remarks>
        public void Import(PyScope scope, string asname)
        {
            this.Set(asname, scope.obj);
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The 'import .. as ..' statement in Python.
        /// Import a module as a variable into the scope.
        /// </remarks>
        public void Import(PyObject module, string asname = null)
        {
            if (String.IsNullOrEmpty(asname))
            {
                asname = module.GetAttr("__name__").As<string>();
            }
            Set(asname, module);
        }

        /// <summary>
        /// ImportAll Method
        /// </summary>
        /// <remarks>
        /// The 'import * from ..' statement in Python.
        /// Import all content of a scope/module of given name into the scope, scope will be looked up first.
        /// </remarks>
        public void ImportAll(string name)
        {
            PyScope scope;
            Manager.TryGet(name, out scope);
            if (scope != null)
            {
                ImportAll(scope);
                return;
            }
            else
            {
                PyObject module = PythonEngine.ImportModule(name);
                ImportAll(module);
            }
        }

        /// <summary>
        /// ImportAll Method
        /// </summary>
        /// <remarks>
        /// Import all variables of the scope into this scope.
        /// </remarks>
        public void ImportAll(PyScope scope)
        {
            int result = Runtime.PyDict_Update(variables, scope.variables);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// ImportAll Method
        /// </summary>
        /// <remarks>
        /// Import all variables of the module into this scope.
        /// </remarks>
        public void ImportAll(PyObject module)
        {
            if (Runtime.PyObject_Type(module.obj) != Runtime.PyModuleType)
            {
                throw new PyScopeException("object is not a module");
            }
            var module_dict = Runtime.PyModule_GetDict(module.obj);
            int result = Runtime.PyDict_Update(variables, module_dict);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// ImportAll Method
        /// </summary>
        /// <remarks>
        /// Import all variables in the dictionary into this scope.
        /// </remarks>
        public void ImportAll(PyDict dict)
        {
            int result = Runtime.PyDict_Update(variables, dict.obj);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// Execute method
        /// </summary>
        /// <remarks>
        /// Execute a Python ast and return the result as a PyObject.
        /// The ast can be either an expression or stmts.
        /// </remarks>
        public PyObject Execute(PyObject script, PyDict locals = null)
        {
            Check();
            IntPtr _locals = locals == null ? variables : locals.obj;
            IntPtr ptr = Runtime.PyEval_EvalCode(script.Handle, variables, _locals);
            Runtime.CheckExceptionOccurred();
            if (ptr == Runtime.PyNone)
            {
                Runtime.XDecref(ptr);
                return null;
            }
            return new PyObject(ptr);
        }

        /// <summary>
        /// Execute method
        /// </summary>
        /// <remarks>
        /// Execute a Python ast and return the result as a PyObject,
        /// and convert the result to a Managed Object of given type.
        /// The ast can be either an expression or stmts.
        /// </remarks>
        public T Execute<T>(PyObject script, PyDict locals = null)
        {
            Check();
            PyObject pyObj = Execute(script, locals);
            if (pyObj == null)
            {
                return default(T);
            }
            var obj = pyObj.As<T>();
            return obj;
        }

        /// <summary>
        /// Eval method
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression and return the result as a PyObject
        /// or null if an exception is raised.
        /// </remarks>
        public PyObject Eval(string code, PyDict locals = null)
        {
            Check();
            IntPtr _locals = locals == null ? variables : locals.obj;
            var flag = (IntPtr)Runtime.Py_eval_input;
            IntPtr ptr = Runtime.PyRun_String(
                code, flag, variables, _locals
            );
            Runtime.CheckExceptionOccurred();
            return new PyObject(ptr);
        }

        /// <summary>
        /// Evaluate a Python expression
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression
        /// and  convert the result to a Managed Object of given type.
        /// </remarks>
        public T Eval<T>(string code, PyDict locals = null)
        {
            Check();
            PyObject pyObj = Eval(code, locals);
            var obj = pyObj.As<T>();
            return obj;
        }

        /// <summary>
        /// Exec Method
        /// </summary>
        /// <remarks>
        /// Exec a Python script and save its local variables in the current local variable dict.
        /// </remarks>
        public void Exec(string code, PyDict locals = null)
        {
            Check();
            IntPtr _locals = locals == null ? variables : locals.obj;
            Exec(code, variables, _locals);
        }

        private void Exec(string code, IntPtr _globals, IntPtr _locals)
        {
            var flag = (IntPtr)Runtime.Py_file_input;
            IntPtr ptr = Runtime.PyRun_String(
                code, flag, _globals, _locals
            );
            Runtime.CheckExceptionOccurred();
            if (ptr != Runtime.PyNone)
            {
                throw new PythonException();
            }
            Runtime.XDecref(ptr);
        }

        /// <summary>
        /// Set Variable Method
        /// </summary>
        /// <remarks>
        /// Add a new variable to the variables dict if it not exist
        /// or update its value if the variable exists.
        /// </remarks>
        public void Set(string name, object value)
        {
            IntPtr _value = Converter.ToPython(value, value?.GetType());
            Set(name, _value);
            Runtime.XDecref(_value);
        }

        private void Set(string name, IntPtr value)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_SetItem(variables, pyKey.obj, value);
                if (r < 0)
                {
                    throw new PythonException();
                }
            }
        }

        /// <summary>
        /// Remove Method
        /// </summary>
        /// <remarks>
        /// Remove a variable from the variables dict.
        /// </remarks>
        public void Remove(string name)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_DelItem(variables, pyKey.obj);
                if (r < 0)
                {
                    throw new PythonException();
                }
            }
        }

        /// <summary>
        /// Contains Method
        /// </summary>
        /// <remarks>
        /// Returns true if the variable exists in the scope.
        /// </remarks>
        public bool Contains(string name)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                return Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0;
            }
        }

        /// <summary>
        /// Get Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable of given name.
        /// If the variable does not exist, throw an Exception.
        /// </remarks>
        public PyObject Get(string name)
        {
            PyObject scope;
            var state = TryGet(name, out scope);
            if (!state)
            {
                throw new PyScopeException($"The scope of name '{Name}' has no attribute '{name}'");
            }
            return scope;
        }

        /// <summary>
        /// TryGet Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable does not exist, return null.
        /// </remarks>
        public bool TryGet(string name, out PyObject value)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                if (Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0)
                {
                    IntPtr op = Runtime.PyObject_GetItem(variables, pyKey.obj);
                    if (op == IntPtr.Zero)
                    {
                        throw new PythonException();
                    }
                    if (op == Runtime.PyNone)
                    {
                        Runtime.XDecref(op);
                        value = null;
                        return true;
                    }
                    value = new PyObject(op);
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Get Method
        /// </summary>
        /// <remarks>
        /// Obtain the value of the variable of given name,
        /// and convert the result to a Managed Object of given type.
        /// If the variable does not exist, throw an Exception.
        /// </remarks>
        public T Get<T>(string name)
        {
            Check();
            PyObject pyObj = Get(name);
            if (pyObj == null)
            {
                return default(T);
            }
            return pyObj.As<T>();
        }

        /// <summary>
        /// TryGet Method
        /// </summary>
        /// <remarks>
        /// Obtain the value of the variable of given name,
        /// and convert the result to a Managed Object of given type.
        /// If the variable does not exist, return false.
        /// </remarks>
        public bool TryGet<T>(string name, out T value)
        {
            Check();
            PyObject pyObj;
            var result = TryGet(name, out pyObj);
            if (!result)
            {
                value = default(T);
                return false;
            }
            if (pyObj == null)
            {
                if (typeof(T).IsValueType)
                {
                    throw new PyScopeException($"The value of the attribute '{name}' is None which cannot be convert to '{typeof(T).ToString()}'");
                }
                else
                {
                    value = default(T);
                    return true;
                }
            }
            value = pyObj.As<T>();
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = this.Get(binder.Name);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this.Set(binder.Name, value);
            return true;
        }

        private void Check()
        {
            if (_isDisposed)
            {
                throw new PyScopeException($"The scope of name '{Name}' object has been disposed");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            Runtime.XDecref(obj);
            this.OnDispose?.Invoke(this);
        }

        public IntPtr[] GetTrackedHandles()
        {
            return new IntPtr[] { obj };
        }

        ~PyScope()
        {
            if (_finalized || _isDisposed)
            {
                return;
            }
            _finalized = true;
            Finalizer.Instance.AddFinalizedObject(this);
        }
    }

    public class PyScopeManager
    {
        public static PyScopeManager Global;

        private Dictionary<string, PyScope> NamedScopes = new Dictionary<string, PyScope>();

        internal static void Reset()
        {
            Global = new PyScopeManager();
        }

        internal PyScope NewScope(string name)
        {
            if (name == null)
            {
                name = "";
            }
            var module = Runtime.PyModule_New(name);
            if (module == IntPtr.Zero)
            {
                throw new PythonException();
            }
            return new PyScope(module, this);
        }

        /// <summary>
        /// Create Method
        /// </summary>
        /// <remarks>
        /// Create an anonymous scope.
        /// </remarks>
        [PyGIL]
        public PyScope Create()
        {
            var scope = this.NewScope(null);
            return scope;
        }

        /// <summary>
        /// Create Method
        /// </summary>
        /// <remarks>
        /// Create an named scope of given name.
        /// </remarks>
        [PyGIL]
        public PyScope Create(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name != null && Contains(name))
            {
                throw new PyScopeException($"A scope of name '{name}' does already exist");
            }
            var scope = this.NewScope(name);
            scope.OnDispose += Remove;
            NamedScopes[name] = scope;
            return scope;
        }

        /// <summary>
        /// Contains Method
        /// </summary>
        /// <remarks>
        /// return true if the scope exists in this manager.
        /// </remarks>
        public bool Contains(string name)
        {
            return NamedScopes.ContainsKey(name);
        }

        /// <summary>
        /// Get Method
        /// </summary>
        /// <remarks>
        /// Find the scope in this manager.
        /// If the scope not exist, an Exception will be thrown.
        /// </remarks>
        public PyScope Get(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (NamedScopes.ContainsKey(name))
            {
                return NamedScopes[name];
            }
            throw new PyScopeException($"There is no scope named '{name}' registered in this manager");
        }

        /// <summary>
        /// Get Method
        /// </summary>
        /// <remarks>
        /// Try to find the scope in this manager.
        /// </remarks>
        public bool TryGet(string name, out PyScope scope)
        {
            return NamedScopes.TryGetValue(name, out scope);
        }

        /// <summary>
        /// Remove Method
        /// </summary>
        /// <remarks>
        /// remove the scope from this manager.
        /// </remarks>
        public void Remove(PyScope scope)
        {
            NamedScopes.Remove(scope.Name);
        }

        [PyGIL]
        public void Clear()
        {
            var scopes = NamedScopes.Values.ToList();
            foreach (var scope in scopes)
            {
                scope.Dispose();
            }
        }
    }
}
