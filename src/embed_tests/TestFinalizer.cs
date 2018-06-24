using NUnit.Framework;
using Python.Runtime;
using System;
using System.Linq;
using System.Threading;

namespace Python.EmbeddingTest
{
    public class TestFinalizer
    {
        private string _PYTHONMALLOC = string.Empty;
        private int _oldThreshold;

        [SetUp]
        public void SetUp()
        {
            try
            {
                _PYTHONMALLOC = Environment.GetEnvironmentVariable("PYTHONMALLOC");
            }
            catch (ArgumentNullException)
            {
                _PYTHONMALLOC = null;
            }
            Environment.SetEnvironmentVariable("PYTHONMALLOC", "malloc");
            _oldThreshold = Finalizer.Instance.Threshold;
            PythonEngine.Initialize();
            Exceptions.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Finalizer.Instance.Threshold = _oldThreshold;
            PythonEngine.Shutdown();
            Environment.SetEnvironmentVariable("PYTHONMALLOC", _PYTHONMALLOC);
        }

        private static void FullGCCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
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

            WeakReference shortWeak;
            WeakReference longWeak;
            {
                MakeAGarbage(out shortWeak, out longWeak);
            }
            FullGCCollect();
            // The object has been resurrected
            Assert.IsFalse(shortWeak.IsAlive);
            Assert.IsTrue(longWeak.IsAlive);

            {
                var garbage = Finalizer.Instance.GetCollectedObjects();
                Assert.NotZero(garbage.Count);
                Assert.IsTrue(garbage.Any(T => ReferenceEquals(T.Target, longWeak.Target)));
            }

            Assert.IsFalse(called);
            Finalizer.Instance.CollectOnce += handler;
            try
            {
                Finalizer.Instance.CallPendingFinalizers();
            }
            finally
            {
                Finalizer.Instance.CollectOnce -= handler;
            }
            Assert.IsTrue(called);
        }

        private static void MakeAGarbage(out WeakReference shortWeak, out WeakReference longWeak)
        {
            PyLong obj = new PyLong(1024);
            shortWeak = new WeakReference(obj);
            longWeak = new WeakReference(obj, true);
            obj = null;
        }

        private static long CompareWithFinalizerOn(PyObject pyCollect, bool enbale)
        {
            // Must larger than 512 bytes make sure Python use 
            string str = new string('1', 1024);
            Finalizer.Instance.Enable = true;
            FullGCCollect();
            FullGCCollect();
            pyCollect.Invoke();
            Finalizer.Instance.Collect();
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
                Finalizer.Instance.Collect();
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
