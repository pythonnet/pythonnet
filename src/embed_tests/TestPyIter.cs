using System.Linq;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest;

public class TestPyIter : BaseFixture
{
    [Test]
    public void KeepOldObjects()
    {
        using (Py.GIL())
        using (var testString = new PyString("hello world! !$%&/()=?"))
        {
            PyObject[] chars = testString.ToArray();
            Assert.IsTrue(chars.Length > 1);
            string reconstructed = string.Concat(chars.Select(c => c.As<string>()));
            Assert.AreEqual(testString.As<string>(), reconstructed);
        }
    }
}
