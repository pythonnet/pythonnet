using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Python.Runtime
{

    /// <summary>
    /// Encapsulates the Python exception APIs.
    /// </summary>
    /// <remarks>
    /// Readability of the Exceptions class improvements as we look toward version 2.7 ...
    /// </remarks>
    internal static class Exceptions
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // set in Initialize
        internal static PyObject warnings_module;
        internal static PyObject exceptions_module;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// Initialization performed on startup of the Python runtime.
        /// </summary>
        internal static void Initialize()
        {
            string exceptionsModuleName = "builtins";
            exceptions_module = PyModule.Import(exceptionsModuleName);
            warnings_module = PyModule.Import("warnings");
            Type type = typeof(Exceptions);
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                using var op = Runtime.PyObject_GetAttrString(exceptions_module.obj, fi.Name);
                if (!@op.IsNull())
                {
                    fi.SetValue(type, op.MoveToPyObject());
                }
                else
                {
                    fi.SetValue(type, null);
                    DebugUtil.Print($"Unknown exception: {fi.Name}");
                }
            }
            Runtime.PyErr_Clear();
        }


        /// <summary>
        /// Cleanup resources upon shutdown of the Python runtime.
        /// </summary>
        internal static void Shutdown()
        {
            if (Runtime.Py_IsInitialized() == 0)
            {
                return;
            }
            Type type = typeof(Exceptions);
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var op = (PyObject?)fi.GetValue(type);
                if (op is null)
                {
                    continue;
                }
                op.Dispose();
                fi.SetValue(null, null);
            }
            exceptions_module.Dispose();
            warnings_module.Dispose();
        }

        /// <summary>
        /// Set the 'args' slot on a python exception object that wraps
        /// a CLR exception. This is needed for pickling CLR exceptions as
        /// BaseException_reduce will only check the slots, bypassing the
        /// __getattr__ implementation, and thus dereferencing a NULL
        /// pointer.
        /// </summary>
        internal static bool SetArgsAndCause(BorrowedReference ob, Exception e)
        {
            NewReference args;
            if (!string.IsNullOrEmpty(e.Message))
            {
                args = Runtime.PyTuple_New(1);
                using var msg = Runtime.PyString_FromString(e.Message);
                Runtime.PyTuple_SetItem(args.Borrow(), 0, msg.StealOrThrow());
            }
            else
            {
                args = Runtime.PyTuple_New(0);
            }

            using (args)
            {
                if (Runtime.PyObject_SetAttrString(ob, "args", args.Borrow()) != 0)
                {
                    return false;
                }
            }

            if (e.InnerException != null)
            {
                // Note: For an AggregateException, InnerException is only the first of the InnerExceptions.
                using var cause = CLRObject.GetReference(e.InnerException);
                Runtime.PyException_SetCause(ob, cause.Steal());
            }

            return true;
        }

        /// <summary>
        /// Shortcut for (pointer == NULL) -&gt; throw PythonException
        /// </summary>
        /// <param name="pointer">Pointer to a Python object</param>
        internal static BorrowedReference ErrorCheck(BorrowedReference pointer)
        {
            if (pointer.IsNull)
            {
                throw PythonException.ThrowLastAsClrException();
            }

            return pointer;
        }

        internal static void ErrorCheck(IntPtr pointer) => ErrorCheck(new BorrowedReference(pointer));

        /// <summary>
        /// Shortcut for (pointer == NULL or ErrorOccurred()) -&gt; throw PythonException
        /// </summary>
        internal static void ErrorOccurredCheck(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero || ErrorOccurred())
            {
                throw PythonException.ThrowLastAsClrException();
            }
        }

        internal static IntPtr ErrorCheckIfNull(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero && ErrorOccurred())
            {
                throw PythonException.ThrowLastAsClrException();
            }
            return pointer;
        }

        /// <summary>
        /// ExceptionMatches Method
        /// </summary>
        /// <remarks>
        /// Returns true if the current Python exception matches the given
        /// Python object. This is a wrapper for PyErr_ExceptionMatches.
        /// </remarks>
        public static bool ExceptionMatches(BorrowedReference ob)
        {
            return Runtime.PyErr_ExceptionMatches(ob) != 0;
        }

        /// <summary>
        /// Sets the current Python exception given a native string.
        /// This is a wrapper for the Python PyErr_SetString call.
        /// </summary>
        public static void SetError(BorrowedReference type, string message)
        {
            Runtime.PyErr_SetString(type, message);
        }

        /// <summary>
        /// SetError Method
        /// </summary>
        /// <remarks>
        /// Sets the current Python exception given a Python object.
        /// This is a wrapper for the Python PyErr_SetObject call.
        /// </remarks>
        public static void SetError(BorrowedReference type, BorrowedReference exceptionObject)
        {
            Runtime.PyErr_SetObject(type, exceptionObject);
        }

        internal const string DispatchInfoAttribute = "__dispatch_info__";
        /// <summary>
        /// SetError Method
        /// </summary>
        /// <remarks>
        /// Sets the current Python exception given a CLR exception
        /// object. The CLR exception instance is wrapped as a Python
        /// object, allowing it to be handled naturally from Python.
        /// </remarks>
        public static bool SetError(Exception e)
        {
            Debug.Assert(e is not null);

            // Because delegates allow arbitrary nesting of Python calling
            // managed calling Python calling... etc. it is possible that we
            // might get a managed exception raised that is a wrapper for a
            // Python exception. In that case we'd rather have the real thing.

            if (e is PythonException pe)
            {
                pe.Restore();
                return true;
            }

            using var instance = Converter.ToPython(e);
            if (instance.IsNull()) return false;

            var exceptionInfo = ExceptionDispatchInfo.Capture(e);
            using var pyInfo = Converter.ToPython(exceptionInfo);

            if (Runtime.PyObject_SetAttrString(instance.Borrow(), DispatchInfoAttribute, pyInfo.Borrow()) != 0)
                return false;

            Debug.Assert(Runtime.PyObject_TypeCheck(instance.Borrow(), BaseException));

            var type = Runtime.PyObject_TYPE(instance.Borrow());
            Runtime.PyErr_SetObject(type, instance.Borrow());
            return true;
        }

        /// <summary>
        /// When called after SetError, sets the cause of the error.
        /// </summary>
        /// <param name="cause">The cause of the current error</param>
        public static void SetCause(Exception cause)
        {
            var currentException = PythonException.FetchCurrentRaw();
            currentException.Normalize();
            using var causeInstance = Converter.ToPython(cause);
            Runtime.PyException_SetCause(currentException.Value!.Reference, causeInstance.Steal());
            currentException.Restore();
        }

        /// <summary>
        /// ErrorOccurred Method
        /// </summary>
        /// <remarks>
        /// Returns true if an exception occurred in the Python runtime.
        /// This is a wrapper for the Python PyErr_Occurred call.
        /// </remarks>
        public static bool ErrorOccurred()
        {
            return Runtime.PyErr_Occurred() != null;
        }

        /// <summary>
        /// Clear Method
        /// </summary>
        /// <remarks>
        /// Clear any exception that has been set in the Python runtime.
        /// </remarks>
        public static void Clear()
        {
            Runtime.PyErr_Clear();
        }

        //====================================================================
        // helper methods for raising warnings
        //====================================================================

        /// <summary>
        /// Alias for Python's warnings.warn() function.
        /// </summary>
        public static void warn(string message, BorrowedReference exception, int stacklevel)
        {
            if (exception == null ||
                (Runtime.PyObject_IsSubclass(exception, Exceptions.Warning) != 1))
            {
                Exceptions.RaiseTypeError("Invalid exception");
            }

            using var warn = Runtime.PyObject_GetAttrString(warnings_module.obj, "warn");
            warn.BorrowOrThrow();

            using var argsTemp = Runtime.PyTuple_New(3);
            BorrowedReference args = argsTemp.BorrowOrThrow();

            using var msg = Runtime.PyString_FromString(message);
            Runtime.PyTuple_SetItem(args, 0, msg.StealOrThrow());
            Runtime.PyTuple_SetItem(args, 1, exception);

            using var level = Runtime.PyInt_FromInt32(stacklevel);
            Runtime.PyTuple_SetItem(args, 2, level.StealOrThrow());

            using var result = Runtime.PyObject_CallObject(warn.Borrow(), args);
            result.BorrowOrThrow();
        }

        public static void warn(string message, BorrowedReference exception)
        {
            warn(message, exception, 1);
        }

        public static void deprecation(string message, int stacklevel)
        {
            warn(message, Exceptions.DeprecationWarning, stacklevel);
        }

        public static void deprecation(string message)
        {
            deprecation(message, 1);
        }

        //====================================================================
        // Internal helper methods for common error handling scenarios.
        //====================================================================

        /// <summary>
        /// Raises a <see cref="TypeError"/> and attaches any existing exception as its cause.
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <returns><c>null</c></returns>
        internal static NewReference RaiseTypeError(string message)
        {
            var cause = PythonException.FetchCurrentOrNullRaw();
            cause?.Normalize();

            Exceptions.SetError(Exceptions.TypeError, message);

            if (cause is null) return default;

            var typeError = PythonException.FetchCurrentRaw();
            typeError.Normalize();

            Runtime.PyException_SetCause(
                typeError.Value!,
                new NewReference(cause.Value!).Steal());
            typeError.Restore();

            return default;
        }

        // 2010-11-16: Arranged in python (2.6 & 2.7) source header file order
        /* Predefined exceptions are
           public static variables on the Exceptions class filled in from
           the python class using reflection in Initialize() looked up by
		   name, not position. */
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // set in Initialize
        public static PyObject BaseException;
        public static PyObject Exception;
        public static PyObject StopIteration;
        public static PyObject GeneratorExit;
        public static PyObject ArithmeticError;
        public static PyObject LookupError;

        public static PyObject AssertionError;
        public static PyObject AttributeError;
        public static PyObject BufferError;
        public static PyObject EOFError;
        public static PyObject FloatingPointError;
        public static PyObject EnvironmentError;
        public static PyObject IOError;
        public static PyObject OSError;
        public static PyObject ImportError;
        public static PyObject ModuleNotFoundError;
        public static PyObject IndexError;
        public static PyObject KeyError;
        public static PyObject KeyboardInterrupt;
        public static PyObject MemoryError;
        public static PyObject NameError;
        public static PyObject OverflowError;
        public static PyObject RuntimeError;
        public static PyObject NotImplementedError;
        public static PyObject SyntaxError;
        public static PyObject IndentationError;
        public static PyObject TabError;
        public static PyObject ReferenceError;
        public static PyObject SystemError;
        public static PyObject SystemExit;
        public static PyObject TypeError;
        public static PyObject UnboundLocalError;
        public static PyObject UnicodeError;
        public static PyObject UnicodeEncodeError;
        public static PyObject UnicodeDecodeError;
        public static PyObject UnicodeTranslateError;
        public static PyObject ValueError;
        public static PyObject ZeroDivisionError;
//#ifdef MS_WINDOWS
        //public static IntPtr WindowsError;
//#endif
//#ifdef __VMS
        //public static IntPtr VMSError;
//#endif

        //PyAPI_DATA(PyObject *) PyExc_BufferError;

        //PyAPI_DATA(PyObject *) PyExc_MemoryErrorInst;
        //PyAPI_DATA(PyObject *) PyExc_RecursionErrorInst;


        /* Predefined warning categories */
        public static PyObject Warning;
        public static PyObject UserWarning;
        public static PyObject DeprecationWarning;
        public static PyObject PendingDeprecationWarning;
        public static PyObject SyntaxWarning;
        public static PyObject RuntimeWarning;
        public static PyObject FutureWarning;
        public static PyObject ImportWarning;
        public static PyObject UnicodeWarning;
        //PyAPI_DATA(PyObject *) PyExc_BytesWarning;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }
}
