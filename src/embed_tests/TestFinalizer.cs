using NUnit.Framework;
using Python.Runtime;
using System;
using System.Threading;

namespace Python.EmbeddingTest
{
    public class TestFinalizer
    {
        private string _PYTHONMALLOC = string.Empty;

        [SetUp]
        public void SetUp()
        {
            try
            {
                _PYTHONMALLOC = Environment.GetEnvironmentVariable("PYTHONMALLOC");
            }
            catch (ArgumentNullException)
            {
                _PYTHONMALLOC = string.Empty;
            }
            Environment.SetEnvironmentVariable("PYTHONMALLOC", "malloc");
            PythonEngine.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            PythonEngine.Shutdown();
            if (string.IsNullOrEmpty(_PYTHONMALLOC))
            {
                Environment.SetEnvironmentVariable("PYTHONMALLOC", _PYTHONMALLOC);
            }
        }

        private static void FullGCCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void CollectBasicObject()
        {
            Assert.IsTrue(Finalizer.Instance.Enable);

            int thId = Thread.CurrentThread.ManagedThreadId;
            Finalizer.Instance.Threshold = 1;
            bool called = false;
            EventHandler<Finalizer.CollectArgs> handler = (s, e) =>
            {
                Assert.AreEqual(thId, Thread.CurrentThread.ManagedThreadId);
                Assert.GreaterOrEqual(e.ObjectCount, 1);
                called = true;
            };
            Finalizer.Instance.CollectOnce += handler;
            FullGCCollect();
            PyLong obj = new PyLong(1024);

            WeakReference shortWeak = new WeakReference(obj);
            WeakReference longWeak = new WeakReference(obj, true);
            obj = null;
            FullGCCollect();
            // The object has been resurrected
            // FIXME: Sometimes the shortWeak would get alive 
            //Assert.IsFalse(shortWeak.IsAlive);
            Assert.IsTrue(longWeak.IsAlive);

            Assert.IsFalse(called);
            var garbage = Finalizer.Instance.GetCollectedObjects();
            Assert.NotZero(garbage.Count);
            // FIXME: If make some query for garbage,
            // the above case will failed Assert.IsFalse(shortWeak.IsAlive)
            //Assert.IsTrue(garbage.All(T => T.IsAlive));

            Finalizer.Instance.CallPendingFinalizers();
            Assert.IsTrue(called);

            FullGCCollect();
            //Assert.IsFalse(garbage.All(T => T.IsAlive));

            Assert.IsNull(longWeak.Target);

            Finalizer.Instance.CollectOnce -= handler;
        }

        private static long CompareWithFinalizerOn(PyObject pyCollect, bool enbale)
        {
            // Must larger than 512 bytes make sure Python use 
            string str = new string('1', 1024);
            Finalizer.Instance.Enable = true;
            FullGCCollect();
            FullGCCollect();
            pyCollect.Invoke();
            Finalizer.Instance.CallPendingFinalizers();
            Finalizer.Instance.Enable = enbale;

            // Estimate unmanaged memory size
            long before = Environment.WorkingSet - GC.GetTotalMemory(true);
            for (int i = 0; i < 10000; i++)
            {
                // Memory will leak when disable Finalizer
                new PyString(str);
            }
            FullGCCollect();
            FullGCCollect();
            pyCollect.Invoke();
            if (enbale)
            {
                Finalizer.Instance.CallPendingFinalizers();
            }

            FullGCCollect();
            FullGCCollect();
            long after = Environment.WorkingSet - GC.GetTotalMemory(true);
            return after - before;

        }

        /// <summary>
        /// Because of two vms both have their memory manager,
        /// this test only prove the finalizer has take effect.
        /// </summary>
        [Test]
        [Ignore("Too many uncertainties, only manual on when debugging")]
        public void SimpleTestMemory()
        {
            bool oldState = Finalizer.Instance.Enable;
            try
            {
                using (PyObject gcModule = PythonEngine.ImportModule("gc"))
                using (PyObject pyCollect = gcModule.GetAttr("collect"))
                {
                    long span1 = CompareWithFinalizerOn(pyCollect, false);
                    long span2 = CompareWithFinalizerOn(pyCollect, true);
                    Assert.Less(span2, span1);
                }
            }
            finally
            {
                Finalizer.Instance.Enable = oldState;
            }
        }
    }
}
