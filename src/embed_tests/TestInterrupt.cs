
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestInterrupt
    {
        private IntPtr _threadState;

        [DllImport("Kernel32", EntryPoint = "GetCurrentThreadId", ExactSpelling = true)]
        private static extern uint GetCurrentThreadId();

        [DllImport("libc", EntryPoint = "pthread_self")]
        private static extern IntPtr pthread_selfLinux();

        [DllImport("pthread", EntryPoint = "pthread_self", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong pthread_selfOSX();

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
            ulong nativeThreadId = 0;
            Task.Factory.StartNew(() =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    nativeThreadId = GetCurrentThreadId();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    nativeThreadId = (ulong)pthread_selfLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    nativeThreadId = pthread_selfOSX();
                }

                using (Py.GIL())
                {
                    runSimpleStringReturnValue = PythonEngine.RunSimpleString(@"
import time

while True:
    time.sleep(0.2)");
                }
            });

            Thread.Sleep(200);

            using (Py.GIL())
            {
                int interruptReturnValue = PythonEngine.Interrupt(nativeThreadId);
                Assert.AreEqual(1, interruptReturnValue);
            }

            Thread.Sleep(300);

            Assert.AreEqual(-1, runSimpleStringReturnValue);
        }
    }
}
