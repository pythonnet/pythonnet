using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestInterrupt
    {
        PyObject threading;
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            // workaround for assert tlock.locked() warning
            threading = Py.Import("threading");
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            threading.Dispose();
            PythonEngine.Shutdown();
        }

        [Test]
        public void PythonThreadIDStable()
        {
            long pythonThreadID = 0;
            long pythonThreadID2 = 0;
            var asyncCall = Task.Factory.StartNew(() =>
            {
                using (Py.GIL())
                {
                    Interlocked.Exchange(ref pythonThreadID, (long)PythonEngine.GetPythonThreadID());
                    Interlocked.Exchange(ref pythonThreadID2, (long)PythonEngine.GetPythonThreadID());
                }
            });

            var timeout = Stopwatch.StartNew();

            IntPtr threadState = PythonEngine.BeginAllowThreads();
            while (Interlocked.Read(ref pythonThreadID) == 0 || Interlocked.Read(ref pythonThreadID2) == 0)
            {
                Assert.Less(timeout.Elapsed, TimeSpan.FromSeconds(5), "thread IDs were not assigned in time");
            }
            PythonEngine.EndAllowThreads(threadState);

            Assert.IsTrue(asyncCall.Wait(TimeSpan.FromSeconds(5)), "Async thread has not finished in time");

            Assert.AreEqual(pythonThreadID, pythonThreadID2);
            Assert.NotZero(pythonThreadID);
        }

        [Test]
        public void InterruptTest()
        {
            long pythonThreadID = 0;
            var asyncCall = Task.Factory.StartNew(() =>
            {
                using (Py.GIL())
                {
                    Interlocked.Exchange(ref pythonThreadID, (long)PythonEngine.GetPythonThreadID());
                    return PythonEngine.RunSimpleString(@"
try:
  import time

  while True:
    time.sleep(0.2)
except KeyboardInterrupt:
  pass");
                }
            });

            var timeout = Stopwatch.StartNew();

            IntPtr threadState = PythonEngine.BeginAllowThreads();
            while (Interlocked.Read(ref pythonThreadID) == 0)
            {
                Assert.Less(timeout.Elapsed, TimeSpan.FromSeconds(5), "thread ID was not assigned in time");
            }
            PythonEngine.EndAllowThreads(threadState);

            int interruptReturnValue = PythonEngine.Interrupt((ulong)Interlocked.Read(ref pythonThreadID));
            Assert.AreEqual(1, interruptReturnValue);

            threadState = PythonEngine.BeginAllowThreads();
            Assert.IsTrue(asyncCall.Wait(TimeSpan.FromSeconds(5)), "Async thread was not interrupted in time");
            PythonEngine.EndAllowThreads(threadState);

            Assert.AreEqual(0, asyncCall.Result);
        }
    }
}
