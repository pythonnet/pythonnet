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
        private IntPtr _threadState;

        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            _threadState = PythonEngine.BeginAllowThreads();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.EndAllowThreads(_threadState);
            PythonEngine.Shutdown();
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
import time

while True:
    time.sleep(0.2)");
                }
            });

            var timeout = Stopwatch.StartNew();
            while (Interlocked.Read(ref pythonThreadID) == 0)
            {
                Assert.Less(timeout.Elapsed, TimeSpan.FromSeconds(5), "thread ID was not assigned in time");
            }

            using (Py.GIL())
            {
                int interruptReturnValue = PythonEngine.Interrupt((ulong)Interlocked.Read(ref pythonThreadID));
                Assert.AreEqual(1, interruptReturnValue);
            }

            Assert.IsTrue(asyncCall.Wait(TimeSpan.FromSeconds(5)), "Async thread was not interrupted in time");

            Assert.AreEqual(-1, asyncCall.Result);
        }
    }
}
