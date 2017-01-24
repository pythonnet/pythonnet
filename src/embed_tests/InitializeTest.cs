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
        public static void Test()
        {
            PythonEngine.Initialize();
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            PythonEngine.Shutdown();
        }
    }
}
