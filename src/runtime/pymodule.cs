using System;

namespace Python.Runtime
{
    public class PyModule : PyScope
    {
        internal PyModule(ref NewReference reference) : base(ref reference, PyScopeManager.Global) { }
        public PyModule(PyObject o) : base(o.Reference, PyScopeManager.Global) { }

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
    }
}
