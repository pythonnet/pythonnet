using System;
using System.IO;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// Test Import unittests and regressions
    /// </summary>
    /// <remarks>
    /// Keeping in old-style SetUp/TearDown due to required SetUp.
    /// The required directory structure was added to .\pythonnet\src\embed_tests\fixtures\ directory:
    /// + PyImportTest/
    /// | - __init__.py
    /// | + test/
    /// | | - __init__.py
    /// | | - one.py
    /// </remarks>
    public class PyImportTest
    {
        private IntPtr _gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            _gs = PythonEngine.AcquireLock();

            /* Append the tests directory to sys.path
             * using reflection to circumvent the private
             * modifiers placed on most Runtime methods. */
#if NETCOREAPP
            const string s = "../../fixtures";
#else
            const string s = "../fixtures";
#endif
            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, s);

            IntPtr str = Runtime.Runtime.PyString_FromString(testPath);
            IntPtr path = Runtime.Runtime.PySys_GetObject("path");
            Runtime.Runtime.PyList_Append(path, str);
        }

        [TearDown]
        public void Dispose()
        {
            PythonEngine.ReleaseLock(_gs);
            PythonEngine.Shutdown();
        }

        /// <summary>
        /// Test subdirectory import
        /// </summary>
        [Test]
        public void TestDottedName()
        {
            PyObject module = PythonEngine.ImportModule("PyImportTest.test.one");
            Assert.IsNotNull(module);
        }

        /// <summary>
        /// Tests that sys.args is set. If it wasn't exception would be raised.
        /// </summary>
        [Test]
        public void TestSysArgsImportException()
        {
            PyObject module = PythonEngine.ImportModule("PyImportTest.sysargv");
            Assert.IsNotNull(module);
        }

        /// <summary>
        /// Test Global Variable casting. GH#420
        /// </summary>
        [Test]
        public void TestCastGlobalVar()
        {
            dynamic foo = Py.Import("PyImportTest.cast_global_var");
            Assert.AreEqual("1", foo.FOO.ToString());
            Assert.AreEqual("1", foo.test_foo().ToString());

            foo.FOO = 2;
            Assert.AreEqual("2", foo.FOO.ToString());
            Assert.AreEqual("2", foo.test_foo().ToString());
        }
    }
}
