#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;

namespace Python.Runtime
{
    public class PyModule : PyObject
    {
        /// <summary>
        /// the variable dict of the module. Borrowed.
        /// </summary>
        internal readonly IntPtr variables;
        internal BorrowedReference VarsRef => new BorrowedReference(variables);

        public PyModule(string name = "")
            : this(Create(name ?? throw new ArgumentNullException(nameof(name))))
        {
        }

        public PyModule(string name, string? fileName = null) : this(Create(name, fileName)) { }

        static StolenReference Create(string name, string? filename = null)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            NewReference op = Runtime.PyModule_New(name);
            PythonException.ThrowIfIsNull(op);

            if (filename is not null)
            {
                BorrowedReference globals = Runtime.PyModule_GetDict(op);
                PythonException.ThrowIfIsNull(globals);
                using var pyFileName = filename.ToPython();
                int rc = Runtime.PyDict_SetItemString(globals, "__file__", pyFileName.Reference);
                PythonException.ThrowIfIsNotZero(rc);
            }

            return op.Steal();
        }

        internal PyModule(in StolenReference reference) : base(reference)
        {
            if (!IsModule(Reference))
            {
                throw new ArgumentException("object is not a module");
            }
            //Refcount of the variables not increase
            variables = Runtime.PyModule_GetDict(Reference).DangerousGetAddress();
            PythonException.ThrowIfIsNull(variables);

            int res = Runtime.PyDict_SetItem(
                VarsRef, new BorrowedReference(PyIdentifier.__builtins__),
                Runtime.PyEval_GetBuiltins()
            );
            PythonException.ThrowIfIsNotZero(res);
        }
        internal PyModule(BorrowedReference reference) : this(new NewReference(reference).Steal())
        {
        }

        /// <summary>
        /// Given a module or package name, import the module and return the resulting object.
        /// </summary>
        /// <param name="name">Fully-qualified module or package name</param>
        public static PyObject Import(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            NewReference op = Runtime.PyImport_ImportModule(name);
            PythonException.ThrowIfIsNull(op);
            return IsModule(op) ? new PyModule(op.Steal()) : op.MoveToPyObject();
        }

        /// <summary>
        /// Reloads the module, and returns the updated object
        /// </summary>
        public PyModule Reload()
        {
            NewReference op = Runtime.PyImport_ReloadModule(this.Reference);
            PythonException.ThrowIfIsNull(op);
            return new PyModule(op.Steal());
        }

        public static PyModule FromString(string name, string code)
        {
            using NewReference c = Runtime.Py_CompileString(code, "none", (int)RunFlagType.File);
            PythonException.ThrowIfIsNull(c);
            NewReference m = Runtime.PyImport_ExecCodeModule(name, c);
            PythonException.ThrowIfIsNull(m);
            return new PyModule(m.Steal());
        }

        public void SetBuiltins(PyDict builtins)
        {
            if (builtins == null || builtins.IsNone())
            {
                throw new ArgumentNullException(nameof(builtins));
            }

            BorrowedReference globals = Runtime.PyModule_GetDict(this.Reference);
            PythonException.ThrowIfIsNull(globals);
            int rc = Runtime.PyDict_SetItemString(globals, "__builtins__", builtins.Reference);
            PythonException.ThrowIfIsNotZero(rc);
        }

        public static PyDict SysModules
        {
            get
            {
                BorrowedReference sysModulesRef = Runtime.PyImport_GetModuleDict();
                PythonException.ThrowIfIsNull(sysModulesRef);
                return new PyDict(sysModulesRef);
            }
        }

        internal static bool IsModule(BorrowedReference reference)
        {
            if (reference == null) return false;
            BorrowedReference type = Runtime.PyObject_TYPE(reference);
            return Runtime.PyType_IsSubtype(type, Runtime.PyModuleType);
        }

        /// <summary>
        /// Returns the variables dict of the module.
        /// </summary>
        public PyDict Variables() => new(VarsRef);

        /// <summary>
        /// Create a scope, and import all from this scope
        /// </summary>
        public PyModule NewScope()
        {
            var scope = new PyModule();
            scope.ImportAll(this);
            return scope;
        }
        /// <summary>
        /// Import module by its name.
        /// </summary>
        public PyObject Import(string name, string? asname = null)
        {
            Check();

            asname ??= name;

            var module = PyModule.Import(name);
            Import(module, asname);
            return module;
        }

        /// <summary>
        /// Import module as a variable of given name.
        /// </summary>
        public void Import(PyModule module, string asname)
        {
            this.SetPyValue(asname, module.Handle);
        }

        /// <summary>
        /// The 'import .. as ..' statement in Python.
        /// Import a module as a variable.
        /// </summary>
        public void Import(PyObject module, string? asname = null)
        {
            asname ??= module.GetAttr("__name__").As<string>();
            Set(asname, module);
        }

