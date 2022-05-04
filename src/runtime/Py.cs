namespace Python.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

using Python.Runtime.Native;

public static class Py
{
    public static GILState GIL() => PythonEngine.DebugGIL ? new DebugGILState() : new GILState();

    public static PyModule CreateScope() => new();
    public static PyModule CreateScope(string name)
        => new(name ?? throw new ArgumentNullException(nameof(name)));


    public class GILState : IDisposable
    {
        private readonly PyGILState state;
        private bool isDisposed;

        internal GILState()
        {
            state = PythonEngine.AcquireLock();
        }

        public virtual void Dispose()
        {
            if (this.isDisposed) return;

            PythonEngine.ReleaseLock(state);
            GC.SuppressFinalize(this);
            this.isDisposed = true;
        }

        ~GILState()
        {
            throw new InvalidOperationException("GIL must always be released, and it must be released from the same thread that acquired it.");
        }
    }

    public class DebugGILState : GILState
    {
        readonly Thread owner;
        internal DebugGILState() : base()
        {
            this.owner = Thread.CurrentThread;
        }
        public override void Dispose()
        {
            if (this.owner != Thread.CurrentThread)
                throw new InvalidOperationException("GIL must always be released from the same thread, that acquired it");

            base.Dispose();
        }
    }

    public class KeywordArguments : PyDict
    {
        public KeywordArguments() : base()
        {
        }

        protected KeywordArguments(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    public static KeywordArguments kw(params object?[] kv)
    {
        var dict = new KeywordArguments();
        if (kv.Length % 2 != 0)
        {
            throw new ArgumentException("Must have an equal number of keys and values");
        }
        for (var i = 0; i < kv.Length; i += 2)
        {
            if (kv[i] is not string key)
                throw new ArgumentException("Keys must be non-null strings");

            BorrowedReference value;
            NewReference temp = default;
            if (kv[i + 1] is PyObject pyObj)
            {
                value = pyObj;
            }
            else
            {
                temp = Converter.ToPythonDetectType(kv[i + 1]);
                value = temp.Borrow();
            }
            using (temp)
            {
                if (Runtime.PyDict_SetItemString(dict, key, value) != 0)
                {
                    throw new ArgumentException(
                        string.Format("Cannot add key '{0}' to dictionary.", key),
                        innerException: PythonException.FetchCurrent());
                }
            }
        }
        return dict;
    }

    /// <summary>
    /// Given a module or package name, import the module and return the resulting object.
    /// </summary>
    /// <param name="name">Fully-qualified module or package name</param>
    public static PyObject Import(string name) => PyModule.Import(name);

    public static void SetArgv()
    {
        IEnumerable<string> args;
        try
        {
            args = Environment.GetCommandLineArgs();
        }
        catch (NotSupportedException)
        {
            args = Enumerable.Empty<string>();
        }

        SetArgv(new[] { "" }.Concat(args.Skip(1)));
    }

    public static void SetArgv(params string[] argv)
    {
        SetArgv(argv as IEnumerable<string>);
    }

    public static void SetArgv(IEnumerable<string> argv)
    {
        if (argv is null) throw new ArgumentNullException(nameof(argv));

        using (GIL())
        {
            string[] arr = argv.ToArray();
            Runtime.PySys_SetArgvEx(arr.Length, arr, 0);
            Runtime.CheckExceptionOccurred();
        }
    }

    public static void With(PyObject obj, Action<PyObject> Body)
    {
        if (obj is null) throw new ArgumentNullException(nameof(obj));
        if (Body is null) throw new ArgumentNullException(nameof(Body));

        // Behavior described here:
        // https://docs.python.org/2/reference/datamodel.html#with-statement-context-managers

        Exception? ex = null;
        PythonException? pyError = null;

        try
        {
            PyObject enterResult = obj.InvokeMethod("__enter__");

            Body(enterResult);
        }
        catch (PythonException e)
        {
            ex = pyError = e;
        }
        catch (Exception e)
        {
            ex = e;
            Exceptions.SetError(e);
            pyError = PythonException.FetchCurrentRaw();
        }

        PyObject type = pyError?.Type ?? PyObject.None;
        PyObject val = pyError?.Value ?? PyObject.None;
        PyObject traceBack = pyError?.Traceback ?? PyObject.None;

        var exitResult = obj.InvokeMethod("__exit__", type, val, traceBack);

        if (ex != null && !exitResult.IsTrue()) throw ex;
    }

    public static void With(PyObject obj, Action<dynamic> Body)
        => With(obj, (PyObject context) => Body(context));
}
