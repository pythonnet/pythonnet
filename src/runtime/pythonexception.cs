using System;

namespace Python.Runtime
{
    /// <summary>
    /// Provides a managed interface to exceptions thrown by the Python
    /// runtime.
    /// </summary>
    public class PythonException : System.Exception
    {
        private IntPtr _pyType = IntPtr.Zero;
        private IntPtr _pyValue = IntPtr.Zero;
        private IntPtr _pyTB = IntPtr.Zero;
        private string _tb = "";
        private string _message = "";
        private bool disposed = false;

        public PythonException()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Fetch(ref _pyType, ref _pyValue, ref _pyTB);
            Runtime.XIncref(_pyType);
            Runtime.XIncref(_pyValue);
            Runtime.XIncref(_pyTB);
            if (_pyType != IntPtr.Zero && _pyValue != IntPtr.Zero)
            {
                string type;
                string message;
                Runtime.XIncref(_pyType);
                using (var pyType = new PyObject(_pyType))
                using (PyObject pyTypeName = pyType.GetAttr("__name__"))
                {
                    type = pyTypeName.ToString();
                }

                Runtime.XIncref(_pyValue);
                using (var pyValue = new PyObject(_pyValue))
                {
                    message = pyValue.ToString();
                }
                _message = type + " : " + message;
            }
            if (_pyTB != IntPtr.Zero)
            {
                PyObject tb_module = PythonEngine.ImportModule("traceback");
                Runtime.XIncref(_pyTB);
                using (var pyTB = new PyObject(_pyTB))
                {
                    _tb = tb_module.InvokeMethod("format_tb", pyTB).ToString();
                }
            }
            PythonEngine.ReleaseLock(gs);
        }

        // Ensure that encapsulated Python objects are decref'ed appropriately
        // when the managed exception wrapper is garbage-collected.

        ~PythonException()
        {
            // We needs to disable Finalizers until it's valid implementation.
            // Current implementation can produce low probability floating bugs.
            return;

            Dispose();
        }

        /// <summary>
        /// Restores python error.
        /// </summary>
        public void Restore()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Restore(_pyType, _pyValue, _pyTB);
            _pyType = IntPtr.Zero;
            _pyValue = IntPtr.Zero;
            _pyTB = IntPtr.Zero;
            PythonEngine.ReleaseLock(gs);
        }

        /// <summary>
        /// PyType Property
        /// </summary>
        /// <remarks>
        /// Returns the exception type as a Python object.
        /// </remarks>
        public IntPtr PyType
        {
            get { return _pyType; }
        }

        /// <summary>
        /// PyValue Property
        /// </summary>
        /// <remarks>
        /// Returns the exception value as a Python object.
        /// </remarks>
        public IntPtr PyValue
        {
            get { return _pyValue; }
        }

        /// <summary>
        /// PyTB Property
        /// </summary>
        /// <remarks>
        /// Returns the TraceBack as a Python object.
        /// </remarks>
        public IntPtr PyTB
        {
            get { return _pyTB; }
        }

        /// <summary>
        /// Message Property
        /// </summary>
        /// <remarks>
        /// A string representing the python exception message.
        /// </remarks>
        public override string Message
        {
            get { return _message; }
        }

        /// <summary>
        /// StackTrace Property
        /// </summary>
        /// <remarks>
        /// A string representing the python exception stack trace.
        /// </remarks>
        public override string StackTrace
        {
            get { return _tb; }
        }


        /// <summary>
        /// Dispose Method
        /// </summary>
        /// <remarks>
        /// The Dispose method provides a way to explicitly release the
        /// Python objects represented by a PythonException.
        /// If object not properly disposed can cause AppDomain unload issue.
        /// See GH#397 and GH#400.
        /// </remarks>
        public void Dispose()
        {
            if (!disposed)
            {
                if (Runtime.Py_IsInitialized() > 0 && !Runtime.IsFinalizing)
                {
                    IntPtr gs = PythonEngine.AcquireLock();
                    Runtime.XDecref(_pyType);
                    Runtime.XDecref(_pyValue);
                    // XXX Do we ever get TraceBack? //
                    if (_pyTB != IntPtr.Zero)
                    {
                        Runtime.XDecref(_pyTB);
                    }
                    PythonEngine.ReleaseLock(gs);
                }
                GC.SuppressFinalize(this);
                disposed = true;
            }
        }

        /// <summary>
        /// Matches Method
        /// </summary>
        /// <remarks>
        /// Returns true if the Python exception type represented by the
        /// PythonException instance matches the given exception type.
        /// </remarks>
        public static bool Matches(IntPtr ob)
        {
            return Runtime.PyErr_ExceptionMatches(ob) != 0;
        }
    }
}