        /// <summary>
        /// Import all variables of the module into this module.
        /// </summary>
        public void ImportAll(PyModule module)
        {
            int result = Runtime.PyDict_Update(VarsRef, module.VarsRef);
            if (result < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <remarks>
        /// Import all variables of the module into this module.
        /// </remarks>
        public void ImportAll(PyObject module)
        {
            if (module is null) throw new ArgumentNullException(nameof(module));

            if (!IsModule(module.Reference))
            {
                throw new ArgumentException("object is not a module", paramName: nameof(module));
            }
            var module_dict = Runtime.PyModule_GetDict(module.Reference);
            int result = Runtime.PyDict_Update(VarsRef, module_dict);
            if (result < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <summary>
        /// Import all variables in the dictionary into this module.
        /// </summary>
        public void ImportAll(PyDict dict)
        {
            int result = Runtime.PyDict_Update(VarsRef, dict.Reference);
            if (result < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <summary>
        /// Execute method
        /// </summary>
        /// <remarks>
        /// Execute a Python ast and return the result as a PyObject.
        /// The ast can be either an expression or stmts.
        /// </remarks>
        public PyObject Execute(PyObject script, PyDict? locals = null)
        {
            Check();
            IntPtr _locals = locals == null ? variables : locals.obj;
            IntPtr ptr = Runtime.PyEval_EvalCode(script.Handle, variables, _locals);
            PythonException.ThrowIfIsNull(ptr);
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
        public T Execute<T>(PyObject script, PyDict? locals = null)
        {
            Check();
            PyObject pyObj = Execute(script, locals);
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
        public PyObject Eval(string code, PyDict? locals = null)
        {
            Check();
            BorrowedReference _locals = locals == null ? VarsRef : locals.Reference;

            NewReference reference = Runtime.PyRun_String(
                code, RunFlagType.Eval, VarsRef, _locals
            );
            PythonException.ThrowIfIsNull(reference);
            return reference.MoveToPyObject();
        }

        /// <summary>
        /// Evaluate a Python expression
        /// </summary>
        /// <remarks>
        /// Evaluate a Python expression
        /// and  convert the result to a Managed Object of given type.
        /// </remarks>
        public T Eval<T>(string code, PyDict? locals = null)
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
        public void Exec(string code, PyDict? locals = null)
        {
            Check();
            BorrowedReference _locals = locals == null ? VarsRef : locals.Reference;
            Exec(code, VarsRef, _locals);
        }

        private void Exec(string code, BorrowedReference _globals, BorrowedReference _locals)
        {
            using NewReference reference = Runtime.PyRun_String(
                code, RunFlagType.File, _globals, _locals
            );
            PythonException.ThrowIfIsNull(reference);
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
            if (name is null) throw new ArgumentNullException(nameof(name));

            IntPtr _value = Converter.ToPython(value, value?.GetType());
            SetPyValue(name, _value);
            Runtime.XDecref(_value);
        }

        private void SetPyValue(string name, IntPtr value)
        {
            Check();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_SetItem(variables, pyKey.obj, value);
                if (r < 0)
                {
                    throw PythonException.ThrowLastAsClrException();
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
            if (name is null) throw new ArgumentNullException(nameof(name));

            Check();
            using (var pyKey = new PyString(name))
            {
                int r = Runtime.PyObject_DelItem(variables, pyKey.obj);
                if (r < 0)
                {
                    throw PythonException.ThrowLastAsClrException();
                }
            }
        }

        /// <summary>
        /// Returns true if the variable exists in the module.
        /// </summary>
        public bool Contains(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            Check();
            using (var pyKey = new PyString(name))
            {
                return Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0;
            }
        }

        /// <summary>
        /// Returns the value of the variable with the given name.
        /// </summary>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when variable with the given name does not exist.
        /// </exception>
        public PyObject Get(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            var state = TryGet(name, out var value);
            if (!state)
            {
                throw new KeyNotFoundException($"The module has no attribute '{name}'");
            }
            return value!;
        }

        /// <summary>
        /// TryGet Method
        /// </summary>
        /// <remarks>
        /// Returns the value of the variable, local variable first.
        /// If the variable does not exist, return null.
        /// </remarks>
        public bool TryGet(string name, out PyObject? value)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            Check();
            using (var pyKey = new PyString(name))
            {
                if (Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0)
                {
                    IntPtr op = Runtime.PyObject_GetItem(variables, pyKey.obj);
                    if (op == IntPtr.Zero)
                    {
                        throw PythonException.ThrowLastAsClrException();
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
            if (name is null) throw new ArgumentNullException(nameof(name));

            Check();
            PyObject pyObj = Get(name);
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
        public bool TryGet<T>(string name, out T? value)
        {
            Check();
            var result = TryGet(name, out var pyObj);
            if (!result)
            {
                value = default(T);
                return false;
            }
            value = pyObj!.As<T>();
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = CheckNone(this.Get(binder.Name));
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this.Set(binder.Name, value);
            return true;
        }

        private void Check()
        {
            if (this.obj == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(PyModule));
            }
        }
    }
}
