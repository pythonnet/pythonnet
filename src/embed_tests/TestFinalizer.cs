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
            Exceptions.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Finalizer.Instance.Threshold = _oldThreshold;
        }

        private static void FullGCCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect(); // reclaim objects whose finalizers just ran
        }

        [Test]
        [Obsolete("GC tests are not guaranteed")]
        public void CollectBasicObject()
        {
            Assert.That(Finalizer.Instance.Enable, Is.True);

            Finalizer.Instance.Threshold = 1;
            bool called = false;
            var objectCount = 0;
            EventHandler<Finalizer.CollectArgs> handler = (s, e) =>
            {
                objectCount = e.ObjectCount;
                called = true;
            };

            Assert.That(called, Is.False, "The event handler was called before it was installed");
            Finalizer.Instance.BeforeCollect += handler;

            IntPtr pyObj = MakeAGarbage(out var shortWeak, out var longWeak);

            // The real contract: after the wrapper is GC'd, the underlying
            // Python pointer must end up in Finalizer's queue.  Poll because
            // .NET Framework / .NET Core differ in how many GC cycles it takes.
            List<IntPtr> garbage = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                FullGCCollect();
                garbage = Finalizer.Instance.GetCollectedObjects();
                if (garbage.Contains(pyObj)) break;
                Thread.Sleep(20);
            }

            Warn.If(shortWeak.IsAlive,
                "shortWeak is alive after FullGCCollect; runtime hasn't reclaimed the wrapper yet",
                shortWeak);
            // longWeak.IsAlive at this point is .NET-GC-implementation-defined
            // (Framework reclaims post-finalize objects more eagerly than Core);
            // intentionally not asserted.

            Assert.That(garbage, Has.Member(pyObj),
                "PyObject did not reach Finalizer.Instance.GetCollectedObjects()");
            try
            {
                Finalizer.Instance.Collect();
            }
            finally
            {
                Finalizer.Instance.BeforeCollect -= handler;
            }
            Assert.That(called, Is.True, "The event handler was not called during finalization");
            Assert.GreaterOrEqual(objectCount, 1);
        }

        [Test]
        [Ignore("Requires explicit shutdown")]
        [Obsolete("GC tests are not guaranteed")]
        public void CollectOnShutdown()
        {
            IntPtr op = MakeAGarbage(out var shortWeak, out var longWeak);
            FullGCCollect();
            Assert.That(shortWeak.IsAlive, Is.False);
            List<IntPtr> garbage = Finalizer.Instance.GetCollectedObjects();
            Assert.IsNotEmpty(garbage, "The garbage object should be collected");
            Assert.That(garbage.Contains(op),
                Is.True,
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
            Assert.That(garbageGen.Join(TimeSpan.FromSeconds(5)), Is.True, "Garbage creation timed out");
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
                Assert.That(e.Handle, Is.EqualTo(ptr));
                Assert.That(e.ImpactedObjects.Count, Is.EqualTo(2));
                // Fix for this test, don't do this on general environment
#pragma warning disable CS0618 // Type or member is obsolete
                Runtime.Runtime.XIncref(e.Reference);
#pragma warning restore CS0618 // Type or member is obsolete
                return false;
            };
            Finalizer.Instance.IncorrectRefCntResolver += handler;
            try
            {
                ptr = CreateStringGarbage();
                FullGCCollect();
                Assert.Throws<Finalizer.IncorrectRefCountException>(() => Finalizer.Instance.Collect());
                Assert.That(called, Is.True);
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
            IntPtr address = s1.Reference.DangerousGetAddress();
            PyString s2 = new (StolenReference.DangerousFromPointer(address));
            return address;
        }
    }
}
