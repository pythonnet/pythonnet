using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Provides a managed interface to exceptions thrown by the Python
    /// runtime.
    /// </summary>
    public class PythonException : System.Exception, IDisposable
    {
        private IntPtr _pyType = IntPtr.Zero;
        private IntPtr _pyValue = IntPtr.Zero;
        private IntPtr _pyTB = IntPtr.Zero;
        private readonly string _tb = "";
        private readonly string _message = "";
        private readonly string _pythonTypeName = "";
        private bool disposed = false;
        private bool _finalized = false;

        public PythonException()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Fetch(out _pyType, out _pyValue, out _pyTB);
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

                _pythonTypeName = type;

                // TODO: If pyValue has a __cause__ attribute, then we could set this.InnerException to the equivalent managed exception.
                Runtime.XIncref(_pyValue);
                using (var pyValue = new PyObject(_pyValue))
                {
                    message = pyValue.ToString();
                }
                _message = type + " : " + message;
            }

            if (_pyTB != IntPtr.Zero)
            {
                using PyObject tb_module = PythonEngine.ImportModule("traceback");

                Runtime.XIncref(_pyTB);
                using var pyTB = new PyObject(_pyTB);

                using var tbList = tb_module.InvokeMethod("format_tb", pyTB);

                var sb = new StringBuilder();
                // Reverse Python's traceback list to match the order used in C#
                // stacktraces
                foreach (var line in tbList.Reverse()) {
                    sb.Append(line.ToString());
                }
                _tb = sb.ToString();
            }
            PythonEngine.ReleaseLock(gs);
        }

        // Ensure that encapsulated Python objects are decref'ed appropriately
        // when the managed exception wrapper is garbage-collected.

        ~PythonException()
        {
            if (_finalized || disposed)
            {
                return;
            }
            _finalized = true;
            Finalizer.Instance.AddFinalizedObject(ref _pyType);
            Finalizer.Instance.AddFinalizedObject(ref _pyValue);
            Finalizer.Instance.AddFinalizedObject(ref _pyTB);
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
        public override string StackTrace => $"{_tb}===\n{base.StackTrace}";

        /// <summary>
        /// Python error type name.
        /// </summary>
        public string PythonTypeName
        {
            get { return _pythonTypeName; }
        }

        /// <summary>
        /// Replaces PyValue with an instance of PyType, if PyValue is not already an instance of PyType.
        /// Often PyValue is a string and this method will replace it with a proper exception object.
        /// Must not be called when an error is set.
        /// </summary>
        public void Normalize()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                if (Exceptions.ErrorOccurred()) throw new InvalidOperationException("Cannot normalize when an error is set");
                // If an error is set and this PythonException is unnormalized, the error will be cleared and the PythonException will be replaced by a different error.
                Runtime.PyErr_NormalizeException(ref _pyType, ref _pyValue, ref _pyTB);
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }

        /// <summary>
        /// Formats this PythonException object into a message as would be printed
        /// out via the Python console. See traceback.format_exception
        /// </summary>
        public string Format()
        {
            string res;
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                if (_pyTB != IntPtr.Zero && _pyType != IntPtr.Zero && _pyValue != IntPtr.Zero)
                {
                    IntPtr tb = _pyTB;
                    IntPtr type = _pyType;
                    IntPtr value = _pyValue;

                    Runtime.XIncref(type);
                    Runtime.XIncref(value);
                    Runtime.XIncref(tb);
                    Runtime.PyErr_NormalizeException(ref type, ref value, ref tb);

                    using (PyObject pyType = new PyObject(type))
                    using (PyObject pyValue = new PyObject(value))
                    using (PyObject pyTB = new PyObject(tb))
                    using (PyObject tb_mod = PythonEngine.ImportModule("traceback"))
                    {
                        var buffer = new StringBuilder();
                        var values = tb_mod.InvokeMethod("format_exception", pyType, pyValue, pyTB);
                        foreach (PyObject val in values)
                        {
                            buffer.Append(val.ToString());
                        }
                        res = buffer.ToString();
                    }
                }
                else
                {
                    res = StackTrace;
                }
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
            return res;
        }

        public bool IsMatches(IntPtr exc)
        {
            return Runtime.PyErr_GivenExceptionMatches(PyType, exc) != 0;
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
                    if (_pyType != IntPtr.Zero)
                    {
                        Runtime.XDecref(_pyType);
                        _pyType= IntPtr.Zero;
                    }

                    if (_pyValue != IntPtr.Zero)
                    {
                        Runtime.XDecref(_pyValue);
                        _pyValue = IntPtr.Zero;
                    }

                    // XXX Do we ever get TraceBack? //
                    if (_pyTB != IntPtr.Zero)
                    {
                        Runtime.XDecref(_pyTB);
                        _pyTB = IntPtr.Zero;
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

        [System.Diagnostics.DebuggerHidden]
        public static void ThrowIfIsNull(IntPtr ob)
        {
            if (ob == IntPtr.Zero)
            {
                throw new PythonException();
            }
        }

        [System.Diagnostics.DebuggerHidden]
        internal static void ThrowIfIsNull(BorrowedReference reference)
        {
            if (reference.IsNull)
            {
                throw new PythonException();
            }
        }

        [System.Diagnostics.DebuggerHidden]
        public static void ThrowIfIsNotZero(int value)
        {
            if (value != 0)
            {
                throw new PythonException();
            }
        }
    }
}
