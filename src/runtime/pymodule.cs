using System;
using System.Text;

using Python.Runtime.Native;

namespace Python.Runtime
{
    public class PyModule : PyScope
    {
        internal PyModule(ref NewReference reference) : base(ref reference, PyScopeManager.Global) { }
        public PyModule(PyObject o) : base(o.Reference, PyScopeManager.Global) { }
        public PyModule(string name, string filename = null) : this(Create(name, filename)) { }

        /// <summary>
        /// Given a module or package name, import the
        /// module and return the resulting module object as a <see cref="PyModule"/>.
        /// </summary>
        /// <param name="name">Fully-qualified module or package name</param>
        public static PyModule Import(string name)
        {
            NewReference op = Runtime.PyImport_ImportModule(name);
            PythonException.ThrowIfIsNull(op);
            return new PyModule(ref op);
        }

        /// <summary>
        /// Reloads the module, and returns the updated object
        /// </summary>
        public PyModule Reload()
        {
            NewReference op = Runtime.PyImport_ReloadModule(this.Reference);
            PythonException.ThrowIfIsNull(op);
            return new PyModule(ref op);
        }

        public static PyModule FromString(string name, string code)
        {
            using NewReference c = Runtime.Py_CompileString(code, "none", (int)RunFlagType.File);
            PythonException.ThrowIfIsNull(c);
            NewReference m = Runtime.PyImport_ExecCodeModule(name, c);
            PythonException.ThrowIfIsNull(m);
            return new PyModule(ref m);
        }

        private static PyModule Create(string name, string filename=null)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            NewReference op = Runtime.PyModule_New(name);
            PythonException.ThrowIfIsNull(op);

            if (filename != null)
            {
                BorrowedReference globals = Runtime.PyModule_GetDict(op);
                PythonException.ThrowIfIsNull(globals);
                int rc = Runtime.PyDict_SetItemString(globals, "__file__", filename.ToPython().Reference);
                PythonException.ThrowIfIsNotZero(rc);
            }

            return new PyModule(ref op);
        }

        public void SetBuiltins(PyDict builtins)
        {
            if(builtins == null || builtins.IsNone())
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
    }
}
