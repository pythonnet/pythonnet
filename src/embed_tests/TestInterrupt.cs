
using System;
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
            int runSimpleStringReturnValue = int.MinValue;
            ulong nativeThreadID = ulong.MinValue;
            Task.Factory.StartNew(() =>
            {
                using (Py.GIL())
                {
                    nativeThreadID = PythonEngine.GetNativeThreadID();
                    runSimpleStringReturnValue = PythonEngine.RunSimpleString(@"
import time

while True:
    time.sleep(0.2)");
                }
            });

            Thread.Sleep(200);

            Assert.AreNotEqual(ulong.MinValue, nativeThreadID);

            using (Py.GIL())
            {
                int interruptReturnValue = PythonEngine.Interrupt(nativeThreadID);
                Assert.AreEqual(1, interruptReturnValue);
            }

            Thread.Sleep(300);

            Assert.AreEqual(-1, runSimpleStringReturnValue);
        }
    }
}
