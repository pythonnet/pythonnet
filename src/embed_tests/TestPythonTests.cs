using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPythonTests
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        static IEnumerable<string[]> MyTestCases()
        {
            yield return new[] { "test_generic", "test_missing_generic_type" };
        }

        [TestCaseSource(nameof(MyTestCases))]
        public void EmbeddedPythonTest(string testFile, string testName)
        {
            string folder = @"..\\..\\..\\..\\tests";
            PythonEngine.Exec($@"
import sys
sys.path.insert(0, '{folder}')
from {testFile} import *
{testName}()
");
        }
    }
}
