using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyIterTest
    {
        [Test]
        public void TestOnPyList()
        {
            using (Py.GIL())
            {
                var list = new PyList();
                list.Append(new PyString("foo"));
                list.Append(new PyString("bar"));
                list.Append(new PyString("baz"));
                var result = new List<string>();
                foreach (PyObject item in list)
                {
                    result.Add(item.ToString());
                }
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual("foo", result[0]);
                Assert.AreEqual("bar", result[1]);
                Assert.AreEqual("baz", result[2]);
            }
        }
    }
}
