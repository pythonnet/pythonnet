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
                Assert.That(argv[0].ToString() == args[0]);
                Assert.That(argv[1].ToString() == args[1]);
            }
        }

        [Test]
        public static void LoadDefaultArgs()
        {
            using (new PythonEngine())
            using (var argv = new PyList(Runtime.Runtime.PySys_GetObject("argv")))
            {
                Assert.That(argv.Length() != 0);
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
