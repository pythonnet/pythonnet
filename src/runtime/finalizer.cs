using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
            public Exception Error { get; set; }
        }

        public static readonly Finalizer Instance = new Finalizer();

        public event EventHandler<CollectArgs> CollectOnce;
        public event EventHandler<ErrorArgs> ErrorHandler;

        public int Threshold { get; set; }
        public bool Enable { get; set; }

        private ConcurrentQueue<IntPtr> _objQueue = new ConcurrentQueue<IntPtr>();
        private int _throttled;

        #region FINALIZER_CHECK

#if FINALIZER_CHECK
        private readonly object _queueLock = new object();
        public bool RefCountValidationEnabled { get; set; } = true;
#else
        public readonly bool RefCountValidationEnabled = false;
#endif
        // Keep these declarations for compat even no FINALIZER_CHECK
        public class IncorrectFinalizeArgs : EventArgs
        {
            public IntPtr Handle { get; internal set; }
            public ICollection<IntPtr> ImpactedObjects { get; internal set; }
        }

        public class IncorrectRefCountException : Exception
        {
            public IntPtr PyPtr { get; internal set; }
            private string _message;
            public override string Message => _message;

            public IncorrectRefCountException(IntPtr ptr)
            {
                PyPtr = ptr;
                IntPtr pyname = Runtime.PyObject_Unicode(PyPtr);
                string name = Runtime.GetManagedString(pyname);
                Runtime.XDecref(pyname);
                _message = $"<{name}> may has a incorrect ref count";
            }
        }

        public delegate bool IncorrectRefCntHandler(object sender, IncorrectFinalizeArgs e);
        #pragma warning disable 414
        public event IncorrectRefCntHandler IncorrectRefCntResolver = null;
        #pragma warning restore 414
        public bool ThrowIfUnhandleIncorrectRefCount { get; set; } = true;

        #endregion

        private Finalizer()
        {
            Enable = true;
            Threshold = 200;
        }

        public void Collect() => this.DisposeAll();

        internal void ThrottledCollect()
        {
            _throttled = unchecked(this._throttled + 1);
            if (!Enable || _throttled < Threshold) return;
            _throttled = 0;
            this.Collect();
        }

        internal List<IntPtr> GetCollectedObjects()
        {
            return _objQueue.ToList();
        }

        internal void AddFinalizedObject(ref IntPtr obj)
        {
            if (!Enable || obj == IntPtr.Zero)
            {
                return;
            }

#if FINALIZER_CHECK
            lock (_queueLock)
#endif
            {
                this._objQueue.Enqueue(obj);
            }
            obj = IntPtr.Zero;
        }

        internal static void Shutdown()
        {
            Instance.DisposeAll();
        }

        private void DisposeAll()
        {
            CollectOnce?.Invoke(this, new CollectArgs()
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
                IntPtr obj;
                Runtime.PyErr_Fetch(out var errType, out var errVal, out var traceback);

                try
                {
                    while (!_objQueue.IsEmpty)
                    {
                        if (!_objQueue.TryDequeue(out obj))
                            continue;

                        Runtime.XDecref(obj);
                        try
                        {
                            Runtime.CheckExceptionOccurred();
                        }
                        catch (Exception e)
                        {
                            var handler = ErrorHandler;
                            if (handler is null)
                            {
                                throw new FinalizationException(
                                    "Python object finalization failed",
                                    disposable: obj, innerException: e);
                            }

                            handler.Invoke(this, new ErrorArgs()
                            {
                                Error = e
                            });
                        }
                    }
                }
                finally
                {
                    // Python requires finalizers to preserve exception:
                    // https://docs.python.org/3/extending/newtypes.html#finalization-and-de-allocation
                    Runtime.PyErr_Restore(errType, errVal, traceback);
                }
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

    public class FinalizationException : Exception
    {
        public IntPtr PythonObject { get; }

        public FinalizationException(string message, IntPtr disposable, Exception innerException)
            : base(message, innerException)
        {
            if (disposable == IntPtr.Zero) throw new ArgumentNullException(nameof(disposable));
            this.PythonObject = disposable;
        }
    }
}
