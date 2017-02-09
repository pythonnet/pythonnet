using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyInitializeTest
    {
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
        public static void StartAndStopTwice()
        {
            PythonEngine.Initialize();
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            PythonEngine.Shutdown();
        }
    }
}
