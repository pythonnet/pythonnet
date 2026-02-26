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
            // workaround for assert tlock.locked() warning
            threading = Py.Import("threading");
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            threading.Dispose();
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

            Assert.That(asyncCall.Wait(TimeSpan.FromSeconds(5)), Is.True, "Async thread has not finished in time");

            Assert.That(pythonThreadID2, Is.EqualTo(pythonThreadID));
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
            Assert.That(interruptReturnValue, Is.EqualTo(1));

            threadState = PythonEngine.BeginAllowThreads();
            Assert.That(asyncCall.Wait(TimeSpan.FromSeconds(5)), Is.True, "Async thread was not interrupted in time");
            PythonEngine.EndAllowThreads(threadState);

            Assert.That(asyncCall.Result, Is.EqualTo(0));
        }
    }
}
