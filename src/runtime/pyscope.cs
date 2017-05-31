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
    public class PyScope : DynamicObject, IDisposable
    {
        public readonly string Name;

        internal readonly IntPtr obj;

        /// <summary>
        /// the dict for local variables
        /// </summary>
        internal readonly IntPtr variables;

        private bool isDisposed;

        internal readonly PyScopeManager Manager;

        public event Action<PyScope> OnDispose;

        internal PyScope(IntPtr ptr, PyScopeManager manager)
        {
            if (Runtime.PyObject_Type(ptr) != Runtime.PyModuleType)
            {
                throw new PyScopeException("object is not a module");
            }
            if (manager == null)
            {
                manager = PyScopeManager.Global;
            }
            Manager = manager;
            obj = ptr;
            //Refcount of the variables not increase
            variables = Runtime.PyModule_GetDict(obj);
            if (variables == IntPtr.Zero)
            {
                throw new PythonException();
            }
            Runtime.PyDict_SetItemString(
                variables, "__builtins__",
                Runtime.PyEval_GetBuiltins()
            );
            this.Name = this.GetVariable<string>("__name__");
        }

        public PyDict Variables()
        {
            Runtime.XIncref(variables);
            return new PyDict(variables);
        }

        public PyScope NewScope()
        {
            var scope = Manager.Create();
            scope.ImportAll(this);
            return scope;
        }

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

        public void Import(PyScope scope, string asname)
        {
            this.SetVariable(asname, scope.obj);
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module,add it to the variables dict and return the resulting module object as a PyObject.
        /// </remarks>
        public void Import(PyObject module, string asname = null)
        {
            if (String.IsNullOrEmpty(asname))
            {
                asname = module.GetAttr("__name__").ToString();
            }
            SetVariable(asname, module);
        }

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

        public void ImportAll(PyScope scope)
        {
            int result = Runtime.PyDict_Update(variables, scope.variables);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

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
        /// Evaluate a Python expression and convert the result to Managed Object.
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
        /// SetVariable Method
        /// </summary>
        /// <remarks>
        /// Add a new variable to the variables dict if it not exists
        /// or update its value if the variable exists.
        /// </remarks>
        public void SetVariable(string name, object value)
        {
            IntPtr _value = Converter.ToPython(value, value?.GetType());
            SetVariable(name, _value);
            Runtime.XDecref(_value);
        }

        private void SetVariable(string name, IntPtr value)
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
        /// RemoveVariable Method
        /// </summary>
        /// <remarks>
        /// Remove a variable from the variables dict.
        /// </remarks>
        public void RemoveVariable(string name)
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
        /// ContainsVariable Method
        /// </summary>
        /// <remarks>
        /// Returns true if the variable exists in the variables dict.
        /// </remarks>
        public bool ContainsVariable(string name)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                return Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0;
            }
        }

        /// <summary>
        /// GetVariable Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable is not exists, throw an Exception.
        /// </remarks>
        public PyObject GetVariable(string name)
        {
            PyObject scope;
            var state = TryGetVariable(name, out scope);
            if(!state)
            {
                throw new PyScopeException($"The scope of name '{Name}' has no attribute '{name}'");
            }
            return scope;
        }

        /// <summary>
        /// TryGetVariable Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable is not exists, return null.
        /// </remarks>
        public bool TryGetVariable(string name, out PyObject value)
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

        public T GetVariable<T>(string name)
        {
            Check();
            PyObject pyObj = GetVariable(name);
            if (pyObj == null)
            {
                return default(T);
            }
            return pyObj.As<T>();
        }

        public bool TryGetVariable<T>(string name, out T value)
        {
            Check();
            PyObject pyObj;
            var result = TryGetVariable(name, out pyObj);
            if (!result)
            {
                value = default(T);
                return false;
            }            
            if (pyObj == null)
            {
                if(typeof(T).IsValueType)
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
            result = this.GetVariable(binder.Name);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this.SetVariable(binder.Name, value);
            return true;
        }

        private void Check()
        {
            if (isDisposed)
            {
                throw new PyScopeException($"The scope of name '{Name}' object has been disposed");
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            Runtime.XDecref(obj);
            this.OnDispose?.Invoke(this);
        }

        ~PyScope()
        {
            Dispose();
        }
    }

    public class PyScopeManager
    {
        public readonly static PyScopeManager Global = new PyScopeManager();

        private Dictionary<string, PyScope> NamedScopes = new Dictionary<string, PyScope>();

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

        [PyGIL]
        public PyScope Create()
        {
            var scope = this.NewScope(null);
            return scope;
        }

        [PyGIL]
        public PyScope Create(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name != null && NamedScopes.ContainsKey(name))
            {
                throw new PyScopeException($"A scope of name '{name}' does already exist");
            }
            var scope = this.NewScope(name);
            scope.OnDispose += Remove;
            NamedScopes[name] = scope;
            return scope;
        }

        public bool Contains(string name)
        {
            return NamedScopes.ContainsKey(name);
        }

        public PyScope Get(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (NamedScopes.ContainsKey(name))
            {
                return NamedScopes[name];
            }
            throw new PyScopeException($"There is no scope named '{name}' registered in this manager");
        }

        public bool TryGet(string name, out PyScope scope)
        {
            return NamedScopes.TryGetValue(name, out scope);
        }

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
