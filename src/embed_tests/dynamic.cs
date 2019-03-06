using System;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class DynamicTest
    {
        private Py.GILState _gs;

        [SetUp]
        public void SetUp()
        {
            try {
                _gs = Py.GIL();
            } catch (Exception e) {
                Console.WriteLine($"exception in SetUp: {e}");
                throw;
            }
        }

        [TearDown]
        public void Dispose()
        {
            try {
                _gs.Dispose();
            } catch(Exception e) {
                Console.WriteLine($"exception in TearDown: {e}");
                throw;
            }
        }

        /// <summary>
        /// Set the attribute of a PyObject with a .NET object.
        /// </summary>
        [Test]
        public void AssignObject()
        {
            var stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr = stream;
            // Check whether there are the same object.
            dynamic _stream = sys.testattr.AsManagedObject(typeof(StringBuilder));
            Assert.AreEqual(_stream, stream);

            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr.Append('Hello!')\n");
            Assert.AreEqual(stream.ToString(), "Hello!");
        }

        /// <summary>
        /// Set the attribute of a PyObject to null.
        /// </summary>
        [Test]
        public void AssignNone()
        {
            dynamic sys = Py.Import("sys");
            sys.testattr = new StringBuilder();
            Assert.IsNotNull(sys.testattr);

            sys.testattr = null;
            Assert.IsNull(sys.testattr);
        }

        /// <summary>
        /// Check whether we can get the attr of a python object when the
        /// value of attr is a PyObject.
        /// </summary>
        /// <remarks>
        /// FIXME: Issue on Travis PY27: Error : Python.EmbeddingTest.dynamicTest.AssignPyObject
        /// Python.Runtime.PythonException : ImportError : /home/travis/virtualenv/python2.7.9/lib/python2.7/lib-dynload/_io.so: undefined symbol: _PyLong_AsInt
        /// </remarks>
        [Test]
        public void AssignPyObject()
        {
            if (Environment.GetEnvironmentVariable("TRAVIS") == "true" &&
                Environment.GetEnvironmentVariable("TRAVIS_PYTHON_VERSION") == "2.7")
            {
                Assert.Ignore("Fails on Travis/PY27: ImportError: ... undefined symbol: _PyLong_AsInt");
            }

            dynamic sys = Py.Import("sys");
            dynamic io = Py.Import("io");
            sys.testattr = io.StringIO();
            dynamic bb = sys.testattr; // Get the PyObject
            bb.write("Hello!");
            Assert.AreEqual(bb.getvalue().ToString(), "Hello!");
        }

        /// <summary>
        /// Pass the .NET object in Python side.
        /// </summary>
        [Test]
        public void PassObjectInPython()
        {
            var stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr1 = stream;

            // Pass the .NET object in Python side
            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr2 = sys.testattr1\n"
            );

            // Compare in Python
            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
            );
            Assert.AreEqual(sys.testattr3.ToString(), "True");

            // Compare in .NET
            Assert.IsTrue(sys.testattr1.Equals(sys.testattr2));
        }

        /// <summary>
        /// Pass the PyObject in .NET side
        /// </summary>
        [Test]
        public void PassPyObjectInNet()
        {
            var stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr1 = stream;
            sys.testattr2 = sys.testattr1;

            // Compare in Python
            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
            );

            Assert.AreEqual(sys.testattr3.ToString(), "True");

            // Compare in .NET
            Assert.IsTrue(sys.testattr1.Equals(sys.testattr2));
        }
    }
}
