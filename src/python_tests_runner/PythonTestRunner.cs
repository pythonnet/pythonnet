using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using NUnit.Framework;

using Python.Runtime;

namespace Python.PythonTestsRunner
{
    public class PythonTestRunner
    {
        string OriginalDirectory;

        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            OriginalDirectory = Environment.CurrentDirectory;

            var codeDir = File.ReadAllText("tests_location.txt").Trim();
            TestContext.Progress.WriteLine($"Changing working directory to {codeDir}");
            Environment.CurrentDirectory = codeDir;
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
            Environment.CurrentDirectory = OriginalDirectory;
        }

        /// <summary>
        /// Selects the Python tests to be run as embedded tests.
        /// </summary>
        /// <returns></returns>
        static IEnumerable<string[]> PythonTestCases()
        {
            // Add the test that you want to debug here.
            yield return new[] { "test_indexer", "test_boolean_indexer" };
            yield return new[] { "test_delegate", "test_bool_delegate" };
            yield return new[] { "test_subclass", "test_implement_interface_and_class" };
        }

        /// <summary>
        /// Runs a test in src/tests/*.py as an embedded test.  This facilitates debugging.
        /// </summary>
        /// <param name="testFile">The file name without extension</param>
        /// <param name="testName">The name of the test method</param>
        [TestCaseSource(nameof(PythonTestCases))]
        public void RunPythonTest(string testFile, string testName)
        {
            using dynamic pytest = Py.Import("pytest");

            using var args = new PyList();
            args.Append(new PyString($"{testFile}.py::{testName}"));
            int res = pytest.main(args);
            if (res != 0)
            {
                Assert.Fail($"Python test {testFile}.{testName} failed");
            }
        }
    }
}
