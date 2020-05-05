using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Provides a managed interface to exceptions thrown by the Python
    /// runtime.
    /// </summary>
    public class PythonException : System.Exception, IDisposable
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
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
                try
                {
                    var clrObject = ManagedType.GetManagedObject(value) as CLRObject;
#if NETSTANDARD
                    if (clrObject?.inst is ExceptionDispatchInfo storedException)
                    {
                        storedException.Throw();
                        throw storedException.SourceException; // unreachable
                    }
#endif
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

        /// <summary>
        /// Requires lock to be acquired elsewhere
        /// </summary>
        static Exception FromPyErr(BorrowedReference typeHandle, BorrowedReference valueHandle, BorrowedReference tracebackHandle)
        {
            Exception inner = null;
            string pythonTypeName = null, msg = "", tracebackText = null;

            var clrObject = ManagedType.GetManagedObject(valueHandle) as CLRObject;
            if (clrObject?.inst is Exception e)
            {
                return e;
            }

#if NETSTANDARD
            if (clrObject?.inst is ExceptionDispatchInfo exceptionDispatchInfo)
            {
                return exceptionDispatchInfo.SourceException;
            }
#endif

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
                msg = pythonTypeName + " : " + value;
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
            }

            PyObject tracebackModule = PythonEngine.ImportModule("traceback");
            PyList stackLines = new PyList(tracebackModule.InvokeMethod("format_tb", traceback));
            stackLines.Reverse();
            var result = new StringBuilder();
            foreach (object stackLine in stackLines)
            {
                result.Append(stackLine);
            }
            return result.ToString();
        }

        /// <summary>
        /// Restores python error. Clears this instance.
        /// </summary>
        public void Restore()
        {
            if (this.disposed) throw new ObjectDisposedException(nameof(PythonException));

            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Restore(
                _type.MakeNewReferenceOrNull(),
                _value.MakeNewReferenceOrNull(),
                _pyTB.MakeNewReferenceOrNull());
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
            get { return _traceback + base.StackTrace; }
        }

        /// <summary>
        /// Python error type name.
        /// </summary>
        public string PythonTypeName
        {
            get { return _pythonTypeName; }
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
                    using (PyObject tb_mod = PythonEngine.ImportModule("traceback"))
                    {
                        var buffer = new StringBuilder();
                        var values = tb_mod.InvokeMethod("format_exception", _type, _value, _pyTB);
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

        internal static IntPtr ThrowIfIsNull(IntPtr ob)
        {
            if (ob == IntPtr.Zero)
            {
                throw ThrowLastAsClrException();
            }

            return ob;
        }

        internal static void ThrowIfIsNotZero(int value)
        {
            if (value != 0)
            {
                throw ThrowLastAsClrException();
            }
        }
    }
}
