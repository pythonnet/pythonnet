using NUnit.Framework;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        private static void FullGCCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        [Obsolete("GC tests are not guaranteed")]
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
            Finalizer.Instance.BeforeCollect += handler;

            IntPtr pyObj = MakeAGarbage(out var shortWeak, out var longWeak);
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
                    garbage.Contains(pyObj),
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
                Finalizer.Instance.BeforeCollect -= handler;
            }
            Assert.IsTrue(called, "The event handler was not called during finalization");
            Assert.GreaterOrEqual(objectCount, 1);
        }

        [Test]
        [Obsolete("GC tests are not guaranteed")]
        public void CollectOnShutdown()
        {
            IntPtr op = MakeAGarbage(out var shortWeak, out var longWeak);
            FullGCCollect();
            Assert.IsFalse(shortWeak.IsAlive);
            List<IntPtr> garbage = Finalizer.Instance.GetCollectedObjects();
            Assert.IsNotEmpty(garbage, "The garbage object should be collected");
            Assert.IsTrue(garbage.Contains(op),
                "Garbage should contains the collected object");

            PythonEngine.Shutdown();
            garbage = Finalizer.Instance.GetCollectedObjects();

            if (garbage.Count > 0)
            {
                PythonEngine.Initialize();
                string objects = string.Join("\n", garbage.Select(ob =>
                {
                    var obj = new PyObject(new BorrowedReference(ob));
                    return $"{obj} [{obj.GetPythonType()}@{obj.Handle}]";
                }));
                Assert.Fail("Garbage is not empty:\n" + objects);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] // ensure lack of references to obj
        [Obsolete("GC tests are not guaranteed")]
        private static IntPtr MakeAGarbage(out WeakReference shortWeak, out WeakReference longWeak)
        {
            IntPtr handle = IntPtr.Zero;
            WeakReference @short = null, @long = null;
            // must create Python object in the thread where we have GIL
            IntPtr val = Runtime.Runtime.PyLong_FromLongLong(1024).DangerousMoveToPointerOrNull();
            // must create temp object in a different thread to ensure it is not present
            // when conservatively scanning stack for GC roots.
            // see https://xamarin.github.io/bugzilla-archives/17/17593/bug.html
            var garbageGen = new Thread(() =>
            {
                var obj = new PyObject(val, skipCollect: true);
                @short = new WeakReference(obj);
                @long = new WeakReference(obj, true);
                handle = obj.Handle;
            });
            garbageGen.Start();
            Assert.IsTrue(garbageGen.Join(TimeSpan.FromSeconds(5)), "Garbage creation timed out");
            shortWeak = @short;
            longWeak = @long;
            return handle;
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
                using (PyObject gcModule = PyModule.Import("gc"))
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
                Runtime.Runtime.XIncref(e.Reference);
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] // ensure lack of references to s1 and s2
        private static IntPtr CreateStringGarbage()
        {
            PyString s1 = new PyString("test_string");
            // s2 steal a reference from s1
            PyString s2 = new PyString(StolenReference.DangerousFromPointer(s1.Handle));
            return s1.Handle;
        }
    }
}
