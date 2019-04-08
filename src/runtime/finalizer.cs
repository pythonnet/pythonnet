using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct PendingArgs
        {
            public bool cancelled;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PendingCall(IntPtr arg);
        private readonly PendingCall _collectAction;

        private ConcurrentQueue<IPyDisposable> _objQueue = new ConcurrentQueue<IPyDisposable>();
        private bool _pending = false;
        private readonly object _collectingLock = new object();
        private IntPtr _pendingArgs = IntPtr.Zero;

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
            public ICollection<IPyDisposable> ImpactedObjects { get; internal set; }
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
                _message = $"{name} may has a incorrect ref count";
            }
        }

        public delegate bool IncorrectRefCntHandler(object sender, IncorrectFinalizeArgs e);
        public event IncorrectRefCntHandler IncorrectRefCntResolver;
        public bool ThrowIfUnhandleIncorrectRefCount { get; set; } = true;

        #endregion

        private Finalizer()
        {
            Enable = true;
            Threshold = 200;
            _collectAction = OnPendingCollect;
        }

        public void CallPendingFinalizers()
        {
            if (Thread.CurrentThread.ManagedThreadId != Runtime.MainManagedThreadId)
            {
                throw new Exception("PendingCall should execute in main Python thread");
            }
            Runtime.Py_MakePendingCalls();
        }

        public void Collect()
        {
            using (var gilState = new Py.GILState())
            {
                DisposeAll();
            }
        }

        public List<WeakReference> GetCollectedObjects()
        {
            return _objQueue.Select(T => new WeakReference(T)).ToList();
        }

        internal void AddFinalizedObject(IPyDisposable obj)
        {
            if (!Enable)
            {
                return;
            }
            if (Runtime.Py_IsInitialized() == 0)
            {
                // XXX: Memory will leak if a PyObject finalized after Python shutdown,
                // for avoiding that case, user should call GC.Collect manual before shutdown.
                return;
            }
#if FINALIZER_CHECK
            lock (_queueLock)
#endif
            {
                _objQueue.Enqueue(obj);
            }
            GC.ReRegisterForFinalize(obj);
            if (!_pending && _objQueue.Count >= Threshold)
            {
                AddPendingCollect();
            }
        }

        internal static void Shutdown()
        {
            if (Runtime.Py_IsInitialized() == 0)
            {
                Instance._objQueue = new ConcurrentQueue<IPyDisposable>();
                return;
            }
            Instance.DisposeAll();
            if (Thread.CurrentThread.ManagedThreadId != Runtime.MainManagedThreadId)
            {
                if (Instance._pendingArgs == IntPtr.Zero)
                {
                    Instance.ResetPending();
                    return;
                }
                // Not in main thread just cancel the pending operation to avoid error in different domain
                // It will make a memory leak
                unsafe
                {
                    PendingArgs* args = (PendingArgs*)Instance._pendingArgs;
                    args->cancelled = true;
                }
                Instance.ResetPending();
                return;
            }
            Instance.CallPendingFinalizers();
        }

        private void AddPendingCollect()
        {
            if(Monitor.TryEnter(_collectingLock))
            {
                try
                {
                    if (!_pending)
                    {
                        _pending = true;
                        var args = new PendingArgs { cancelled = false };
                        _pendingArgs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PendingArgs)));
                        Marshal.StructureToPtr(args, _pendingArgs, false);
                        IntPtr func = Marshal.GetFunctionPointerForDelegate(_collectAction);
                        if (Runtime.Py_AddPendingCall(func, _pendingArgs) != 0)
                        {
                            // Full queue, append next time
                            FreePendingArgs();
                            _pending = false;
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(_collectingLock);
                }
            }
        }

        private static int OnPendingCollect(IntPtr arg)
        {
            Debug.Assert(arg == Instance._pendingArgs);
            try
            {
                unsafe
                {
                    PendingArgs* pendingArgs = (PendingArgs*)arg;
                    if (pendingArgs->cancelled)
                    {
                        return 0;
                    }
                }
                Instance.DisposeAll();
            }
            finally
            {
                Instance.FreePendingArgs();
                Instance.ResetPending();
            }
            return 0;
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
                IPyDisposable obj;
                while (_objQueue.TryDequeue(out obj))
                {
                    try
                    {
                        obj.Dispose();
                        Runtime.CheckExceptionOccurred();
                    }
                    catch (Exception e)
                    {
                        // We should not bother the main thread
                        ErrorHandler?.Invoke(this, new ErrorArgs()
                        {
                            Error = e
                        });
                    }
                }
            }
        }

        private void FreePendingArgs()
        {
            if (_pendingArgs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pendingArgs);
                _pendingArgs = IntPtr.Zero;
            }
        }

        private void ResetPending()
        {
            lock (_collectingLock)
            {
                _pending = false;
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
            var indexer = new Dictionary<IntPtr, List<IPyDisposable>>();
            foreach (var obj in _objQueue)
            {
                IntPtr[] handles = obj.GetTrackedHandles();
                foreach (var handle in handles)
                {
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }
                    if (!counter.ContainsKey(handle))
                    {
                        counter[handle] = 0;
                    }
                    counter[handle]++;
                    if (!holdRefs.ContainsKey(handle))
                    {
                        holdRefs[handle] = Runtime.Refcount(handle);
                    }
                    List<IPyDisposable> objs;
                    if (!indexer.TryGetValue(handle, out objs))
                    {
                        objs = new List<IPyDisposable>();
                        indexer.Add(handle, objs);
                    }
                    objs.Add(obj);
                }
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
}
