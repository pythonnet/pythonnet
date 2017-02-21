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

        [Test]
        [Ignore("System.OverflowException : Arithmetic operation resulted in an overflow")]
        //[Ignore("System.ArgumentException : Cannot pass a GCHandle across AppDomains")]
        public void ReInitialize()
        {
            string code = "from System import Int32\n";
            PythonEngine.Initialize();
            using (Py.GIL())
            {
                //import any class or struct from .NET
                PythonEngine.RunSimpleString(code);
            }
            PythonEngine.Shutdown();

            PythonEngine.Initialize();
            using (Py.GIL())
            {
                //Import a class/struct from .NET
                //This class/struct must be imported during the first initialization.
                PythonEngine.RunSimpleString(code);
                //Create an instance of the class/struct
                //System.OverflowException Exception will be raised here.
                //If replacing int with Int64, OverflowException will be replaced with AppDomain exception.
                PythonEngine.RunSimpleString("Int32(1)");
            }
            PythonEngine.Shutdown();
        }
    }
}
