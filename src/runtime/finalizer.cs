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

        private ConcurrentQueue<IDisposable> _objQueue = new ConcurrentQueue<IDisposable>();
        private bool _pending = false;
        private readonly object _collectingLock = new object();
        private IntPtr _pendingArgs;

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

        internal void AddFinalizedObject(IDisposable obj)
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
            _objQueue.Enqueue(obj);
            GC.ReRegisterForFinalize(obj);
            if (_objQueue.Count >= Threshold)
            {
                AddPendingCollect();
            }
        }

        internal static void Shutdown()
        {
            if (Runtime.Py_IsInitialized() == 0)
            {
                Instance._objQueue = new ConcurrentQueue<IDisposable>();
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
            if (_pending)
            {
                return;
            }
            lock (_collectingLock)
            {
                if (_pending)
                {
                    return;
                }
                _pending = true;
                var args = new PendingArgs() { cancelled = false };
                IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PendingArgs)));
                Marshal.StructureToPtr(args, p, false);
                _pendingArgs = p;
                IntPtr func = Marshal.GetFunctionPointerForDelegate(_collectAction);
                if (Runtime.Py_AddPendingCall(func, p) != 0)
                {
                    // Full queue, append next time
                    _pending = false;
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
                Instance.ResetPending();
                Marshal.FreeHGlobal(arg);
            }
            return 0;
        }

        private void DisposeAll()
        {
            CollectOnce?.Invoke(this, new CollectArgs()
            {
                ObjectCount = _objQueue.Count
            });
            IDisposable obj;
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

        private void ResetPending()
        {
            lock (_collectingLock)
            {
                _pending = false;
                _pendingArgs = IntPtr.Zero;
            }
        }
    }
}
