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

        [Test]
        [Ignore("For debug only")]
        public void TestExceptionMemoryLeak()
        {
            dynamic pymodule;
            PyObject gc;
            dynamic testmethod;

            var ts = PythonEngine.BeginAllowThreads();
            IntPtr pylock = PythonEngine.AcquireLock();

#if NETCOREAPP
            const string s = "../../fixtures";
#else
            const string s = "../fixtures";
#endif
            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, s);

            IntPtr str = Runtime.Runtime.PyString_FromString(testPath);
            IntPtr path = Runtime.Runtime.PySys_GetObject("path");
            Runtime.Runtime.PyList_Append(path, str);

            {
                PyObject sys = PythonEngine.ImportModule("sys");
                gc = PythonEngine.ImportModule("gc");

                pymodule = PythonEngine.ImportModule("MemoryLeakTest.pyraise");
                testmethod = pymodule.test_raise_exception;
            }

            PythonEngine.ReleaseLock(pylock);

            double floatarg1 = 5.1f;
            dynamic res = null;
            {
                for (int i = 1; i <= 10000000; i++)
                {
                    using (Py.GIL())
                    {
                        try
                        {
                            res = testmethod(Py.kw("number", floatarg1), Py.kw("astring", "bbc"));
                        }
                        catch (Exception e)
                        {
                            if (i % 10000 == 0)
                            {
                                TestContext.Progress.WriteLine(e.Message);
                            }
                        }
                    }

                    if (i % 10000 == 0)
                    {
                        GC.Collect();
                    }
                }
            }

            PythonEngine.EndAllowThreads(ts);
        }
    }
}
