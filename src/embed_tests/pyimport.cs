using System;
using System.IO;
using System.Runtime.InteropServices;

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
        string TestPath;

        [OneTimeSetUp]
        public void SetUp()
        {
            /* Append the tests directory to sys.path
             * using reflection to circumvent the private
             * modifiers placed on most Runtime methods. */
            TestPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "fixtures");
            TestContext.Out.WriteLine(TestPath);

            using var str = Runtime.Runtime.PyString_FromString(TestPath);
            Assert.That(str.IsNull(), Is.False);
            BorrowedReference path = Runtime.Runtime.PySys_GetObject("path");
            Assert.That(path.IsNull, Is.False);
            Runtime.Runtime.PyList_Append(path, str.Borrow());
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            // TODO Undo the above
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
            string path = Runtime.Runtime.PythonDLL;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = @"C:\Windows\System32\kernel32.dll";
            }

            Assert.That(File.Exists(path), Is.True, $"Test DLL {path} does not exist!");

            string code = $@"
import clr
clr.AddReference('{path}')
";

            Assert.Throws<BadImageFormatException>(() => PythonEngine.Exec(code));
        }
    }
}

// regression for https://github.com/pythonnet/pythonnet/issues/1601
// initialize fails if a class derived from IEnumerable is in global namespace
public class PublicEnumerator : System.Collections.IEnumerable
{
    public System.Collections.IEnumerator GetEnumerator()
    {
        return null;
    }
}

