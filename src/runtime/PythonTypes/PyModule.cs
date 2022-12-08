using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.Serialization;

namespace Python.Runtime
{
    [Serializable]
    public class PyModule : PyObject
    {
        internal BorrowedReference variables => VarsRef;
        internal BorrowedReference VarsRef
        {
            get
            {
                var vars = Runtime.PyModule_GetDict(Reference);
                PythonException.ThrowIfIsNull(vars);
                return vars;
            }
        }

        public PyModule(string name = "") : this(Create(name))
        {
            InitializeBuiltins();
        }

        static StolenReference Create(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return Runtime.PyModule_New(name).StealOrThrow();
        }

        internal PyModule(in StolenReference reference) : base(reference)
        {
            if (!IsModule(Reference))
            {
                throw new ArgumentException("object is not a module");
            }
        }

        protected PyModule(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        private void InitializeBuiltins()
        {
            int res = Runtime.PyDict_SetItem(
                VarsRef, PyIdentifier.__builtins__,
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
            return IsModule(op.BorrowOrThrow()) ? new PyModule(op.Steal()) : op.MoveToPyObject();
        }

        /// <summary>
        /// Reloads the module, and returns the updated object
        /// </summary>
        public PyModule Reload()
        {
            NewReference op = Runtime.PyImport_ReloadModule(this.Reference);
            return new PyModule(op.StealOrThrow());
        }

        public static PyModule FromString(string name, string code, string file = "")
        {
            //force valid value
            if(string.IsNullOrWhiteSpace(file))
            {
                file = "none";
            }

            using NewReference c = Runtime.Py_CompileString(code, file, (int)RunFlagType.File);
            NewReference m = Runtime.PyImport_ExecCodeModule(name, c.BorrowOrThrow());
            return new PyModule(m.StealOrThrow());
        }

        public PyModule SetBuiltins(PyDict builtins)
        {
            if (builtins == null || builtins.IsNone())
            {
                throw new ArgumentNullException(nameof(builtins));
            }

            BorrowedReference globals = Runtime.PyModule_GetDict(this.Reference);
            PythonException.ThrowIfIsNull(globals);
            int rc = Runtime.PyDict_SetItemString(globals, "__builtins__", builtins.Reference);
            PythonException.ThrowIfIsNotZero(rc);
            return this;
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
            if (module is null) throw new ArgumentNullException(nameof(module));
            if (asname is null) throw new ArgumentNullException(nameof(asname));
            this.SetPyValue(asname, module);
        }

        /// <summary>
        /// The 'import .. as ..' statement in Python.
        /// Import a module as a variable.
        /// </summary>
        public void Import(PyObject module, string? asname = null)
        {
            if (module is null) throw new ArgumentNullException(nameof(module));

            asname ??= module.GetAttr("__name__").As<string>();

            if (asname is null) throw new ArgumentException("Module has no name");

            Set(asname, module);
        }

        /// <summary>
        /// Import all variables of the module into this module.
        /// </summary>
        public void ImportAll(PyModule module)
        {
            if (module is null) throw new ArgumentNullException(nameof(module));

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
            if (dict is null) throw new ArgumentNullException(nameof(dict));

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
            if (script is null) throw new ArgumentNullException(nameof(script));

            Check();
            BorrowedReference _locals = locals == null ? variables : locals.obj;
            using var ptr = Runtime.PyEval_EvalCode(script, variables, _locals);
            PythonException.ThrowIfIsNull(ptr);
            return ptr.MoveToPyObject();
        }

        /// <summary>
        /// Execute a Python ast and return the result as a <see cref="PyObject"/>,
        /// and convert the result to a Managed Object of given type.
        /// The ast can be either an expression or stmts.
        /// </summary>
        public T? Execute<T>(PyObject script, PyDict? locals = null)
        {
            Check();
            PyObject pyObj = Execute(script, locals);
            var obj = pyObj.As<T>();
            return obj;
        }

        /// <summary>
        /// Evaluate a Python expression and return the result as a <see cref="PyObject"/>.
        /// </summary>
        public PyObject Eval(string code, PyDict? locals = null)
        {
            if (code is null) throw new ArgumentNullException(nameof(code));

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
        public T? Eval<T>(string code, PyDict? locals = null)
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
        public PyModule Exec(string code, PyDict? locals = null)
        {
            Check();
            BorrowedReference _locals = locals == null ? VarsRef : locals.Reference;
            Exec(code, VarsRef, _locals);
            return this;
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
        public PyModule Set(string name, object? value)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            using var _value = Converter.ToPythonDetectType(value);
            SetPyValue(name, _value.Borrow());
            return this;
        }

        private void SetPyValue(string name, BorrowedReference value)
        {
            Check();
            using var pyKey = new PyString(name);
            int r = Runtime.PyObject_SetItem(variables, pyKey.obj, value);
            if (r < 0)
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        /// <summary>
        /// Remove Method
        /// </summary>
        /// <remarks>
        /// Remove a variable from the variables dict.
        /// </remarks>
        public PyModule Remove(string name)
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
            return this;
        }

        /// <summary>
        /// Returns true if the variable exists in the module.
        /// </summary>
        public bool Contains(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));

            Check();
            using var pyKey = new PyString(name);
            return Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0;
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
            using var pyKey = new PyString(name);
            if (Runtime.PyMapping_HasKey(variables, pyKey.obj) != 0)
            {
                using var op = Runtime.PyObject_GetItem(variables, pyKey.obj);
                value = new PyObject(op.StealOrThrow());
                return true;
            }
            else
            {
                value = null;
                return false;
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
            return pyObj.As<T>()!;
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
                value = default;
                return false;
            }
            value = pyObj!.As<T>();
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = CheckNone(this.Get(binder.Name));
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            this.Set(binder.Name, value);
            return true;
        }

        private void Check()
        {
            if (this.rawPtr == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(PyModule));
            }
        }
    }
}
