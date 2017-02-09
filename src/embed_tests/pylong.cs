using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class PyLongTest
    {
        [Test]
        public void TestToInt64()
        {
            using (Py.GIL())
            {
                long largeNumber = 8L * 1024L * 1024L * 1024L; // 8 GB
                var pyLargeNumber = new PyLong(largeNumber);
                Assert.AreEqual(largeNumber, pyLargeNumber.ToInt64());
            }
        }
    }
}
