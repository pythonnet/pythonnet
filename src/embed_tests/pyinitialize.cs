using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyInitializeTest
    {
        /// <summary>
        /// Tests issue with multiple simple Initialize/Shutdowns.
        /// Fixed by #343
        /// </summary>
        [Test]
        public static void StartAndStopTwice()
        {
            PythonEngine.Initialize();
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            PythonEngine.Shutdown();
        }

        [Test]
        public static void LoadDefaultArgs()
        {
            using (new PythonEngine())
            using (var argv = new PyList(Runtime.Runtime.PySys_GetObject("argv")))
            {
                Assert.AreNotEqual(0, argv.Length());
            }
        }

        [Test]
        public static void LoadSpecificArgs()
        {
            var args = new[] { "test1", "test2" };
            using (new PythonEngine(args))
            using (var argv = new PyList(Runtime.Runtime.PySys_GetObject("argv")))
            {
                Assert.AreEqual(args[0], argv[0].ToString());
                Assert.AreEqual(args[1], argv[1].ToString());
            }
        }

        /// <summary>
        /// Failing test demonstrating current issue with OverflowException (#376)
        /// and ArgumentException issue after that one is fixed.
        /// More complex version of StartAndStopTwice test
        /// </summary>
        [Test]
        [Ignore("GH#376: System.OverflowException : Arithmetic operation resulted in an overflow")]
        //[Ignore("System.ArgumentException : Cannot pass a GCHandle across AppDomains")]
        public void ReInitialize()
        {
            var code = "from System import Int32\n";
            PythonEngine.Initialize();
            using (Py.GIL())
            {
                // Import any class or struct from .NET
                PythonEngine.RunSimpleString(code);
            }
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            using (Py.GIL())
            {
                // Import a class/struct from .NET
                // This class/struct must be imported during the first initialization.
                PythonEngine.RunSimpleString(code);
                // Create an instance of the class/struct
                // System.OverflowException Exception will be raised here.
                // If replacing int with Int64, OverflowException will be replaced with AppDomain exception.
                PythonEngine.RunSimpleString("Int32(1)");
            }
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestScopeIsShutdown()
        {
            PythonEngine.Initialize();
            var scope = PyScopeManager.Global.Create("test");
            PythonEngine.Shutdown();
            Assert.That(PyScopeManager.Global.Contains("test"), Is.False);
        }

        /// <summary>
        /// Helper for testing the shutdown handlers.
        /// </summary>
        int shutdown_count = 0;
        void OnShutdownIncrement()
        {
            shutdown_count++;
        }
        void OnShutdownDouble()
        {
            shutdown_count *= 2;
        }

        /// <summary>
        /// Test the shutdown handlers.
        /// </summary>
        [Test]
        public void ShutdownHandlers()
        {
            // Test we can run one shutdown handler.
            shutdown_count = 0;
            PythonEngine.Initialize();
            PythonEngine.AddShutdownHandler(OnShutdownIncrement);
            PythonEngine.Shutdown();
            Assert.That(shutdown_count, Is.EqualTo(1));

            // Test we can run multiple shutdown handlers in the right order.
            shutdown_count = 4;
            PythonEngine.Initialize();
            PythonEngine.AddShutdownHandler(OnShutdownIncrement);
            PythonEngine.AddShutdownHandler(OnShutdownDouble);
            PythonEngine.Shutdown();
            // Correct: 4 * 2 + 1 = 9
            // Wrong:  (4 + 1) * 2 = 10
            Assert.That(shutdown_count, Is.EqualTo(9));

            // Test we can remove shutdown handlers, handling duplicates.
            shutdown_count = 4;
            PythonEngine.Initialize();
            PythonEngine.AddShutdownHandler(OnShutdownIncrement);
            PythonEngine.AddShutdownHandler(OnShutdownIncrement);
            PythonEngine.AddShutdownHandler(OnShutdownDouble);
            PythonEngine.AddShutdownHandler(OnShutdownIncrement);
            PythonEngine.AddShutdownHandler(OnShutdownDouble);
            PythonEngine.RemoveShutdownHandler(OnShutdownDouble);
            PythonEngine.Shutdown();
            // Correct: (4 + 1) * 2 + 1 + 1 = 12
            // Wrong:   (4 * 2) + 1 + 1 + 1 = 11
            Assert.That(shutdown_count, Is.EqualTo(12));
        }
    }
}
