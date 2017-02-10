using System;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class dynamicTest
    {
        private Py.GILState gil;

        [SetUp]
        public void SetUp()
        {
            gil = Py.GIL();
        }

        [TearDown]
        public void TearDown()
        {
            gil.Dispose();
        }

        /// <summary>
        /// Set the attribute of a pyobject with a .NET object.
        /// </summary>
        [Test]
        public void AssignObject()
        {
            StringBuilder stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr = stream;
            // Check whether there are the same object.
            var _stream = sys.testattr.AsManagedObject(typeof(StringBuilder));
            Assert.AreEqual(_stream, stream);

            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr.Append('Hello!')\n");
            Assert.AreEqual(stream.ToString(), "Hello!");
        }

        /// <summary>
        /// Set the attribute of a pyobject to null.
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
        [Test]
        public void AssignPyObject()
        {
            dynamic sys = Py.Import("sys");
            dynamic io = Py.Import("io");
            sys.testattr = io.StringIO();
            dynamic bb = sys.testattr; //Get the PyObject
            bb.write("Hello!");
            Assert.AreEqual(bb.getvalue().ToString(), "Hello!");
        }

        /// <summary>
        /// Pass the .NET object in Python side.
        /// </summary>
        [Test]
        public void PassObjectInPython()
        {
            StringBuilder stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr1 = stream;

            //Pass the .NET object in Python side
            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr2 = sys.testattr1\n"
            );

            //Compare in Python
            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
            );
            Assert.AreEqual(sys.testattr3.ToString(), "True");

            //Compare in .NET
            Assert.AreEqual(sys.testattr1, sys.testattr2);
        }

        /// <summary>
        /// Pass the PyObject in .NET side
        /// </summary>
        [Test]
        public void PassPyObjectInNet()
        {
            StringBuilder stream = new StringBuilder();
            dynamic sys = Py.Import("sys");
            sys.testattr1 = stream;
            sys.testattr2 = sys.testattr1;

            //Compare in Python
            PyObject res = PythonEngine.RunString(
                "import sys\n" +
                "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
            );
            Assert.AreEqual(sys.testattr3.ToString(), "True");

            //Compare in .NET
            Assert.AreEqual(sys.testattr1, sys.testattr2);
        }
    }
}
