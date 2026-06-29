namespace Python.EmbeddingTest
{
    using NUnit.Framework;
    using Python.Runtime;

    public class References
    {
        [Test]
        public void MoveToPyObject_SetsNull()
        {
            var dict = new PyDict();
            NewReference reference = Runtime.PyDict_Items(dict.Reference);
            try
            {
                Assert.That(reference.IsNull(), Is.False);

                using (reference.MoveToPyObject())
                    Assert.That(reference.IsNull(), Is.True);
            }
            finally
            {
                reference.Dispose();
            }
        }

        [Test]
        public void CanBorrowFromNewReference()
        {
            var dict = new PyDict();
            using NewReference reference = Runtime.PyDict_Items(dict.Reference);
            BorrowedReference borrowed = reference.BorrowOrThrow();
            PythonException.ThrowIfIsNotZero(Runtime.PyList_Reverse(borrowed));
        }
    }
}
