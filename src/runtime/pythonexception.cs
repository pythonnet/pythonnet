using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Provides a managed interface to exceptions thrown by the Python
    /// runtime.
    /// </summary>
    public class PythonException : System.Exception
    {
        private PyObject _type;
        private PyObject _value;
        private PyObject _pyTB;
        private string _traceback = "";
        private string _message = "";
        private string _pythonTypeName = "";
        private bool disposed = false;

        [Obsolete("Please, use ThrowLastAsClrException or FromPyErr instead")]
        public PythonException()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
            _type = type.MoveToPyObjectOrNull();
            _value = value.MoveToPyObjectOrNull();
            _pyTB = traceback.MoveToPyObjectOrNull();
            if (_type != null && _value != null)
            {
                using (PyObject pyTypeName = _type.GetAttr("__name__"))
                {
                    _pythonTypeName = pyTypeName.ToString();
                }

                _message = _pythonTypeName + " : " + _value;
            }
            if (_pyTB != null)
            {
                _traceback = TracebackToString(_pyTB);
            }
            PythonEngine.ReleaseLock(gs);
        }

        private PythonException(PyObject type, PyObject value, PyObject traceback,
                                string message, string pythonTypeName, string tracebackText,
                                Exception innerException)
            : base(message, innerException)
        {
            _type = type;
            _value = value;
            _pyTB = traceback;
            _message = message;
            _pythonTypeName = pythonTypeName ?? _pythonTypeName;
            _traceback = tracebackText ?? _traceback;
        }

        /// <summary>
        /// Rethrows the last Python exception as corresponding CLR exception.
        /// It is recommended to call this as <code>throw ThrowLastAsClrException()</code>
        /// to assist control flow checks.
        /// </summary>
        internal static Exception ThrowLastAsClrException()
        {
            // prevent potential interop errors in this method
            // from crashing process with undebuggable StackOverflowException
            RuntimeHelpers.EnsureSufficientExecutionStack();

            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
                try
                {
#if NETSTANDARD
                    if (!value.IsNull())
                    {
                        var exceptionInfo = TryGetDispatchInfo(value);
                        if (exceptionInfo != null)
                        {
                            exceptionInfo.Throw();
                            throw exceptionInfo.SourceException; // unreachable
                        }
                    }
#endif

                    var clrObject = ManagedType.GetManagedObject(value) as CLRObject;
                    if (clrObject?.inst is Exception e)
                    {
                        throw e;
                    }

                    var result = FromPyErr(type, value, traceback);
                    throw result;
                }
                finally
                {
                    type.Dispose();
                    value.Dispose();
                    traceback.Dispose();
                }
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }

#if NETSTANDARD
        static ExceptionDispatchInfo TryGetDispatchInfo(BorrowedReference exception)
        {
            if (exception.IsNull) return null;

            var pyInfo = Runtime.PyObject_GetAttrString(exception, Exceptions.DispatchInfoAttribute);
            if (pyInfo.IsNull())
            {
                if (Exceptions.ExceptionMatches(Exceptions.AttributeError))
                {
                    Exceptions.Clear();
                }
                return null;
            }

            try
            {
                if (Converter.ToManagedValue(pyInfo, typeof(ExceptionDispatchInfo), out object result, setError: false))
                {
                    return (ExceptionDispatchInfo)result;
                }

                return null;
            }
            finally
            {
                pyInfo.Dispose();
            }
        }
