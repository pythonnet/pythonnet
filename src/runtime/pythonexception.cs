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

        private PythonException(PyType type, PyObject value, PyObject traceback,
                                Exception innerException)
            : base("An exception has occurred in Python code", innerException)
        {
            Type = type;
            Value = value;
            Traceback = traceback;
        }

        private PythonException(PyType type, PyObject value, PyObject traceback)
            : base("An exception has occurred in Python code")
        {
            Type = type;
            Value = value;
            Traceback = traceback;
        }

        /// <summary>
        /// Rethrows the last Python exception as corresponding CLR exception.
        /// It is recommended to call this as <code>throw ThrowLastAsClrException()</code>
        /// to assist control flow checks.
        /// </summary>
        internal static Exception ThrowLastAsClrException()
        {
            var exception = FetchCurrentOrNull(out ExceptionDispatchInfo dispatchInfo)
                            ?? throw new InvalidOperationException("No exception is set");
            dispatchInfo?.Throw();
            // when dispatchInfo is not null, this line will not be reached
            throw exception;
        }

        internal static PythonException FetchCurrentOrNullRaw()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                Runtime.PyErr_Fetch(type: out var type, val: out var value, tb: out var traceback);
                if (type.IsNull() && value.IsNull())
                    return null;

                try
                {
                    return new PythonException(
                        type: new PyType(type.Steal()),
                        value: value.MoveToPyObjectOrNull(),
                        traceback: traceback.MoveToPyObjectOrNull());
                }
                finally
                {
                    type.Dispose();
                }
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }
        internal static PythonException FetchCurrentRaw()
            => FetchCurrentOrNullRaw()
               ?? throw new InvalidOperationException("No exception is set");

        internal static Exception FetchCurrentOrNull(out ExceptionDispatchInfo dispatchInfo)
        {
            dispatchInfo = default;

            // prevent potential interop errors in this method
            // from crashing process with undebuggable StackOverflowException
            RuntimeHelpers.EnsureSufficientExecutionStack();

            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
                if (value.IsNull() && type.IsNull()) return null;

                try
                {
                    if (!value.IsNull())
                    {
                        dispatchInfo = TryGetDispatchInfo(value);
                        if (dispatchInfo != null)
                        {
                            return dispatchInfo.SourceException;
                        }
                    }

                    var clrObject = ManagedType.GetManagedObject(value) as CLRObject;
                    if (clrObject?.inst is Exception e)
                    {
                        return e;
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
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }
        }

        private static ExceptionDispatchInfo TryGetDispatchInfo(BorrowedReference exception)
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

        /// <summary>
        /// Requires lock to be acquired elsewhere
        /// </summary>
        private static Exception FromPyErr(BorrowedReference typeHandle, BorrowedReference valueHandle, BorrowedReference tracebackHandle)
        {
            Exception inner = null;

            var exceptionDispatchInfo = TryGetDispatchInfo(valueHandle);
            if (exceptionDispatchInfo != null)
            {
                return exceptionDispatchInfo.SourceException;
            }

            if (valueHandle != null
                && ManagedType.GetManagedObject(valueHandle) is CLRObject { inst: Exception e })
            {
                return e;
            }

            var type = PyType.FromNullableReference(typeHandle);
            var value = PyObject.FromNullableReference(valueHandle);
            var traceback = PyObject.FromNullableReference(tracebackHandle);

            if (type != null && value != null)
            {
                if (PyObjectConversions.TryDecode(valueHandle, typeHandle, typeof(Exception), out object decoded)
                    && decoded is Exception decodedException)
                {
                    return decodedException;
                }

                var raw = new PythonException(type, value, traceback);
                raw.Normalize();

                using var cause = Runtime.PyException_GetCause(raw.Value.Reference);
                if (!cause.IsNull() && !cause.IsNone())
                {
                    using var innerTraceback = Runtime.PyException_GetTraceback(cause);
                    inner = FromPyErr(
                        typeHandle: Runtime.PyObject_TYPE(cause),
                        valueHandle: cause,
                        tracebackHandle: innerTraceback);
                }
            }

            return new PythonException(type, value, traceback, inner);
        }

        private string GetMessage() => GetMessage(Value, Type);

        private static string GetMessage(PyObject value, PyType type)
        {
            using var _ = new Py.GILState();
            if (value != null && !value.IsNone())
            {
                return value.ToString();
            }

            if (type != null)
            {
                return type.Name;
            }

            throw new ArgumentException("One of the values must not be null");
        }

        private static string TracebackToString(PyObject traceback)
        {
            if (traceback is null)
            {
                throw new ArgumentNullException(nameof(traceback));
            }

            using var tracebackModule = PyModule.Import("traceback");
            using var stackLines = new PyList(tracebackModule.InvokeMethod("format_tb", traceback));
            stackLines.Reverse();
            var result = new StringBuilder();
            foreach (PyObject stackLine in stackLines)
            {
                result.Append(stackLine);
            }
            return result.ToString();
        }

        /// <summary>Restores python error.</summary>
        public void Restore()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.PyErr_Restore(
                Type.NewReferenceOrNull().Steal(),
                Value.NewReferenceOrNull().Steal(),
                Traceback.NewReferenceOrNull().Steal());
            PythonEngine.ReleaseLock(gs);
        }

        /// <summary>
        /// Returns the exception type as a Python object.
        /// </summary>
        public PyType Type { get; private set; }

        /// <summary>
        /// Returns the exception value as a Python object.
        /// </summary>
        /// <seealso cref="Normalize"/>
        public PyObject Value { get; private set; }

        /// <remarks>
        /// Returns the TraceBack as a Python object.
        /// </remarks>
        public PyObject Traceback { get; }

        /// <summary>
        /// StackTrace Property
        /// </summary>
        /// <remarks>
        /// A string representing the python exception stack trace.
        /// </remarks>
        public override string StackTrace
        {
            get
            {
                if (Traceback is null) return base.StackTrace;

                using var _ = new Py.GILState();
                return TracebackToString(Traceback) + base.StackTrace;
            }
        }

        public override string Message => GetMessage();

        /// <summary>
        /// Replaces Value with an instance of Type, if Value is not already an instance of Type.
        /// </summary>
        public void Normalize()
        {
            IntPtr gs = PythonEngine.AcquireLock();
            try
            {
                if (Exceptions.ErrorOccurred()) throw new InvalidOperationException("Cannot normalize when an error is set");
                // If an error is set and this PythonException is unnormalized, the error will be cleared and the PythonException will be replaced by a different error.
                NewReference value = Value.NewReferenceOrNull();
                NewReference type = Type.NewReferenceOrNull();
                NewReference tb = Traceback.NewReferenceOrNull();
                Runtime.PyErr_NormalizeException(type: ref type, val: ref value, tb: ref tb);
                Value = value.MoveToPyObject();
                Type = new PyType(type.Steal());
                if (!tb.IsNull())
                {
                    int r = Runtime.PyException_SetTraceback(Value.Reference, tb);
                    ThrowIfIsNotZero(r);
                }
                tb.Dispose();
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
                var copy = Clone();
                copy.Normalize();

                if (copy.Traceback != null && copy.Type != null && copy.Value != null)
                {
                    using var traceback = PyModule.Import("traceback");
                    var buffer = new StringBuilder();
                    var values = traceback.InvokeMethod("format_exception", copy.Type, copy.Value, copy.Traceback);
                    foreach (PyObject val in values)
                    {
                        buffer.Append(val);
                    }
                    res = buffer.ToString();
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

        public PythonException Clone()
            => new PythonException(Type, Value, Traceback, InnerException);

        internal bool Is(IntPtr type)
        {
            return Runtime.PyErr_GivenExceptionMatches(
                (Value ?? Type).Reference,
                new BorrowedReference(type)) != 0;
        }

        /// <summary>
        /// Returns <c>true</c> if the current Python exception
        /// matches the given exception type.
        /// </summary>
        internal static bool CurrentMatches(IntPtr ob)
        {
            return Runtime.PyErr_ExceptionMatches(ob) != 0;
        }

        internal static BorrowedReference ThrowIfIsNull(BorrowedReference ob)
        {
            if (ob == null)
            {
                throw ThrowLastAsClrException();
            }

            return ob;
        }

        public static IntPtr ThrowIfIsNull(IntPtr ob)
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
