using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestFinalizer
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestClrObjectFullRelease()
        {
            var gs = PythonEngine.BeginAllowThreads();

            WeakReference weakRef;
            try
            {
                var weakRefCreateTask = Task.Factory.StartNew(() =>
                {
                    using (Py.GIL())
                    {
                        byte[] testObject = new byte[100];
                        var testObjectWeakReference = new WeakReference(testObject);

                        dynamic pyList = new PyList();
                        pyList.append(testObject);
                        return testObjectWeakReference;
                    }
                });

                weakRef = weakRefCreateTask.Result;
            }
            finally
            {
                PythonEngine.EndAllowThreads(gs);
            }

            // Triggering C# finalizer (PyList ref should be scheduled to decref).
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Forcing dec reference thread to wakeup and decref PyList.
            PythonEngine.EndAllowThreads(PythonEngine.BeginAllowThreads());
            Thread.Sleep(200);
            PythonEngine.CurrentRefDecrementer.WaitForPendingDecReferences();

            // Now python free up GCHandle on CLRObject and subsequent GC should fully remove testObject.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakRef.IsAlive, "Clr object should be collected."); 
        }
    }
}