#endif

        /// <summary>
        /// Requires lock to be acquired elsewhere
        /// </summary>
        static Exception FromPyErr(BorrowedReference typeHandle, BorrowedReference valueHandle, BorrowedReference tracebackHandle)
        {
            Exception inner = null;
            string pythonTypeName = null, msg = "", tracebackText = null;

            var exceptionDispatchInfo = TryGetDispatchInfo(valueHandle);
            if (exceptionDispatchInfo != null)
            {
                return exceptionDispatchInfo.SourceException;
            }

            var clrObject = ManagedType.GetManagedObject(valueHandle) as CLRObject;
            if (clrObject?.inst is Exception e)
            {
                return e;
            }

            var type = PyObject.FromNullableReference(typeHandle);
            var value = PyObject.FromNullableReference(valueHandle);
            var traceback = PyObject.FromNullableReference(tracebackHandle);

            if (type != null && value != null)
            {
                if (PyObjectConversions.TryDecode(valueHandle, typeHandle, typeof(Exception), out object decoded)
                    && decoded is Exception decodedException)
                {
                    return decodedException;
                }

                using (PyObject pyTypeName = type.GetAttr("__name__"))
                {
                    pythonTypeName = pyTypeName.ToString();
                }

                var cause = value.GetAttr("__cause__", null);
                if (cause != null && cause.Handle != Runtime.PyNone)
                {
                    using (var innerTraceback = cause.GetAttr("__traceback__", null))
                    {
                        inner = FromPyErr(
                            typeHandle: cause.GetPythonTypeReference(),
                            valueHandle: cause.Reference,
                            tracebackHandle: innerTraceback is null
                                ? BorrowedReference.Null
                                : innerTraceback.Reference);
                    }
                }
            }
            if (traceback != null)
            {
                tracebackText = TracebackToString(traceback);
            }

            return new PythonException(type, value, traceback,
                msg, pythonTypeName, tracebackText, inner);
        }

        static string TracebackToString(PyObject traceback)
        {
            if (traceback is null)
            {
                throw new ArgumentNullException(nameof(traceback));
                throw new ArgumentNullException(nameof(traceback));
            }
            _finalized = true;
            Finalizer.Instance.AddFinalizedObject(this);
        }

        /// <summary>Restores python error.</summary>
        public void Restore()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Restore(
                _type.MakeNewReferenceOrNull().Steal(),
                _value.MakeNewReferenceOrNull().Steal(),
                _pyTB.MakeNewReferenceOrNull().Steal());
            PythonEngine.ReleaseLock(gs);
        }

        /// <summary>
        /// PyType Property
        /// </summary>
        /// <remarks>
        /// Returns the exception type as a Python object.
        /// </remarks>
        public PyObject PyType => _type;

        /// <summary>
        /// PyValue Property
        /// </summary>
        /// <remarks>
        /// Returns the exception value as a Python object.
        /// </remarks>
        public PyObject PyValue => _value;

        /// <summary>
        /// PyTB Property
        /// </summary>
        /// <remarks>
        /// Returns the TraceBack as a Python object.
        /// </remarks>
        public PyObject PyTB => _pyTB;

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
            get { return _tb + base.StackTrace; }
        }

        /// <summary>
        /// Python error type name.
        /// </summary>
        public string PythonTypeName
        {
            get { return _pythonTypeName; }
        }

        /// <summary>
        /// Replaces PyValue with an instance of PyType, if PyValue is not already an instance of PyType.
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
                if (_pyTB != null && _type != null && _value != null)
                {
                    Runtime.XIncref(_pyType);
                    Runtime.XIncref(_pyValue);
                    Runtime.XIncref(_pyTB);
                    using (PyObject pyType = new PyObject(_pyType))
                    using (PyObject pyValue = new PyObject(_pyValue))
                    using (PyObject pyTB = new PyObject(_pyTB))
                    using (PyObject tb_mod = PythonEngine.ImportModule("traceback"))
                    {
                        var buffer = new StringBuilder();
                        var values = tb_mod.InvokeMethod("format_exception", _type, _value, _pyTB);
                        foreach (PyObject val in values)
                        {
                            buffer.Append(val.ToString());
                        }
                        res = buffer.ToString();
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
                _type?.Dispose();
                _value?.Dispose();
                _pyTB?.Dispose();
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
        internal static bool Matches(IntPtr ob)
        {
            return Runtime.PyErr_ExceptionMatches(ob) != 0;
        }

        public static void ThrowIfIsNull(IntPtr ob)
        {
            if (ob == IntPtr.Zero)
            {
                throw ThrowLastAsClrException();
            }

            return ob;
        }

        public static void ThrowIfIsNotZero(int value)
        {
            if (value != 0)
            {
                throw ThrowLastAsClrException();
            }
        }
    }
}
