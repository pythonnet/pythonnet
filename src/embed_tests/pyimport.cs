using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyImportTest
    {
        private IntPtr gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            gs = PythonEngine.AcquireLock();

            //string here = Environment.CurrentDirectory;
            //trunk\pythonnet\src\embed_tests\bin\x86\DebugWin

            /*
             * Append the tests directory to sys.path
             * using reflection to circumvent the private modifires placed on most Runtime methods.
             */
            const string s = @"../../../../tests";

            Type RTClass = typeof(Runtime.Runtime);

            /* pyStrPtr = PyString_FromString(s); */
            MethodInfo PyString_FromString = RTClass.GetMethod("PyString_FromString",
                BindingFlags.NonPublic | BindingFlags.Static);
            object[] funcArgs = new object[1];
            funcArgs[0] = s;
            IntPtr pyStrPtr = (IntPtr)PyString_FromString.Invoke(null, funcArgs);

            /* SysDotPath = sys.path */
            MethodInfo PySys_GetObject = RTClass.GetMethod("PySys_GetObject",
                BindingFlags.NonPublic | BindingFlags.Static);
            funcArgs[0] = "path";
            IntPtr SysDotPath = (IntPtr)PySys_GetObject.Invoke(null, funcArgs);

            /* SysDotPath.append(*pyStrPtr) */
            MethodInfo PyList_Append = RTClass.GetMethod("PyList_Append", BindingFlags.NonPublic | BindingFlags.Static);
            funcArgs = new object[] { SysDotPath, pyStrPtr };
            int r = (int)PyList_Append.Invoke(null, funcArgs);
        }

        [TearDown]
        public void TearDown()
        {
            PythonEngine.ReleaseLock(gs);
            PythonEngine.Shutdown();
        }

        /// <summary>
        /// Test subdirectory import
        /// </summary>
        /// <remarks>
        /// The required directory structure was added to the \trunk\pythonnet\src\tests directory:
        ///
        ///     PyImportTest/
        ///         __init__.py
        ///         test/
        ///             __init__.py
        ///             one.py
        /// </remarks>
        [Test]
        public void TestDottedName()
        {
            PyObject module = PythonEngine.ImportModule("PyImportTest.test.one");
            Assert.IsNotNull(module, ">>>  import PyImportTest.test.one  # FAILED");
        }
    }
}
