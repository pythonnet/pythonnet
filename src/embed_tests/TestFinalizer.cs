using NUnit.Framework;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Python.EmbeddingTest
{
    public class TestFinalizer
    {
        private int _oldThreshold;

        [SetUp]
        public void SetUp()
        {
            _oldThreshold = Finalizer.Instance.Threshold;
            PythonEngine.Initialize();
            Exceptions.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Finalizer.Instance.Threshold = _oldThreshold;
            PythonEngine.Shutdown();
        }

        private static bool FullGCCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            try
            {
                return GC.WaitForFullGCComplete() == GCNotificationStatus.Succeeded;
            }
            catch (NotImplementedException)
            {
                // Some clr runtime didn't implement GC.WaitForFullGCComplete yet.
                return false;
            }
            finally
            {
                GC.WaitForPendingFinalizers();
            }
        }

        [Test]
        public void CollectBasicObject()
        {
            Assert.IsTrue(Finalizer.Instance.Enable);

            Finalizer.Instance.Threshold = 1;
            bool called = false;
            var objectCount = 0;
            EventHandler<Finalizer.CollectArgs> handler = (s, e) =>
            {
                objectCount = e.ObjectCount;
                called = true;
            };

            Assert.IsFalse(called, "The event handler was called before it was installed");
            Finalizer.Instance.CollectOnce += handler;

            WeakReference shortWeak;
            WeakReference longWeak;
            {
                MakeAGarbage(out shortWeak, out longWeak);
            }
            FullGCCollect();
            // The object has been resurrected
            Warn.If(
                shortWeak.IsAlive,
                "The referenced object is alive although it should have been collected",
                shortWeak
            );
            Assert.IsTrue(
                longWeak.IsAlive,
                "The reference object is not alive although it should still be",
                longWeak
            );

            {
                var garbage = Finalizer.Instance.GetCollectedObjects();
                Assert.NotZero(garbage.Count, "There should still be garbage around");
                Warn.Unless(
                    garbage.Any(T => ReferenceEquals(T.Target, longWeak.Target)),
                    $"The {nameof(longWeak)} reference doesn't show up in the garbage list",
                    garbage
                );
            }
            try
            {
                Finalizer.Instance.Collect();
            }
            finally
            {
                Finalizer.Instance.CollectOnce -= handler;
            }
            Assert.IsTrue(called, "The event handler was not called during finalization");
            Assert.GreaterOrEqual(objectCount, 1);
        }

        [Test]
        [Ignore("Ignore temporarily")]
        public void CollectOnShutdown()
        {
            IntPtr op = MakeAGarbage(out var shortWeak, out var longWeak);
            int hash = shortWeak.Target.GetHashCode();
            List<WeakReference> garbage;
            if (!FullGCCollect())
            {
                Assert.IsTrue(WaitForCollected(op, hash, 10000));
            }
            Assert.IsFalse(shortWeak.IsAlive);
            garbage = Finalizer.Instance.GetCollectedObjects();
            Assert.IsNotEmpty(garbage, "The garbage object should be collected");
            Assert.IsTrue(garbage.Any(r => ReferenceEquals(r.Target, longWeak.Target)),
                "Garbage should contains the collected object");

            PythonEngine.Shutdown();
            garbage = Finalizer.Instance.GetCollectedObjects();
            Assert.IsEmpty(garbage);
        }

        private static IntPtr MakeAGarbage(out WeakReference shortWeak, out WeakReference longWeak)
        {
            PyLong obj = new PyLong(1024);
            shortWeak = new WeakReference(obj);
            longWeak = new WeakReference(obj, true);
            return obj.Handle;
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

        class MyPyObject : PyObject
        {
            public MyPyObject(IntPtr op) : base(op)
            {
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                GC.SuppressFinalize(this);
                throw new Exception("MyPyObject");
            }
            internal static void CreateMyPyObject(IntPtr op)
            {
                Runtime.Runtime.XIncref(op);
                new MyPyObject(op);
            }
        }

        [Test]
        public void ErrorHandling()
        {
            bool called = false;
            var errorMessage = "";
            EventHandler<Finalizer.ErrorArgs> handleFunc = (sender, args) =>
            {
                called = true;
                errorMessage = args.Error.Message;
            };
            Finalizer.Instance.Threshold = 1;
            Finalizer.Instance.ErrorHandler += handleFunc;
            try
            {
                WeakReference shortWeak;
                WeakReference longWeak;
                {
                    MakeAGarbage(out shortWeak, out longWeak);
                    var obj = (PyLong)longWeak.Target;
                    IntPtr handle = obj.Handle;
                    shortWeak = null;
                    longWeak = null;
                    MyPyObject.CreateMyPyObject(handle);
                    obj.Dispose();
                    obj = null;
                }
                FullGCCollect();
                Finalizer.Instance.Collect();
                Assert.IsTrue(called);
            }
            finally
            {
                Finalizer.Instance.ErrorHandler -= handleFunc;
            }
            Assert.AreEqual(errorMessage, "MyPyObject");
        }

        [Test]
        public void ValidateRefCount()
        {
            if (!Finalizer.Instance.RefCountValidationEnabled)
            {
                Assert.Ignore("Only run with FINALIZER_CHECK");
            }
            IntPtr ptr = IntPtr.Zero;
            bool called = false;
            Finalizer.IncorrectRefCntHandler handler = (s, e) =>
            {
                called = true;
                Assert.AreEqual(ptr, e.Handle);
                Assert.AreEqual(2, e.ImpactedObjects.Count);
                // Fix for this test, don't do this on general environment
                Runtime.Runtime.XIncref(e.Handle);
                return false;
            };
            Finalizer.Instance.IncorrectRefCntResolver += handler;
            try
            {
                ptr = CreateStringGarbage();
                FullGCCollect();
                Assert.Throws<Finalizer.IncorrectRefCountException>(() => Finalizer.Instance.Collect());
                Assert.IsTrue(called);
            }
            finally
            {
                Finalizer.Instance.IncorrectRefCntResolver -= handler;
            }
        }

        private static IntPtr CreateStringGarbage()
        {
            PyString s1 = new PyString("test_string");
            // s2 steal a reference from s1
            PyString s2 = new PyString(s1.Handle);
            return s1.Handle;
        }

        private static bool WaitForCollected(IntPtr op, int hash, int milliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            do
            {
                var garbage = Finalizer.Instance.GetCollectedObjects();
                foreach (var item in garbage)
                {
                    // The validation is not 100% precise,
                    // but it's rare that two conditions satisfied but they're still not the same object.
                    if (item.Target.GetHashCode() != hash)
                    {
                        continue;
                    }
                    var obj = (IPyDisposable)item.Target;
                    if (obj.GetTrackedHandles().Contains(op))
                    {
                        return true;
                    }
                }
            } while (stopwatch.ElapsedMilliseconds < milliseconds);
            return false;
        }
    }
}
