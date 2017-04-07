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
    /// Classes implement this interface must be used with GIL obtained.
    /// </summary>
    public interface IPyObject : IDisposable
    {
    }

    public class PyScope : DynamicObject, IPyObject
    {
        public readonly string Name;

        internal readonly IntPtr obj;

        /// <summary>
        /// the dict for local variables
        /// </summary>
        internal readonly IntPtr variables;

        private bool isDisposed;
        
        internal static PyScope New(string name = null)
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
            return new PyScope(module);
        }
        
        private PyScope(IntPtr ptr)
        {
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

        public event Action<PyScope> OnDispose;

        public PyDict Variables()
        {
            Runtime.XIncref(variables);
            return new PyDict(variables);
        }

        public PyScope NewScope()
        {
            var scope = PyScope.New();
            scope.ImportAllFromScope(this);
            return scope;
        }

        public void ImportAllFromScope(string name)
        {
            var scope = Py.GetScope(name);
            ImportAllFromScope(scope);
        }

        public void ImportAllFromScope(PyScope scope)
        {
            int result = Runtime.PyDict_Update(variables, scope.variables);
            if (result < 0)
            {
                throw new PythonException();
            }
        }

        public void ImportScope(string name, string asname = null)
        {
            var scope = Py.GetScope(name);
            if(asname == null)
            {
                asname = name;
            }
            ImportScope(scope, asname);
        }

        public void ImportScope(PyScope scope, string asname)
        {
            this.SetVariable(asname, scope.obj);
        }

        /// <summary>
        /// ImportModule Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the variables dict and return the resulting module object as a PyObject.
        /// </remarks>
        public PyObject ImportModule(string name)
        {
            return ImportModule(name, name);
        }

        /// <summary>
        /// ImportModule Method
        /// </summary>
        /// <remarks>
        /// The import .. as .. statement in Python.
        /// Import a module ,add it to the variables dict and return the resulting module object as a PyObject.
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

        public void AddVariables(PyDict dict)
        {
            int result = Runtime.PyDict_Update(variables, dict.obj);
            if (result < 0)
            {
                throw new PythonException();
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
                        return null;
                    }
                    return new PyObject(op);
                }
                else
                {
                    throw new PyScopeException(String.Format("'ScopeStorage' object has no attribute '{0}'", name));
                }
            }
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
                value = default(T);
                return true;
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
            Runtime.XDecref(obj);
            this.OnDispose?.Invoke(this);
        }

        ~PyScope()
        {
            Dispose();
        }
    }
}
