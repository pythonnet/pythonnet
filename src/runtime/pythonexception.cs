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
                string message;
                using (PyObject pyTypeName = _type.GetAttr("__name__"))
                {
                    _pythonTypeName = pyTypeName.ToString();
                }

                message = _value.ToString();
                _message = _pythonTypeName + " : " + message;
            }
            if (_pyTB != null)
            {
                _traceback = TracebackToString(_pyTB);
            }
            PythonEngine.ReleaseLock(gs);
        }

        private PythonException(BorrowedReference pyTypeHandle,
                                BorrowedReference pyValueHandle,
                                BorrowedReference pyTracebackHandle,
                                string message, string pythonTypeName, string traceback,
                                Exception innerException)
            : base(message, innerException)
        {
            _type = PyObject.FromNullableReference(pyTypeHandle);
            _value = PyObject.FromNullableReference(pyValueHandle);
            _pyTB = PyObject.FromNullableReference(pyTracebackHandle);
            _message = message;
            _pythonTypeName = pythonTypeName ?? _pythonTypeName;
            _traceback = traceback ?? _traceback;
        }

        internal static Exception FromPyErr()
        {
            Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
            try
            {
                return FromPyErr(
                    typeHandle: type,
                    valueHandle: value,
                    tracebackHandle: traceback);
            }
            finally
            {
                type.Dispose();
                value.Dispose();
                traceback.Dispose();
            }
        }

        internal static Exception FromPyErrOrNull()
        {
            Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
            try
            {
                if (value.IsNull() && type.IsNull() && traceback.IsNull())
                {
                    return null;
                }

                var result = FromPyErr(type, value, traceback);
                return result;
            }
            finally
            {
                type.Dispose();
                value.Dispose();
                traceback.Dispose();
            }
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
                Runtime.PyErr_Fetch(out var pyTypeHandle, out var pyValueHandle, out var pyTracebackHandle);
                try
                {
                    var clrObject = ManagedType.GetManagedObject(pyValueHandle) as CLRObject;
                    if (clrObject?.inst is Exception e)
                    {
#if NETSTANDARD
                        ExceptionDispatchInfo.Capture(e).Throw();
#endif
                        throw e;
                    }

                    var result = FromPyErr(pyTypeHandle, pyValueHandle, pyTracebackHandle);
                    throw result;
                }
                finally
                {
                    pyTypeHandle.Dispose();
                    pyValueHandle.Dispose();
                    pyTracebackHandle.Dispose();
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
            string pythonTypeName = null, msg = "", traceback = null;

            var clrObject = ManagedType.GetManagedObject(valueHandle) as CLRObject;
            if (clrObject?.inst is Exception e)
            {
                return e;
            }

            if (!typeHandle.IsNull && !valueHandle.IsNull)
            {
                if (PyObjectConversions.TryDecode(valueHandle, typeHandle, typeof(Exception), out object decoded)
                    && decoded is Exception decodedException)
                {
                    return decodedException;
                }

                string type;
                string message;
                using (var pyType = new PyObject(typeHandle))
                using (PyObject pyTypeName = pyType.GetAttr("__name__"))
                {
                    type = pyTypeName.ToString();
                }

                pythonTypeName = type;

                using (var pyValue = new PyObject(valueHandle))
                {
                    message = pyValue.ToString();
                    var cause = pyValue.GetAttr("__cause__", null);
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
                msg = type + " : " + message;
            }
            if (!tracebackHandle.IsNull)
            {
                traceback = TracebackToString(new PyObject(tracebackHandle));
            }

            return new PythonException(typeHandle, valueHandle, tracebackHandle,
                msg, pythonTypeName, traceback, inner);
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
