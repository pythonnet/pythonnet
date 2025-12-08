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

            using var op = Runtime.Runtime.PyString_FromString(expected);
            string s1 = Runtime.Runtime.GetManagedString(op.BorrowOrThrow());
            string s2 = Runtime.Runtime.GetManagedString(op.Borrow());

            Assert.That(Runtime.Runtime.Refcount32(op.Borrow()), Is.EqualTo(1));
            Assert.That(s1, Is.EqualTo(expected));
            Assert.That(s2, Is.EqualTo(expected));
        }
    }
}
