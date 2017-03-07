using System;
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

    public interface IPyObject : IDisposable
    {
    }

    public class PyScope : DynamicObject, IPyObject
    {
        public string Name
        {
            get;
            private set;
        }

        private bool isDisposed;

        internal PyScope(string name)
        {
            this.Name = name;
            variables = Runtime.PyDict_New();
            if (variables == IntPtr.Zero)
            {
                throw new PythonException();
            }
            Runtime.PyDict_SetItemString(
                variables, "__builtins__",
                Runtime.PyEval_GetBuiltins()
            );
        }
        
        /// <summary>
        /// the dict for local variables
        /// </summary>
        internal IntPtr variables { get; private set; }

        public event Action<PyScope> OnDispose;

        public PyScope CreateScope()
        {
            var scope = new PyScope(null);
            scope.ImportScope(this);
            return scope;
        }

        public void ImportScope(string name)
        {
            var scope = Py.GetScope(name);
            ImportScope(scope);
        }

        public void ImportScope(PyScope scope)
        {
            int result = Runtime.PyDict_Update(variables, scope.variables);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the local variable dict and return the resulting module object as a PyObject.
        /// </remarks>
        public PyObject ImportModule(string name)
        {
            return ImportModule(name, name);
        }

        /// <summary>
        /// Import Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the local variable dict and return the resulting module object as a PyObject.
        /// </remarks>
        public PyObject ImportModule(string name, string asname)
        {
            Check();
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
            Check();
            IntPtr ptr = Runtime.PyEval_EvalCode(script.Handle, variables, variables);
            Runtime.CheckExceptionOccurred();
            if (ptr == Runtime.PyNone)
            {
                Runtime.XDecref(ptr);
                return null;
            }
            return new PyObject(ptr);
        }

        public T Execute<T>(PyObject script)
        {
            Check();
            PyObject pyObj = Execute(script);
            if (pyObj == null)
            {
                return default(T);
            }
            var obj = (T)ToManagedObject<T>(pyObj);
            return obj;
        }

        public T ExecuteVariable<T>(string name)
        {
            PyObject script = GetVariable(name);
            return Execute<T>(script);
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
            Check();
            var flag = (IntPtr)Runtime.Py_eval_input;
            IntPtr ptr = Runtime.PyRun_String(
                code, flag, variables, variables
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
        public T Eval<T>(string code)
        {
            Check();
            PyObject pyObj = Eval(code);
            var obj = (T)ToManagedObject<T>(pyObj);
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
            Check();
            Exec(code, variables, variables);
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
        /// SetLocal Method
        /// </summary>
        /// <remarks>
        /// Add a new variable to local variable dict if it not exists
        /// or set the value of the local variable if it exists.
        /// </remarks>
        public void SetVariable(string name, object value)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                IntPtr _value = Converter.ToPython(value, value?.GetType());
                int r = Runtime.PyObject_SetItem(variables, pyKey.obj, _value);
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
        /// Exists Method
        /// </summary>
        /// <remarks>
        /// Returns true if the variable appears in the local variable dict or the global variable dict.
        /// </remarks>
        public bool ContainsVariable(string name)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                return Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0;
            }
        }

        private PyObject _GetVariable(string name)
        {
            using (var pyKey = new PyString(name))
            {
                IntPtr op;
                if (Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0)
                {
                    op = Runtime.PyObject_GetItem(variables, pyKey.obj);
                }
                else
                {
                    return null;
                }
                if (op == IntPtr.Zero)
                {
                    throw new PythonException();
                }
                return new PyObject(op);
            }
        }

        /// <summary>
        /// GetVariable Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable is not exists, return null.
        /// </remarks>
        public PyObject GetVariable(string name)
        {
            Check();
            var variable = _GetVariable(name);
            if (variable == null)
            {
                throw new PyScopeException(String.Format("'ScopeStorage' object has no attribute '{0}'", name));
            }
            if (variable.Handle == Runtime.PyNone)
            {
                variable.Dispose();
                return null;
            }
            return variable;
        }

        /// <summary>
        /// GetVariable Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable is not exists, return null.
        /// </remarks>
        public bool TryGetVariable(string name, out PyObject value)
        {
            Check();
            var variable = _GetVariable(name);
            if (variable == null)
            {
                value = null;
                return false;
            }
            if (variable.Handle == Runtime.PyNone)
            {
                value = null;
                return true;
            }
            value = variable;
            return true;
        }

        public T GetVariable<T>(string name)
        {
            Check();
            PyObject pyObj = GetVariable(name);
            if (pyObj == null)
            {
                return default(T);
            }
            return (T)ToManagedObject<T>(pyObj);
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
                value = default(T);
                return true;
            }
            value = (T)ToManagedObject<T>(pyObj);
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

        // Currently, AsManagedObject method cannot accept 'dynamic' for the T parameter
        private object ToManagedObject<T>(PyObject pyObj)
        {
            if(typeof(T) == typeof(PyObject) || typeof(T) == typeof(object))
            {
                return pyObj;
            }
            return pyObj.AsManagedObject(typeof(T));
        }

        private void Check()
        {
            if (isDisposed)
            {
                throw new PyScopeException("'ScopeStorage' object has been disposed");
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            Runtime.XDecref(variables);
            if (this.OnDispose != null)
            {
                this.OnDispose(this);
            }
        }

        ~PyScope()
        {
            Dispose();
        }
    }
}
