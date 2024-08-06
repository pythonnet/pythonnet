using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Python.Runtime
{
    public class Finalizer
    {
        public class CollectArgs : EventArgs
        {
            public int ObjectCount { get; set; }
        }

        public class ErrorArgs : EventArgs
        {
            public ErrorArgs(Exception error)
            {
                Error = error ?? throw new ArgumentNullException(nameof(error));
            }
            public bool Handled { get; set; }
            public Exception Error { get; }
        }

        public static Finalizer Instance { get; } = new ();

        public event EventHandler<CollectArgs>? BeforeCollect;
        public event EventHandler<ErrorArgs>? ErrorHandler;

        const int DefaultThreshold = 200;
        [DefaultValue(DefaultThreshold)]
        public int Threshold { get; set; } = DefaultThreshold;

        bool started;

        [DefaultValue(true)]
        public bool Enable { get; set; } = true;

        private readonly ConcurrentQueue<PendingFinalization> _objQueue = new();
        private readonly ConcurrentQueue<PendingFinalization> _derivedQueue = new();
        private readonly ConcurrentQueue<Py_buffer> _bufferQueue = new();
        private int _throttled;

        #region FINALIZER_CHECK

#if FINALIZER_CHECK
        private readonly object _queueLock = new object();
        internal bool RefCountValidationEnabled { get; set; } = true;
#else
        internal bool RefCountValidationEnabled { get; set; } = false;
#endif
        // Keep these declarations for compat even no FINALIZER_CHECK
        internal class IncorrectFinalizeArgs : EventArgs
        {
            public IncorrectFinalizeArgs(IntPtr handle, IReadOnlyCollection<IntPtr> imacted)
            {
                Handle = handle;
                ImpactedObjects = imacted;
            }
            public IntPtr Handle { get; }
            public BorrowedReference Reference => new(Handle);
            public IReadOnlyCollection<IntPtr> ImpactedObjects { get; }
        }

        internal class IncorrectRefCountException : Exception
        {
            public IntPtr PyPtr { get; internal set; }
            string? message;
            public override string Message
            {
                get
                {
                    if (message is not null) return message;
                    var gil = PythonEngine.AcquireLock();
                    try
                    {
                        using var pyname = Runtime.PyObject_Str(new BorrowedReference(PyPtr));
                        string name = Runtime.GetManagedString(pyname.BorrowOrThrow()) ?? Util.BadStr;
                        message = $"<{name}> may has a incorrect ref count";
                    }
                    finally
                    {
                        PythonEngine.ReleaseLock(gil);
                    }
                    return message;
                }
            }

            internal IncorrectRefCountException(IntPtr ptr)
            {
                PyPtr = ptr;
                
            }
        }

        internal delegate bool IncorrectRefCntHandler(object sender, IncorrectFinalizeArgs e);
        #pragma warning disable 414
        internal event IncorrectRefCntHandler? IncorrectRefCntResolver = null;
        #pragma warning restore 414
        internal bool ThrowIfUnhandleIncorrectRefCount { get; set; } = true;

        #endregion

        [ForbidPythonThreads]
        public void Collect() => this.DisposeAll();

        internal void ThrottledCollect()
        {
            if (!started) throw new InvalidOperationException($"{nameof(PythonEngine)} is not initialized");

            _throttled = unchecked(this._throttled + 1);
            if (!started || !Enable || _throttled < Threshold) return;
            _throttled = 0;
            this.Collect();
        }

        internal List<IntPtr> GetCollectedObjects()
        {
            return _objQueue.Select(o => o.PyObj).ToList();
        }

        internal void AddFinalizedObject(ref IntPtr obj, int run
#if TRACE_ALLOC
                                         , StackTrace stackTrace
#endif
        )
        {
            Debug.Assert(obj != IntPtr.Zero);
            if (!Enable)
            {
                return;
            }

            Debug.Assert(Runtime.Refcount(new BorrowedReference(obj)) > 0);

#if FINALIZER_CHECK
            lock (_queueLock)
#endif
            {
                this._objQueue.Enqueue(new PendingFinalization {
                    PyObj = obj, RuntimeRun = run,
#if TRACE_ALLOC
                    StackTrace = stackTrace.ToString(),
#endif
                });
            }
            obj = IntPtr.Zero;
        }

        internal void AddDerivedFinalizedObject(ref IntPtr derived, int run)
        {
            if (derived == IntPtr.Zero)
                throw new ArgumentNullException(nameof(derived));

            if (!Enable)
            {
                return;
            }

            var pending = new PendingFinalization { PyObj = derived, RuntimeRun = run };
            derived = IntPtr.Zero;
            _derivedQueue.Enqueue(pending);
        }

        internal void AddFinalizedBuffer(ref Py_buffer buffer)
        {
            if (buffer.obj == IntPtr.Zero)
                throw new ArgumentNullException(nameof(buffer));

            if (!Enable)
                return;

            var pending = buffer;
            buffer = default;
            _bufferQueue.Enqueue(pending);
        }

        internal static void Initialize()
        {
            Instance.started = true;
        }

        internal static void Shutdown()
        {
            Instance.DisposeAll();
            Instance.started = false;
        }

        internal nint DisposeAll(bool disposeObj = true, bool disposeDerived = true, bool disposeBuffer = true)
        {
            if (_objQueue.IsEmpty && _derivedQueue.IsEmpty && _bufferQueue.IsEmpty)
                return 0;

            nint collected = 0;

            BeforeCollect?.Invoke(this, new CollectArgs()
            {
                ObjectCount = _objQueue.Count
            });
#if FINALIZER_CHECK
            lock (_queueLock)
#endif
            {
#if FINALIZER_CHECK
                ValidateRefCount();
#endif
                Runtime.PyErr_Fetch(out var errType, out var errVal, out var traceback);
                Debug.Assert(errType.IsNull());

                int run = Runtime.GetRun();

                try
                {
                    if (disposeObj) while (!_objQueue.IsEmpty)
                    {
                        if (!_objQueue.TryDequeue(out var obj))
                            continue;

                        if (obj.RuntimeRun != run)
                        {
                            HandleFinalizationException(obj.PyObj, new RuntimeShutdownException(obj.PyObj));
                            continue;
                        }

                        IntPtr copyForException = obj.PyObj;
                        Runtime.XDecref(StolenReference.Take(ref obj.PyObj));
                        collected++;
                        try
                        {
                            Runtime.CheckExceptionOccurred();
                        }
                        catch (Exception e)
                        {
                            HandleFinalizationException(obj.PyObj, e);
                        }
                    }

                    if (disposeDerived) while (!_derivedQueue.IsEmpty)
                    {
                        if (!_derivedQueue.TryDequeue(out var derived))
                            continue;

                        if (derived.RuntimeRun != run)
                        {
                            HandleFinalizationException(derived.PyObj, new RuntimeShutdownException(derived.PyObj));
                            continue;
                        }

#pragma warning disable CS0618 // Type or member is obsolete. OK for internal use
                        PythonDerivedType.Finalize(derived.PyObj);
#pragma warning restore CS0618 // Type or member is obsolete

                        collected++;
                    }

                    if (disposeBuffer) while (!_bufferQueue.IsEmpty)
                    {
                        if (!_bufferQueue.TryDequeue(out var buffer))
                            continue;

                        Runtime.PyBuffer_Release(ref buffer);
                        collected++;
                    }
                }
                finally
                {
                    // Python requires finalizers to preserve exception:
                    // https://docs.python.org/3/extending/newtypes.html#finalization-and-de-allocation
                    Runtime.PyErr_Restore(errType.StealNullable(), errVal.StealNullable(), traceback.StealNullable());
                }
            }
            return collected;
        }

        void HandleFinalizationException(IntPtr obj, Exception cause)
        {
            var errorArgs = new ErrorArgs(cause);

            ErrorHandler?.Invoke(this, errorArgs);

            if (!errorArgs.Handled)
            {
                throw new FinalizationException(
                    "Python object finalization failed",
                    disposable: obj, innerException: cause);
            }
        }

#if FINALIZER_CHECK
        private void ValidateRefCount()
        {
            if (!RefCountValidationEnabled)
            {
                return;
            }
            var counter = new Dictionary<IntPtr, long>();
            var holdRefs = new Dictionary<IntPtr, long>();
            var indexer = new Dictionary<IntPtr, List<IntPtr>>();
            foreach (var obj in _objQueue)
            {
                var handle = obj;
                if (!counter.ContainsKey(handle))
                {
                    counter[handle] = 0;
                }
                counter[handle]++;
                if (!holdRefs.ContainsKey(handle))
                {
                    holdRefs[handle] = Runtime.Refcount(handle);
                }
                List<IntPtr> objs;
                if (!indexer.TryGetValue(handle, out objs))
                {
                    objs = new List<IntPtr>();
                    indexer.Add(handle, objs);
                }
                objs.Add(obj);
            }
            foreach (var pair in counter)
            {
                IntPtr handle = pair.Key;
                long cnt = pair.Value;
                // Tracked handle's ref count is larger than the object's holds
                // it may take an unspecified behaviour if it decref in Dispose
                if (cnt > holdRefs[handle])
                {
                    var args = new IncorrectFinalizeArgs()
                    {
                        Handle = handle,
                        ImpactedObjects = indexer[handle]
                    };
                    bool handled = false;
                    if (IncorrectRefCntResolver != null)
                    {
                        var funcList = IncorrectRefCntResolver.GetInvocationList();
                        foreach (IncorrectRefCntHandler func in funcList)
                        {
                            if (func(this, args))
                            {
                                handled = true;
                                break;
                            }
                        }
                    }
                    if (!handled && ThrowIfUnhandleIncorrectRefCount)
                    {
                        throw new IncorrectRefCountException(handle);
                    }
                }
                // Make sure no other references for PyObjects after this method
                indexer[handle].Clear();
            }
            indexer.Clear();
        }
#endif
    }

    struct PendingFinalization
    {
        public IntPtr PyObj;
        public BorrowedReference Ref => new(PyObj);
        public ManagedType? Managed => ManagedType.GetManagedObject(Ref);
        public nint RefCount => Runtime.Refcount(Ref);
        public int RuntimeRun;
#if TRACE_ALLOC
        public string StackTrace;
#endif
    }

    public class FinalizationException : Exception
    {
        public IntPtr Handle { get; }

        /// <summary>
        /// Gets the object, whose finalization failed.
        ///
        /// <para>If this function crashes, you can also try <see cref="DebugGetObject"/>,
        /// which does not attempt to increase the object reference count.</para>
        /// </summary>
        public PyObject GetObject() => new(new BorrowedReference(this.Handle));
        /// <summary>
        /// Gets the object, whose finalization failed without incrementing
        /// its reference count. This should only ever be called during debugging.
        /// When the result is disposed or finalized, the program will crash.
        /// </summary>
        public PyObject DebugGetObject()
        {
            IntPtr dangerousNoIncRefCopy = this.Handle;
            return new(StolenReference.Take(ref dangerousNoIncRefCopy));
        }

        public FinalizationException(string message, IntPtr disposable, Exception innerException)
            : base(message, innerException)
        {
            if (disposable == IntPtr.Zero) throw new ArgumentNullException(nameof(disposable));
            this.Handle = disposable;
        }

        protected FinalizationException(string message, IntPtr disposable)
            : base(message)
        {
            if (disposable == IntPtr.Zero) throw new ArgumentNullException(nameof(disposable));
            this.Handle = disposable;
        }
    }

    public class RuntimeShutdownException : FinalizationException
    {
        public RuntimeShutdownException(IntPtr disposable)
            : base("Python runtime was shut down after this object was created." +
                   " It is an error to attempt to dispose or to continue using it even after restarting the runtime.", disposable)
        {
        }
    }
}
