#nullable enable
using System;
using System.Diagnostics;
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
        public PythonException(PyType type, PyObject? value, PyObject? traceback,
                               string message, Exception? innerException)
            : base(message, innerException)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Value = value;
            Traceback = traceback;
        }

        public PythonException(PyType type, PyObject? value, PyObject? traceback,
                                Exception? innerException)
            : this(type, value, traceback, GetMessage(value, type), innerException) { }

        public PythonException(PyType type, PyObject? value, PyObject? traceback)
            : this(type, value, traceback, innerException: null) { }

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

            var exception = FetchCurrentOrNull(out ExceptionDispatchInfo? dispatchInfo)
                            ?? throw new InvalidOperationException("No exception is set");
            dispatchInfo?.Throw();
            // when dispatchInfo is not null, this line will not be reached
            throw exception;
        }

        internal static PythonException? FetchCurrentOrNullRaw()
        {
            using var _ = new Py.GILState();

            Runtime.PyErr_Fetch(type: out var type, val: out var value, tb: out var traceback);

            if (type.IsNull())
            {
                Debug.Assert(value.IsNull());
                Debug.Assert(traceback.IsNull());
                return null;
            }

            return new PythonException(
                type: new PyType(type.Steal()),
                value: value.MoveToPyObjectOrNull(),
                traceback: traceback.MoveToPyObjectOrNull());
        }
        internal static PythonException FetchCurrentRaw()
            => FetchCurrentOrNullRaw()
               ?? throw new InvalidOperationException("No exception is set");

        internal static Exception? FetchCurrentOrNull(out ExceptionDispatchInfo? dispatchInfo)
        {
            dispatchInfo = null;

            // prevent potential interop errors in this method
            // from crashing process with undebuggable StackOverflowException
            RuntimeHelpers.EnsureSufficientExecutionStack();

            using var _ = new Py.GILState();
            Runtime.PyErr_Fetch(out var type, out var value, out var traceback);
            if (type.IsNull())
            {
                Debug.Assert(value.IsNull());
                Debug.Assert(traceback.IsNull());
                return null;
            }

            try
            {
                if (TryDecodePyErr(type, value, traceback) is { } pyErr)
                {
                    type.Dispose();
                    value.Dispose();
                    traceback.Dispose();
                    return pyErr;
                }
            }
            catch
            {
                type.Dispose();
                value.Dispose();
                traceback.Dispose();
                throw;
            }

            Runtime.PyErr_NormalizeException(type: ref type, val: ref value, tb: ref traceback);

            try
            {
                return FromPyErr(typeRef: type, valRef: value, tbRef: traceback, out dispatchInfo);
            }
            finally
            {
                type.Dispose();
                value.Dispose();
                traceback.Dispose();
            }
        }

        internal static Exception FetchCurrent()
            => FetchCurrentOrNull(out _)
               ?? throw new InvalidOperationException("No exception is set");

        private static ExceptionDispatchInfo? TryGetDispatchInfo(BorrowedReference exception)
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
                if (Converter.ToManagedValue(pyInfo, typeof(ExceptionDispatchInfo), out object? result, setError: false))
                {
                    return (ExceptionDispatchInfo)result!;
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
        private static Exception FromPyErr(BorrowedReference typeRef, BorrowedReference valRef, BorrowedReference tbRef,
                                           out ExceptionDispatchInfo? exceptionDispatchInfo)
        {
            if (valRef == null) throw new ArgumentNullException(nameof(valRef));

            var type = PyType.FromReference(typeRef);
            var value = new PyObject(valRef);
            var traceback = PyObject.FromNullableReference(tbRef);

            exceptionDispatchInfo = TryGetDispatchInfo(valRef);
            if (exceptionDispatchInfo != null)
            {
                return exceptionDispatchInfo.SourceException;
            }

            if (ManagedType.GetManagedObject(valRef) is CLRObject { inst: Exception e })
            {
                return e;
            }

            if (TryDecodePyErr(typeRef, valRef, tbRef) is { } pyErr)
            {
                return pyErr;
            }

            if (PyObjectConversions.TryDecode(valRef, typeRef, typeof(Exception), out object decoded)
                && decoded is Exception decodedException)
            {
                return decodedException;
            }

            using var cause = Runtime.PyException_GetCause(valRef);
            Exception? inner = FromCause(cause);
            return new PythonException(type, value, traceback, inner);
        }

        private static Exception? TryDecodePyErr(BorrowedReference typeRef, BorrowedReference valRef, BorrowedReference tbRef)
        {
            using var type = PyType.FromReference(typeRef);
            using var value = PyObject.FromNullableReference(valRef);
            using var traceback = PyObject.FromNullableReference(tbRef);

            using var errorDict = new PyDict();
            if (typeRef != null) errorDict["type"] = type;
            if (valRef != null) errorDict["value"] = value;
            if (tbRef != null) errorDict["traceback"] = traceback;

            using var pyErrType = Runtime.InteropModule.GetAttr("PyErr");
            using var pyErrInfo = pyErrType.Invoke(new PyTuple(), errorDict);
            if (PyObjectConversions.TryDecode(pyErrInfo.Reference, pyErrType.Reference,
                typeof(Exception), out object decoded) && decoded is Exception decodedPyErrInfo)
            {
                return decodedPyErrInfo;
            }

            return null;
        }

        private static Exception? FromCause(BorrowedReference cause)
        {
            if (cause == null || cause.IsNone()) return null;

            Debug.Assert(Runtime.PyObject_TypeCheck(cause, new BorrowedReference(Exceptions.BaseException)));

            using var innerTraceback = Runtime.PyException_GetTraceback(cause);
            return FromPyErr(
                typeRef: Runtime.PyObject_TYPE(cause),
                valRef: cause,
                tbRef: innerTraceback,
                out _);

        }

        private static string GetMessage(PyObject? value, PyType type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (value != null && !value.IsNone())
            {
                return value.ToString();
            }

            return type.Name;
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
                stackLine.Dispose();
            }
            return result.ToString();
        }

        /// <summary>Restores python error.</summary>
        public void Restore()
        {
            CheckRuntimeIsRunning();

            using var _ = new Py.GILState();

            NewReference type = Type.NewReferenceOrNull();
            NewReference value = Value.NewReferenceOrNull();
            NewReference traceback = Traceback.NewReferenceOrNull();

            Runtime.PyErr_Restore(
                type: type.Steal(),
                val: value.StealNullable(),
                tb: traceback.StealNullable());
        }

        /// <summary>
        /// Returns the exception type as a Python object.
        /// </summary>
        public PyType Type { get; private set; }

        /// <summary>
        /// Returns the exception value as a Python object.
        /// </summary>
        /// <seealso cref="Normalize"/>
        public PyObject? Value { get; private set; }

        /// <remarks>
        /// Returns the TraceBack as a Python object.
        /// </remarks>
        public PyObject? Traceback { get; }

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

                if (!PythonEngine.IsInitialized && Runtime.Py_IsInitialized() == 0)
                    return "Python stack unavailable as runtime was shut down\n" + base.StackTrace;

                using var _ = new Py.GILState();
                return TracebackToString(Traceback) + base.StackTrace;
            }
        }

        public bool IsNormalized
        {
            get
            {
                if (Value is null) return false;

                CheckRuntimeIsRunning();

                using var _ = new Py.GILState();
                return Runtime.PyObject_TypeCheck(Value.Reference, Type.Reference);
            }
        }

        /// <summary>
        /// Replaces Value with an instance of Type, if Value is not already an instance of Type.
        /// </summary>
        public void Normalize()
        {
            CheckRuntimeIsRunning();

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
                try
                {
                    Debug.Assert(Traceback is null == tb.IsNull());
                    if (!tb.IsNull())
                    {
                        Debug.Assert(Traceback!.Reference == tb);

                        int r = Runtime.PyException_SetTraceback(Value.Reference, tb);
                        ThrowIfIsNotZero(r);
                    }
                }
                finally
                {
                    tb.Dispose();
                }
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
            CheckRuntimeIsRunning();

            using var _ = new Py.GILState();

            var copy = Clone();
            copy.Normalize();

            if (copy.Traceback is null || copy.Value is null)
                return StackTrace;

            using var traceback = PyModule.Import("traceback");
            var buffer = new StringBuilder();
            using var values = traceback.InvokeMethod("format_exception", copy.Type, copy.Value, copy.Traceback);
            foreach (PyObject val in PyIter.GetIter(values))
            {
                buffer.Append(val);
                val.Dispose();
            }
            return buffer.ToString();

        }

        public PythonException Clone()
            => new PythonException(type: Type, value: Value, traceback: Traceback,
                                   Message, InnerException);

        internal bool Is(IntPtr type)
        {
            return Runtime.PyErr_GivenExceptionMatches(
                given: (Value ?? Type).Reference,
                typeOrTypes: new BorrowedReference(type)) != 0;
        }

        private static void CheckRuntimeIsRunning()
        {
            if (!PythonEngine.IsInitialized && Runtime.Py_IsInitialized() == 0)
                throw new InvalidOperationException("Python runtime must be running");
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
