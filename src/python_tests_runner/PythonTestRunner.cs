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
            Python.Runtime.Runtime.PythonDLL =
                "C:\\Python37.2\\python37.dll";
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
            yield return new[] { "test_subclass", "test_virtual_generic_method" };
            yield return new[] { "test_subclass", "test_interface_and_class_impl2" };
            yield return new[] { "test_subclass", "test_class_with_attributes" };
            yield return new[] { "test_subclass", "test_class_with_advanced_attribute" };
            yield return new[] { "test_subclass", "test_more_subclasses" };
            yield return new[] { "test_subclass", "test_more_subclasses2" };
            yield return new[] { "test_subclass", "abstract_test" };
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
