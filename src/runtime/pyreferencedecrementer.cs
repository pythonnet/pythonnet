using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Python.Runtime
{
    internal class PyReferenceDecrementer : IDisposable
    {
        private static readonly DedicatedThreadTaskScheduler DedicatedThreadTaskScheduler = new DedicatedThreadTaskScheduler();

        private readonly BlockingCollection<IntPtr> _asyncDecRefQueue = new BlockingCollection<IntPtr>();

        private CancellationTokenSource _cts;
        private CancellationToken _ct;
        private Task _backgroundWorkerTask;

        public PyReferenceDecrementer()
        {
            InitDecRefThread();
        }

        public void ScheduleDecRef(IntPtr pyRef)
        {
            // ReSharper disable once MethodSupportsCancellation
            _asyncDecRefQueue.Add(pyRef);
        }

        internal void WaitForPendingDecReferences()
        {
            ShutdownDecRefThread();
            InitDecRefThread();
        }

        private void ShutdownDecRefThread()
        {
            _cts?.Cancel();
            try
            {
                IntPtr ts = IntPtr.Zero;
                if (Runtime.PyGILState_GetThisThreadState() != IntPtr.Zero)
                {
                    ts = Runtime.PyEval_SaveThread();
                }
                try
                {
                    // ReSharper disable once MethodSupportsCancellation
                    _backgroundWorkerTask.Wait();
                }
                catch (AggregateException ex)
                {
                    if (!(ex.InnerException is OperationCanceledException))
                    {
                        throw;
                    }
                }
                finally
                {
                    if (ts != IntPtr.Zero)
                    {
                        Runtime.PyEval_RestoreThread(ts);
                    }
                }
            }
            catch
            {
                // Just stopping background thread.
            }

            _cts = null;
            _ct = default(CancellationToken);

            _backgroundWorkerTask = null;
        }

        private void InitDecRefThread()
        {
            _cts = new CancellationTokenSource();
            _ct = _cts.Token;

            _backgroundWorkerTask = Task.Factory.StartNew(WorkerThread, _ct, TaskCreationOptions.LongRunning,
                DedicatedThreadTaskScheduler);
        }

        private void WorkerThread()
        {
            while (true)
            {
                IntPtr refToDecrease = _asyncDecRefQueue.Take(_ct); ;

                try
                {

                    IntPtr gs = PythonEngine.AcquireLock();
                    try
                    {
                        do
                        {
                            Runtime.XDecref(refToDecrease);
                        } while (_asyncDecRefQueue.TryTake(out refToDecrease));
                    }
                    finally
                    {
                        PythonEngine.ReleaseLock(gs);
                    }
                }
                catch
                {
                    // Nothing to do in this case.
                }
            }
        }

        public void Dispose()
        {
            ShutdownDecRefThread();
        }
    }


    /// <summary>
    /// Scheduler that uses only one thread for all scheduled task.
    /// </summary>
    internal class DedicatedThreadTaskScheduler : TaskScheduler
    {
        private readonly BlockingCollection<Task> _queuedTasks = new BlockingCollection<Task>();


        /// <summary>
        /// Initializes a new instance of the <see cref="DedicatedThreadTaskScheduler"/> class.
        /// </summary>
        public DedicatedThreadTaskScheduler()
        {
            var thread = new Thread(WorkerThreadProc);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <inheritdoc/>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _queuedTasks;
        }

        /// <inheritdoc/>
        protected override void QueueTask(Task task)
        {
            _queuedTasks.Add(task);
        }

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        private void WorkerThreadProc()
        {
            for (;;)
            {
                Task dequeuedTask = _queuedTasks.Take();

                // This is synchronous execution.
                bool taskExecuted = TryExecuteTask(dequeuedTask);
                Debug.Assert(taskExecuted, "DedicatedThread task have some problem.");
            }
        }
    }
}
