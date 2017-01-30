using NUnit.Framework;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Python.EmbeddingTest
{
    public class InitializeTest
    {
        [Test]
        public static void LoadSpecificArgs()
        {
            var args = new[] { "test1", "test2" };
            PythonEngine.Initialize(args);
            try
            {
                using (var argv = new PyList(Runtime.Runtime.PySys_GetObject("argv")))
                {
                    Assert.That(argv[0].ToString() == args[0]);
                    Assert.That(argv[1].ToString() == args[1]);
                }
            }
            finally
            {
                PythonEngine.Shutdown();
            }
        }

        [Test]
        public static void LoadDefaultArgs()
        {
            PythonEngine.Initialize();
            try
            {
                using (var argv = new PyList(Runtime.Runtime.PySys_GetObject("argv")))
                {
                    Assert.That(argv.Length() != 0);
                }
            }
            finally
            {
                PythonEngine.Shutdown();
            }
        }

        [Test]
        public static void Test()
        {
            PythonEngine.Initialize();
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            PythonEngine.Shutdown();
        }
    }
}
