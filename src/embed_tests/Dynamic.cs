using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class DynamicTest
    {
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
            Assert.That(_stream, Is.EqualTo(stream));

            PythonEngine.RunSimpleString(
                "import sys\n" +
                "sys.testattr.Append('Hello!')\n");
            Assert.That(stream.ToString(), Is.EqualTo("Hello!"));
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
        [Test]
        public void AssignPyObject()
        {
            dynamic sys = Py.Import("sys");
            dynamic io = Py.Import("io");
            sys.testattr = io.StringIO();
            dynamic bb = sys.testattr; // Get the PyObject
            bb.write("Hello!");
            Assert.That(bb.getvalue().ToString(), Is.EqualTo("Hello!"));
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
            Assert.That(sys.testattr3.ToString(), Is.EqualTo("True"));

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

            Assert.That(sys.testattr3.ToString(), Is.EqualTo("True"));

            // Compare in .NET
            Assert.IsTrue(sys.testattr1.Equals(sys.testattr2));
        }

        // regression test for https://github.com/pythonnet/pythonnet/issues/1848
        [Test]
        public void EnumEquality()
        {
            using var scope = Py.CreateScope();
            scope.Exec(@"
import enum

class MyEnum(enum.IntEnum):
    OK = 1
    ERROR = 2

def get_status():
    return MyEnum.OK 
"
);

            dynamic MyEnum = scope.Get("MyEnum");
            dynamic status = scope.Get("get_status").Invoke();
            Assert.IsTrue(status == MyEnum.OK);
        }

        // regression test for https://github.com/pythonnet/pythonnet/issues/1680
        [Test]
        public void ForEach()
        {
            dynamic pyList = PythonEngine.Eval("[1,2,3]");
            var list = new List<int>();
            foreach (int item in pyList)
                list.Add(item);
        }
    }
}
