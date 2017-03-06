using System;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestCustomMarshal
    {
        [Test]
        public static void GetManagedStringTwice()
        {
            const string expected = "FooBar";
            using (Py.GIL())
            {
                IntPtr op = Runtime.Runtime.PyUnicode_FromString(expected);
                string s1 = Runtime.Runtime.GetManagedString(op);
                string s2 = Runtime.Runtime.GetManagedString(op);

                Assert.AreEqual(1, Runtime.Runtime.Refcount(op));
                Assert.AreEqual(expected, s1);
                Assert.AreEqual(expected, s2);
            }
        }
    }
}
