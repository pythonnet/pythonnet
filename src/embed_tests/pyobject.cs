using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyObjectTest
    {
        [Test]
        public void TestUnicode()
        {
            using (Py.GIL())
            {
                PyObject s = new PyString("foo\u00e9");
                Assert.AreEqual("foo\u00e9", s.ToString());
            }
        }
    }
}
