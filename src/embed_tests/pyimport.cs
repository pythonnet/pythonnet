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
            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures");
            TestContext.Out.WriteLine(testPath);

            IntPtr str = Runtime.Runtime.PyString_FromString(testPath);
            Assert.IsFalse(str == IntPtr.Zero);
            BorrowedReference path = Runtime.Runtime.PySys_GetObject("path");
            Assert.IsFalse(path.IsNull);
            Runtime.Runtime.PyList_Append(path, new BorrowedReference(str));
            Runtime.Runtime.XDecref(str);
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
            var module = PyModule.Import("PyImportTest.test.one");
            Assert.IsNotNull(module);
        }

        /// <summary>
        /// Tests that sys.args is set. If it wasn't exception would be raised.
        /// </summary>
        [Test]
        public void TestSysArgsImportException()
        {
            var module = PyModule.Import("PyImportTest.sysargv");
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

        [Test]
        public void BadAssembly()
        {
            string path;
            if (Python.Runtime.Runtime.IsWindows)
            {
                path = @"C:\Windows\System32\kernel32.dll";
            }
            else
            {
                Assert.Pass("TODO: add bad assembly location for other platforms");
                return;
            }

            string code = $@"
import clr
clr.AddReference('{path}')
";

            Assert.Throws<FileLoadException>(() => PythonEngine.Exec(code));
        }
    }
}
